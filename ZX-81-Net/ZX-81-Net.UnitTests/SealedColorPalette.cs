namespace ZX_81_Net.UnitTests
{
    using EightBit;

    internal class SealedColorPalette : AbstractColorPalette<uint>
    {
        protected override uint ExactColour(byte red, byte green, byte blue) => (uint)Mask.Sixteen;
    }
}
