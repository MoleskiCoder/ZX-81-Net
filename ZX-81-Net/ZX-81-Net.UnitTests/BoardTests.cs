namespace ZX_81_Net.UnitTests
{
    using EightBit;
    using System.Diagnostics;
    using ZX_81_Net;

    [TestClass]
    public sealed class BoardTests
    {
        private readonly Configuration _configuration;
        private SealedBoard? _board;

        private int _instructionsUnderReset;
        private bool _underReset;

        public BoardTests()
        {
            Directory.SetCurrentDirectory(@"c:\github\zx81");
            this._configuration = new Configuration();
        }

        [TestInitialize]
        public void Setup()
        {
            this._board = new SealedBoard(this._configuration);
            this._board.CPU.ExecutingInstruction += this.CPU_ExecutingInstruction;
            this._board.CPU.ExecutedInstruction += this.CPU_ExecutedInstruction;
            this._instructionsUnderReset = 0;
            this._board.Initialize();
            this._board.RaisePOWER();
        }

        [TestCleanup]
        public void Cleanup()
        {
            Debug.Assert(this._board is not null);
            this._board.LowerPOWER();
            this._board.CPU.ExecutingInstruction -= this.CPU_ExecutingInstruction;
            this._board.CPU.ExecutedInstruction -= this.CPU_ExecutedInstruction;
        }

        private void CPU_ExecutingInstruction(object? sender, EventArgs e)
        {
            Debug.Assert(this._board is not null);
            this._underReset = this._board.CPU.RESET.Lowered();
        }

        private void CPU_ExecutedInstruction(object? sender, EventArgs e)
        {
            if (this._underReset)
            {
                Debug.Assert(this._board is not null);
                Assert.AreEqual(0x00, this._board.CPU.OpCode);
                ++this._instructionsUnderReset;
            }
        }

        [TestMethod]
        public void TestMotherboardPowersUp()
        {
            Debug.Assert(this._board is not null);
            Assert.IsTrue(this._board.Powered);
            var cpu = this._board.CPU;
            Assert.IsTrue(cpu.Powered);
        }

        [TestMethod]
        public void TestRomMapping()
        {
            var board = this._board;
            Debug.Assert(board is not null);
            var rom = board.ROM;
            Debug.Assert(rom is not null);
            var size = rom.Size;
            Assert.AreEqual(0x2000, size);
            for (ushort i = 0; i < size; ++i)
            {
                var value = rom.Peek(i);
                Assert.AreEqual(value, board.Peek((ushort)(0x0000 + i)));
                Assert.AreEqual(value, board.Peek((ushort)(0x2000 + i)));
                Assert.AreEqual(value, board.Peek((ushort)(0x8000 + i)));
                Assert.AreEqual(value, board.Peek((ushort)(0xa000 + i)));
            }
        }

        [TestMethod]
        public void TestRamMapping()
        {
            var board = this._board;
            Debug.Assert(board is not null);
            var ram = board.RAM;
            Debug.Assert(ram is not null);
            var size = ram.Size;
            Assert.AreEqual(0x4000, size);
            for (ushort i = 0; i < size; ++i)
            {
                var value = Chip.LowByte(i);
                ram.Poke(i, value);
                Assert.AreEqual(value, board.Peek((ushort)(0x4000 + i)));
                Assert.AreEqual(value, board.Peek((ushort)(0xc000 + i)));
            }
        }
    }
}
