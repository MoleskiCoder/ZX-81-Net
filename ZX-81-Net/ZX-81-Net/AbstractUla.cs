namespace ZX_81_Net
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    internal class AbstractUla<ColorT, KeyT> : EightBit.ClockedChip
    {
        public event EventHandler<EventArgs>? Proceed;

        public void RenderLines()
        {
        }
    }
}