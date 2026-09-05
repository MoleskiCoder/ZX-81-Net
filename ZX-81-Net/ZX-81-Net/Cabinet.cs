namespace ZX_81_Net
{
    using SDL3;
    using System.Diagnostics;

    internal sealed class Cabinet(Configuration configuration) : Gaming.Game(configuration.LoggingLevel)
    {
        public Board Motherboard { get; } = new Board(configuration);

        public Configuration Settings { get; } = configuration;

        public void Plug(string path) => this.Motherboard.Plug(path);

        protected override SDL.PixelFormat PixelFormat => ColorPalette.PixelFormat;

        public override float FramesPerSecond => Settings.Timings.FramesPerSecond;

        public override bool UseVSYNC => true;

        public override int DisplayScale => 2;

        public override int RasterWidth => ITimings.RasterWidth;

        public override int RasterHeight => this.Settings.Timings.RasterHeight;

        public override string Title => "Spectrum";

        protected override uint[] Pixels()
        {
            Debug.Assert(this.Motherboard is not null);
            Debug.Assert(this.Motherboard.ULA is not null);
            Debug.Assert(this.Motherboard.ULA.Pixels is not null);
            return this.Motherboard.ULA.Pixels;
        }

        public override void RaisePOWER()
        {
            base.RaisePOWER();
            this.Motherboard.RaisePOWER();
        }

        public override void LowerPOWER()
        {
            Motherboard.LowerPOWER();
            base.LowerPOWER();
        }

        public override void Initialise()
        {
            base.Initialise();
            this.Motherboard.Initialize();
        }

        protected override bool HandleKeyDown(SDL.Keycode key)
        {
            var handled = base.HandleKeyDown(key);
            if (!handled)
            {
                switch (key)
                {
                    case SDL.Keycode.F7:
                    case SDL.Keycode.F8:
                    case SDL.Keycode.F10:
                    case SDL.Keycode.F11:
                        handled = true;
                        break;
                }
                Motherboard.ULA.PokeKey(key);
            }
            return handled;
        }

        protected override bool HandleKeyUp(SDL.Keycode key)
        {
            var handled = base.HandleKeyUp(key);
            if (!handled)
            {
                Motherboard.ULA.PullKey(key);
            }
            return handled;
        }

        protected override void RunRasterLines() => this.Motherboard.RenderLines();
    }
}
