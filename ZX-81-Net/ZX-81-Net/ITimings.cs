namespace ZX_81_Net
{
    internal interface ITimings
    {
        public const int ActiveRasterWidth = 256;
        public const int ActiveRasterHeight = 192;

        public const int LeftRasterBorder = 64;
        public const int RightRasterBorder = 64;

        public const int RasterWidth = LeftRasterBorder + ActiveRasterWidth + RightRasterBorder;

        public const int TotalHorizontalClocks = HorizontalRetraceClocks + RasterWidth;

        public const int HorizontalRetraceClocks = 30;
        public const int VerticalRetraceLines = 6;

        public abstract int TopRasterBorder { get; }
        public abstract int BottomRasterBorder { get; }

        public abstract float UlaClockRate { get; }
        public float CpuClockRate => this.UlaClockRate / 2.0f;

        public int RasterHeight => this.TopRasterBorder + ActiveRasterHeight + this.BottomRasterBorder;
        public int TotalHeight => VerticalRetraceLines + this.RasterHeight;

        public int TotalClocks => TotalHorizontalClocks * this.TotalHeight;

        public float FramesPerSecond => this.UlaClockRate / this.TotalClocks;
    }
}
