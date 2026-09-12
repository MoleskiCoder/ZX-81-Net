namespace ZX_81_Net.UnitTests
{
    internal class SealedUla : AbstractUla<uint, byte>
    {
        private readonly AbstractColorPalette<uint> _palette = new SealedColorPalette();

        internal SealedUla(EightBit.ILogger logger, SealedBoard board)
        : base(logger, board, board.Timings, board.CPU, board.Ports)
        {
            this._inkColour = 1;
            this._paperColour = 0;
        }

        protected override AbstractColorPalette<uint> Palette => _palette;

        protected override void InitialiseKeyboardMapping()
        {
            // Minimal implementation to satisfy the abstract contract.
            // Populate _keyboardMapping here if your code expects keyboard behaviour.
        }

        public byte Character
        {
            get => this._character;
            set => this._character = value;
        }

        public void SetLineCounter(int target)
        {
            this.ResetLINECNTR();
            for (var i = 0; i < target; i++)
            {
                this.IncrementLINECNTR();   // unconditional in the current code — ignores frozen state, which is what we want here
            }
        }

        public ushort ComputeCharacterAddress() => this.CharacterAddress();

        public bool CheckMaskableInterruptNeeded() => this.MaskableInterruptNeeded();

        public bool CheckRenderingText() => this.RenderingText();
    }
}
