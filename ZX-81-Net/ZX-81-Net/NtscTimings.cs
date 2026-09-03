namespace ZX_81_Net
{
    internal sealed class NtscTimings : ITimings
    {
        public int TopRasterBorder { get; } = 32;
        public int BottomRasterBorder { get; } = 32;

        public float UlaClockRate { get; } = 6_500_000.0f;
    }
}
