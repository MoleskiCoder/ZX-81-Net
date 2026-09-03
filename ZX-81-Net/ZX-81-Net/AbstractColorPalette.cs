namespace ZX_81_Net
{
    internal abstract class AbstractColorPalette<ColorT>
    {
        internal enum Index
        {
            Black,
            White
        }

        protected readonly ColorT[] _colors = new ColorT[2];

        protected AbstractColorPalette()
        {
        }

        public ColorT GetColor(int index, bool bright = false) => this.GetColor(bright ? index + 8 : index);

        public ColorT GetColor(int index) => this._colors[index];

        public ColorT GetColor(Index index) => this.GetColor((int)index);

        protected abstract ColorT ExactColour(byte red, byte green, byte blue);

        protected void LoadExactColour(int idx, byte red, byte green, byte blue) => this._colors[idx] = this.ExactColour(red, green, blue);

        protected void Load()
        {
            this.LoadColour(Index.Black, 0x00, 0x00, 0x00);
            this.LoadColour(Index.White, 0xd7, 0xd7, 0xd7);
        }

        protected void LoadColour(Index idx, byte red, byte green, byte blue) => this.LoadColour((int)idx, red, green, blue);

        protected void LoadColour(int idx, byte red, byte green, byte blue) => this.LoadExactColour(idx, red, green, blue);
    }
}
