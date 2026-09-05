namespace ZX_81_Net
{
    using System;
    using System.Diagnostics;

    internal class AbstractBoard : EightBit.Bus
    {
        protected readonly Z80.Disassembler? _disassembler;
        protected readonly bool _disassembling;

        protected readonly EightBit.MemoryMapping _romMapping;
        protected readonly EightBit.MemoryMapping _ramMapping;
        protected readonly EightBit.MemoryMapping _unused8kMapping;
        protected readonly EightBit.MemoryMapping _unused32kMapping;

        private int _allowed;

        protected AbstractBoard(ITimings timings, bool disassembling)
        {
            this.Timings = timings;

            this.CPU = new Z80.Z80(this, this.Ports);
            this._disassembler = new Z80.Disassembler(this);
            this._disassembling = disassembling;

            this._romMapping = new(this.ROM, 0x0000, 0xffff, EightBit.AccessLevel.ReadOnly);
            this._ramMapping = new(this.RAM, 0x4000, 0xffff, EightBit.AccessLevel.ReadWrite);
            this._unused8kMapping = new(this.Unused8K, 0x2000, 0xffff, EightBit.AccessLevel.ReadOnly);
            this._unused32kMapping = new(this.Unused32K, 0x8000, 0xffff, EightBit.AccessLevel.ReadOnly);
        }

        public ITimings Timings { get; }

        public Z80.Z80 CPU { get; }

        public EightBit.InputOutput Ports { get; } = new EightBit.InputOutput();

        public EightBit.Rom ROM { get; } = new EightBit.Rom();

        public EightBit.Ram RAM { get; } = new EightBit.Ram(0x4000);

        public EightBit.UnusedMemory Unused8K { get; } = new EightBit.UnusedMemory(0x2000, 0xff);

        public EightBit.UnusedMemory Unused32K { get; } = new EightBit.UnusedMemory(0x8000, 0xff);

        public override void Initialize()
        {
            if (this._disassembling)
            {
                this.CPU.ExecutingInstruction += this.CPU_ExecutingInstruction;
            }
        }

        public override void RaisePOWER()
        {
            base.RaisePOWER();
            this.CPU.RaisePOWER();
            this.CPU.RaiseINT();
            this.CPU.RaiseNMI();
            this.RunPowerOnReset();
        }

        public override void LowerPOWER()
        {
            this.CPU.LowerPOWER();
            base.LowerPOWER();
        }

        private void RunPowerOnReset()
        {
            this.CPU.RaiseRESET();
            this.CPU.LowerRESET();
            this._allowed = ITimings.PowerOnResetCycles;
            while (this._allowed > 0)
            {
                this.RunCycle();
            }
            this.CPU.RaiseRESET();
        }

        public void Plug(string path) => this.ROM.Load(path);

        public override EightBit.MemoryMapping Mapping(ushort absolute)
        {
            if (absolute < 0x2000)
            {
                return this._romMapping;
            }

            if (absolute < 0x4000)
            {
                return this._unused8kMapping;
            }

            if (absolute < 0x8000)
            {
                return this._ramMapping;
            }

            return this._unused32kMapping;
        }

        private void CPU_ExecutingInstruction(object? sender, System.EventArgs e)
        {
            Debug.Assert(sender is Z80.Z80);
            var cpu = (Z80.Z80)sender;
            if (cpu.OpCode == 0)
                return;
            var state = Z80.Disassembler.State(cpu);
            Debug.Assert(this._disassembler is not null, "Disassembler has not been initialized.");
            var disassembly = this._disassembler.Disassemble(cpu);
            System.Console.WriteLine($"{state} {disassembly}");
        }

        protected void RunCycle()
        {
            var taken = this.CPU.Run(++this._allowed);
            this._allowed -= taken;
        }
    }
}
