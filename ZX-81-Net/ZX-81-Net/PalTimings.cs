namespace ZX_81_Net
{
    internal sealed class PalTimings : ITimings
    {
        public int TopRasterBorder { get; } = 56;
        public int BottomRasterBorder { get; } = 56;

        public float UlaClockRate { get; } = 6_500_000.0f;
    }
}
