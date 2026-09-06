namespace ZX_81_Net.UnitTests
{
    internal class SealedUla : AbstractUla<uint, byte>
    {
        private readonly AbstractColorPalette<uint> _palette = new SealedColorPalette();

        internal SealedUla(EightBit.ILogger logger, SealedBoard board)
        : base(logger, board, board.Timings, board.CPU, board.Ports)
        {
        }

        protected override AbstractColorPalette<uint> Palette => _palette;

        protected override void InitialiseKeyboardMapping()
        {
            // Minimal implementation to satisfy the abstract contract.
            // Populate _keyboardMapping here if your code expects keyboard behaviour.
        }
    }
}
