namespace ZX_81_Net
{
    internal interface ITimings
    {
        public const int ActiveRasterWidth = 256;   // 32 characters
        public const int ActiveRasterHeight = 192;  // 24 characters

        public const int LeftRasterBorder = 64;     // 8 characters
        public const int RightRasterBorder = 64;    // 8 characters

        public const int RasterWidth = LeftRasterBorder + ActiveRasterWidth + RightRasterBorder;

        public const int TotalHorizontalClocks = HorizontalRetraceClocks + RasterWidth;

        public const int HorizontalRetraceClocks = 30;
        public const int VerticalRetraceLines = 6;

        public abstract int TopRasterBorder { get; }
        public abstract int BottomRasterBorder { get; }

        public const float UlaClockRate = 6_500_000.0f;

        public float CpuClockRate => UlaClockRate / 2.0f;

        public int PowerOnResetCycles => 1; // (int)this.CpuClockRate / 10;

        public int RasterHeight => this.TopRasterBorder + ActiveRasterHeight + this.BottomRasterBorder;
        public int TotalHeight => VerticalRetraceLines + this.RasterHeight;

        public int TotalClocks => TotalHorizontalClocks * this.TotalHeight;

        public float FramesPerSecond => UlaClockRate / this.TotalClocks;
    }
}
