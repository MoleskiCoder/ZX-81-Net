namespace ZX_81_Net
{
    using EightBit;
    using System;
    using System.Diagnostics;
    using System.Runtime.CompilerServices;

    internal abstract class AbstractUla<ColorT, KeyT> : EightBit.ClockedChip
    {
        private const int CharactersPerLine = ITimings.RasterWidth / PixelsPerCharacter;

        public const int PixelsPerCharacter = 8;

        private readonly Bus _bus;
        private readonly ITimings _timings;
        private readonly Z80.Z80 _cpu;
        private readonly InputOutput _ports;

        private ColorT[]? _pixels;

        protected ColorT? _inkColour;
        protected ColorT? _paperColour;

        private int _lineCounter; // 3 bits
        private bool _lineCounterFrozen = false;

        public int LINECNTR => this._lineCounter & (int)Mask.Three;

        // These aren't real in the ULA, but they're useful for me
        // to work out where I am in the pixel buffer.
        protected int _scanLine;
        protected int _rasterOffset;

        protected byte _character;

        protected byte _oldRefreshRegister;
        protected bool _enabledNMI;

        protected readonly Dictionary<byte, KeyT[]> _keyboardMapping = [];
        private readonly HashSet<KeyT> _keyboardRaw = [];

        protected abstract AbstractColorPalette<ColorT> Palette { get; }

        public ColorT[]? Pixels => this._pixels;

        private int CharactersPerFrame => this._timings.TotalClocks / PixelsPerCharacter;

        public event EventHandler<EventArgs>? Proceed;

        protected AbstractUla(EightBit.Bus bus, ITimings timings, Z80.Z80 cpu, InputOutput ports)
        {
            this._bus = bus ?? throw new ArgumentNullException(nameof(bus));
            this._timings = timings ?? throw new ArgumentNullException(nameof(timings));
            this._cpu = cpu ?? throw new ArgumentNullException(nameof(cpu));
            this._ports = ports ?? throw new ArgumentNullException(nameof(ports));

            this._cpu.RaisedRFSH += this.CPU_RaisedRFSH;
            this._cpu.ReadMemory += CPU_ReadMemory;

            this.Ticked += this.Ula_Ticked;

            this._ports.ReadingPort += this.Ports_ReadingPort;
            this._ports.WrittenPort += this.Ports_WrittenPort;
        }

        private bool MaskableInterruptNeeded()
        {
            if (this._cpu.M1.Raised()) return false;
            var previous = (this._oldRefreshRegister & 0b00100000) != 0;
            this._oldRefreshRegister = (byte)this._cpu.REFRESH;
            var current = (this._oldRefreshRegister & 0b00100000) != 0;
            return previous && !current;
        }

        private void MaybeTriggerMaskableInterrupt()
        {
            if (MaskableInterruptNeeded())
            {
                this._cpu.LowerINT();
            }
        }

        private void CPU_RaisedRFSH(object? sender, EventArgs e)
        {
            this.MaybeTriggerMaskableInterrupt();
        }

        private void CPU_ReadMemory(object? sender, EventArgs e)
        {
            if (RenderingText())
            {
                this._character = this._bus.Data;
                this._bus.Data = 0;
            }
        }

        private void Ula_Ticked(object? sender, EventArgs e)
        {
            if ((this.Cycles % 2) == 0)
            {
                this.Proceed?.Invoke(this, EventArgs.Empty);
            }
        }

        private void Ports_ReadingPort(object? sender, PortEventArgs e) => this.ReadingPort(e.Port);

        private void Ports_WrittenPort(object? sender, PortEventArgs e) => this.WrittenPort(e.Port);

        protected void FreezeLINECNTR()
        {
            Console.WriteLine("ULA: ** Freezing/resetting LINECNTR");
            this._lineCounterFrozen = true;
            this.ResetLINECNTR();
        }

        protected void ThawLINECNTR()
        {
            Console.WriteLine("ULA: ** Thawing LINECNTR");
            this._lineCounterFrozen = false;
        }

        protected void IncrementLINECNTR()
        {
            this._lineCounter = (this._lineCounter + 1) & (int)Mask.Three;
            Console.WriteLine($"ULA: ** Incremented LINECNTR {this._lineCounter}");
        }

        protected void MaybeIncrementLINECNTR()
        {
            if (_lineCounterFrozen) return;
            this.IncrementLINECNTR();
        }

        protected void ResetLINECNTR() => this._lineCounter = 0;

        protected void MaybeTriggerNMI()
        {
            if (this._enabledNMI)
            {
                Console.Out.WriteLine("ULA: Triggering NMI");
                this._cpu.LowerNMI();
            }
        }

        protected bool RenderingText()
        {
            if (this._cpu.M1.Raised()) return false;
            var addressing = (this._bus.Address.High & (byte)Bits.Bit7) != 0;
            var rendering = (this._bus.Data & (byte)Bits.Bit6) == 0;
            return addressing && rendering;
        }

        public void RenderCharacter(byte bitmap)
        {
            Debug.Assert(this._scanLine < this._timings.RasterHeight);
            Debug.Assert(this._rasterOffset < ITimings.RasterWidth);
            for (int bit = 0; bit < PixelsPerCharacter; ++bit)
            {
                var pixel = (bitmap & Bit(bit)) != 0;
                Debug.Assert(this._inkColour is not null);
                Debug.Assert(this._paperColour is not null);
                var position = this._scanLine * ITimings.RasterWidth + this._rasterOffset++;
                this.SetClockedPixel(position, pixel ? this._inkColour : this._paperColour);
            }
        }

        protected ushort CharacterAddress()
        {
            var start = PromoteByte(this._cpu.IV);
            var offset = (ushort)(((this._character & (byte)Mask.Six) << 3) | this.LINECNTR);
            return (ushort)(start + offset);
        }

        public void RenderCharacter()
        {
            if (this.RenderingText())
            {
                Console.Out.WriteLine($"ULA: Rendering character at raster offset {this._rasterOffset}, {this._rasterOffset / PixelsPerCharacter} character");
                var contents = this._bus.Peek(CharacterAddress());
                this.RenderCharacter(contents);
            }
            else
            {
                Console.Out.WriteLine($"ULA: Rendering blank (at raster offset {this._rasterOffset}, {this._rasterOffset / PixelsPerCharacter} character");
                this.RenderCharacter(0);
            }
        }

        public void RenderLine()
        {
            Tick(ITimings.RasterWidth);
            this.MaybeTriggerNMI();
            this.MaybeIncrementLINECNTR();
            Tick(ITimings.TotalHorizontalClocks);
            this._cpu.RaiseNMI();
        }

        public void RenderLines()
        {
            Console.Out.WriteLine("ULA: Rendering frame");
            for (int cycle = 0; cycle < this._timings.TotalHeight; ++cycle)
            {
                this.RenderLine();
            }

            //Console.Out.WriteLine("ULA: Rendering frame");
            //for (int character = 0; character < this.CharactersPerFrame; ++character)
            //{
            //    if (character % CharactersPerLine == 0)
            //    {
            //        //Console.WriteLine($"ULA: EOL, Scan line {this._scanLine}");
            //        this.MaybeTriggerNMI();
            //        this._scanLine++;
            //        this._rasterOffset = 0;
            //        this.Tick(ITimings.HorizontalRetraceClocks);
            //    }
            //    if (this._scanLine < this._timings.RasterHeight)
            //        this.RenderCharacter();
            //}
            ////Environment.Exit(0);
        }

        public void PokeKey(KeyT raw) => this._keyboardRaw.Add(raw);

        public void PullKey(KeyT raw) => this._keyboardRaw.Remove(raw);

        public override void RaisePOWER()
        {
            base.RaisePOWER();
            this._pixels = new ColorT[ITimings.RasterWidth * this._timings.RasterHeight];
            this.InitialiseKeyboardMapping();
        }

        protected abstract void InitialiseKeyboardMapping();

        private byte FindSelectedKeys(byte rows)
        {
            var returned = (int)Mask.Eight;
            for (var row = 0; row < 8; ++row)
            {
                var current = Bit(row);
                if (((rows & current) != 0) && this._keyboardMapping.TryGetValue(current, out var keys))
                {
                    for (var column = 0; column < 5; ++column)
                    {
                        if (this._keyboardRaw.Contains(keys[column]))
                        {
                            returned &= ~Bit(column);
                        }
                    }
                }
            }

            return (byte)returned;
        }

        // 0 - 4	Keyboard Inputs(0 = Pressed, 1 = Released)
        // 5		Not used
        // 6		UK/US select
        // 7		EAR Input(CAS LOAD)
        // A8..A15	Keyboard Address Output(0 = Select)

        // 128 64 32 16  8  4  2  U
        //   7  6  5  4  3  2  1  0
        //            <----------->	Keyboard
        //         -				Not used (always 1)
        //      -					UK/US select
        //   -						Ear input

        private void ReadingPort(Register16 port)
        {
            var portHigh = port.High;
            var selected = this.FindSelectedKeys((byte)~portHigh);
            var pal = this._timings is PalTimings;
            var timing = pal ? Bit(6) : 0;
            var value = selected | timing;
            this._ports.WriteInputPort(port, (byte)value);

            this.FreezeLINECNTR();

            this._scanLine = 0;
            this._rasterOffset = 0;

            var timingMessage = pal ? "PAL" : "NTSC";
            Console.WriteLine($"ULA: Read port 0x{port.Low:X2}.  Timing is {timingMessage}");
            //Console.WriteLine("ULA: ** Start VSYNC");
            //Console.WriteLine("ULA: ** Stop HSYNC");
        }

        // 0 - 1	NMI control, bit 0 enable, bit 1 disable (both low)
        // 2 - 7	Not used

        // 128 64 32 16  8  4  2  U
        //   7  6  5  4  3  2  1  0
        //                        - NMI enable low
        //                     -    NMI disable low
        //   <-------------->       Unused

        private void WrittenPort(Register16 port)
        {
            // Nominally FE Bit 0 of port address low == NMI on
            var enableNMI = (port.Low & (byte)Bits.Bit0) == 0;
            this._enabledNMI = enableNMI;

            // Nominally FD Bit 1 of port address low == NMI off
            var disableNMI = (port.Low & (byte)Bits.Bit1) == 0;
            this._enabledNMI = !disableNMI;

            this.ThawLINECNTR();

            Console.WriteLine($"ULA: Written port 0x{port.Low:X2} NMI {(this._enabledNMI ? "enabled" : "disabled")}");
            //Console.WriteLine("ULA: ** Stop VSYNC");
            //Console.WriteLine("ULA: ** Start HSYNC");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetClockedPixel(int offset, ColorT colour)
        {
            this.SetPixel(offset, colour);
            this.Tick();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]

        private void SetPixel(int offset, ColorT colour)
        {
            Debug.Assert(this.Pixels is not null);
            this.Pixels[offset] = colour;
        }
    }
}
