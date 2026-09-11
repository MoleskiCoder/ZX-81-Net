namespace ZX_81_Net
{
    using EightBit;
    using System.Diagnostics;

    internal class AbstractBoard : EightBit.Bus
    {
        protected readonly Z80.Disassembler? _disassembler;
        protected readonly bool _disassembling;

        protected readonly EightBit.MemoryMapping _romMapping;
        protected readonly EightBit.MemoryMapping _ramMapping;

        private readonly ILogger _logger;

        private int _allowed;

        protected AbstractBoard(EightBit.ILogger logger, Configuration configuration)
        {
            this.Settings = configuration;

            this._logger = logger;
            this.CPU = new Z80.Z80(this, this.Ports);
            this._disassembler = new Z80.Disassembler(this);
            this._disassembling = configuration.DebugMode;
            this._romMapping = new(this.ROM, 0x0000, (int)Mask.Thirteen, EightBit.AccessLevel.ReadOnly);
            this._ramMapping = new(this.RAM, 0x4000, (int)Mask.Fourteen, EightBit.AccessLevel.ReadWrite);
        }

        protected Configuration Settings { get; }

        public ITimings Timings => Settings.Timings;

        public Z80.Z80 CPU { get; }

        public EightBit.InputOutput Ports { get; } = new EightBit.InputOutput();

        public EightBit.Rom ROM { get; } = new EightBit.Rom();

        public EightBit.Ram RAM { get; } = new EightBit.Ram(0x4000);

        public override void Initialize()
        {
            if (this._disassembling)
            {
                this.CPU.ExecutingInstruction += this.CPU_ExecutingInstruction;
                this.CPU.ExecutedInstruction += this.CPU_ExecutedInstruction;
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
            this._allowed = this.Timings.PowerOnResetCycles - 1;
            this.RunCycle();
            this.CPU.RaiseRESET();
        }

        public void Plug(string path) => this.ROM.Load(path);

        public override EightBit.MemoryMapping Mapping(ushort absolute)
        {
            var a14 = absolute & (ushort)Bits.Bit14;
            return a14 == 0 ? this._romMapping : this._ramMapping;
        }

        private string _disassembled = "";
        private byte _disassembledOpCode;

        private void CPU_ExecutingInstruction(object? sender, EventArgs e)
        {
            Debug.Assert(this._disassembler is not null, "Disassembler has not been initialized.");
            Debug.Assert(sender is Z80.Z80);
            var cpu = (Z80.Z80)sender;
            this._disassembled = Z80.Disassembler.State(cpu);
            this._disassembled += cpu.RESET.Raised() ? $" {this._disassembler.Disassemble(cpu)}" : "";
            this._disassembledOpCode = this.Peek(cpu.PC);
        }

        private void CPU_ExecutedInstruction(object? sender, EventArgs e)
        {
            Debug.Assert(sender is Z80.Z80);
            var cpu = (Z80.Z80)sender;
            var injected = cpu.RESET.Raised() && (cpu.OpCode == 0) && (this._disassembledOpCode != 0);
            if (injected)
            {
                this._disassembled += " (NOP)";
            }
            System.Console.WriteLine(this._disassembled);
        }

        protected void RunCycle()
        {
            var taken = this.CPU.Run(++this._allowed);
            this._allowed -= taken;
        }
    }
}
