namespace ZX_81_Net.UnitTests
{
    using ZX_81_Net;

    [TestClass]
    public sealed class UlaTests
    {
        private readonly SealedBoard _board;
        private SealedUla ULA => this._board.ULA as SealedUla ?? throw new InvalidOperationException("ULA is not a SealedUla.");

        public UlaTests()
        {
            var configuration = new Configuration();
            this._board = new SealedBoard(configuration);
        }

        [TestInitialize]
        public void Setup()
        {
            Directory.SetCurrentDirectory(@"c:\github\zx81");
            this._board.Initialize();
            this._board.RaisePOWER();
        }

        [TestCleanup]
        public void Cleanup()
        {
            this._board.LowerPOWER();
        }

        [TestMethod]
        public void TestUlaPowersUp()
        {
            Assert.IsTrue(this.ULA.Powered);
        }

        [TestMethod]
        public void TestLINECNTR()
        {
            // Stop and reset the line counter
            Console.WriteLine("* Tests: Freezing LINECNTR");
            this.FreezeLINECNTR();
            Assert.AreEqual(0, this.ULA.LINECNTR);

            // Restart the line counter
            Console.WriteLine("* Tests: Thawing LINECNTR");
            this.ThawLINECNTR();

            // Render a single line and make sure LINECNTR has triggered
            Console.WriteLine("* Tests: Rendering line");
            this.ULA.RenderLine();
            Assert.AreEqual(1, this.ULA.LINECNTR);

            // Stop and reset the line counter
            Console.WriteLine("* Tests: Freezing LINECNTR");
            this.FreezeLINECNTR();
            Assert.AreEqual(0, this.ULA.LINECNTR);

            // Render a set of lines
            this.ULA.RenderLine();
            Assert.AreEqual(1, this.ULA.LINECNTR);
            this.ULA.RenderLine();
            Assert.AreEqual(2, this.ULA.LINECNTR);
            this.ULA.RenderLine();
            Assert.AreEqual(3, this.ULA.LINECNTR);
            this.ULA.RenderLine();
            Assert.AreEqual(4, this.ULA.LINECNTR);
            this.ULA.RenderLine();
            Assert.AreEqual(5, this.ULA.LINECNTR);
            this.ULA.RenderLine();
            Assert.AreEqual(6, this.ULA.LINECNTR);
            this.ULA.RenderLine();
            Assert.AreEqual(7, this.ULA.LINECNTR);

            this.ULA.RenderLine();                  // And that's the loop
            Assert.AreEqual(0, this.ULA.LINECNTR);  
        }

        private void ThawLINECNTR() => this.DisableNMI();

        private void EnableNMI() => this.WriteToPort(0xfe);

        private void DisableNMI() => this.WriteToPort(0xfd);

        // Enable/disable NMI and thaw LINECNTR
        private void WriteToPort(byte port, byte data = 0)
        {
            var board = this._board;
            ushort destination = 0x4000;
            var code = destination;

            board.Poke(destination++, (byte)0x3e);  // LD A, $data
            board.Poke(destination++, data);
            board.Poke(destination++, (byte)0xd3);  // OUT ($port), A
            board.Poke(destination++, port);

            var cpu = board.CPU;
            cpu.PC.Joined = code;

            cpu.PoweredStep();  // LD A, $data
            cpu.PoweredStep();  // OUT ($port), A

            Assert.AreEqual(code + 4, cpu.PC.Joined);
        }

        private void FreezeLINECNTR() => this.ReadFromPort(0xfe);

        // Freeze/reset LINECNTR
        private void ReadFromPort(byte port)
        {
            var board = this._board;
            ushort destination = 0x4000;
            var code = destination;

            board.Poke(destination++, (byte)0xdb);  // IN A, ($port)
            board.Poke(destination++, port);

            var cpu = board.CPU;
            cpu.PC.Joined = code;

            cpu.PoweredStep();  // IN A, ($port)

            Assert.AreEqual(code + 2, cpu.PC.Joined);
        }
    }
}
