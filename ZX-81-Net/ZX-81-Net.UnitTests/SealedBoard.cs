namespace ZX_81_Net.UnitTests
{
    internal sealed class SealedBoard : AbstractBoard
    {
        private readonly SealedUla _ula;

        public SealedBoard(EightBit.ILogger logger, Configuration configuration)
        : base(logger, configuration)
        {
            this._ula = new SealedUla(logger, this);
        }

        public AbstractUla<uint, byte> ULA => this._ula;

        public override void Initialize()
        {
            base.Initialize();
            var romDirectory = this.Settings.RomDirectory;
            this.Plug(romDirectory + "\\zx81.rom");	// ZX-81 Basic
            this.ULA.Proceed += this.ULA_Proceed;
        }

        public override void RaisePOWER()
        {
            base.RaisePOWER();
            this._ula.RaisePOWER();
        }

        public override void LowerPOWER()
        {
            this._ula.LowerPOWER();
            base.LowerPOWER();
        }

        private void ULA_Proceed(object? sender, EventArgs e) => this.RunCycle();
    }
}
