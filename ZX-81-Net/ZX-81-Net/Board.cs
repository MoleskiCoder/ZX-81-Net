namespace ZX_81_Net
{
    using SDL3;

    internal sealed class Board : AbstractBoard
    {
        private readonly Configuration _configuration;

        public Board(Configuration configuration, ITimings timings)
        : base(timings, configuration.DebugMode)
        {
            this._configuration = configuration;
            this.ULA = new Ula(this);
        }

        public AbstractUla<uint, SDL.Keycode> ULA { get; }

        public override void Initialize()
        {
            base.Initialize();
            var romDirectory = this._configuration.RomDirectory;
            this.Plug(romDirectory + "\\zx81.rom");	// ZX-81 Basic
            this.ULA.Proceed += this.ULA_Proceed;
        }

        public override void RaisePOWER()
        {
            base.RaisePOWER();
            this.ULA.RaisePOWER();
        }

        public override void LowerPOWER()
        {
            this.ULA.LowerPOWER();
            base.LowerPOWER();
        }

        public void RenderLines() => this.ULA.RenderLines();

        private void ULA_Proceed(object? sender, EventArgs e) => this.RunCycle();
    }
}

