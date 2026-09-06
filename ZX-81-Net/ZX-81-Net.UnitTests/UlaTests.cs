namespace ZX_81_Net.UnitTests
{
    using EightBit;
    using ZX_81_Net;

    [TestClass]
    public sealed class UlaTests
    {
        private readonly Configuration _configuration;
        private readonly EightBit.ILogger _logger;
        private readonly SealedBoard _board;
        private SealedUla ULA => this._board.ULA as SealedUla ?? throw new InvalidOperationException("ULA is not a SealedUla.");

        public UlaTests()
        {
            this._configuration = new Configuration();
            this._logger = new ConsoleLogger("Ula tests");
            this._logger.Verbosity = this._configuration.LoggingLevel;
            this._board = new SealedBoard(this._logger, this._configuration);
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
        public void TestNMIPulseWidth()
        {
            this.EnableNMI();

            var cpu = this._board.CPU;
            var ula = this.ULA;

            var ulaCycles = 0;
            void ULA_Ticked(object? s, EventArgs e) => ulaCycles++;
            ula.Ticked += ULA_Ticked;

            int? loweredAt = null;
            int? raisedAt = null;
            void CPU_LoweredNMI(object? s, EventArgs e) => loweredAt = ulaCycles;
            void CPU_RaisedNMI(object? s, EventArgs e)
            {
                Assert.IsNotNull(loweredAt);
                Assert.IsNull(raisedAt);
                raisedAt = ulaCycles;
            }
            cpu.LoweredNMI += CPU_LoweredNMI;
            cpu.RaisedNMI += CPU_RaisedNMI;

            ula.RenderLine();

            ula.Ticked -= ULA_Ticked;
            cpu.LoweredNMI -= CPU_LoweredNMI;
            cpu.RaisedNMI -= CPU_RaisedNMI;

            Assert.IsNotNull(loweredAt);
            Assert.IsNotNull(raisedAt);
            Assert.AreEqual(ITimings.RasterWidth, loweredAt);
            Assert.IsGreaterThan(ITimings.RasterWidth, raisedAt.Value);
            Assert.IsLessThanOrEqualTo(ITimings.RasterWidth + ITimings.HorizontalRetraceClocks, raisedAt.Value);
            Assert.AreEqual(ITimings.HorizontalRetraceClocks, raisedAt.Value - loweredAt.Value);
        }

        [TestMethod]
        public void TestNMIEnabledTriggersDuringRenderLine()
        {
            this.EnableNMI();

            var board = this._board;
            var cpu = board.CPU;

            ushort destination = 0x4000;
            var loop = destination;
            board.Poke(destination++, 0x00);  // loop: NOP
            board.Poke(destination++, 0x18);  //       JR loop (-3)
            board.Poke(destination++, 0xfd);

            cpu.PC.Joined = loop;
            var priorSP = cpu.SP.Joined;

            var nmiCounter = 0;
            void CPU_LoweredNMI(object? s, EventArgs e) => nmiCounter++;
            cpu.LoweredNMI += CPU_LoweredNMI;

            this.ULA.RenderLine();
            Assert.AreEqual(1, nmiCounter);

            Assert.AreEqual((ushort)0x66, cpu.PC.Joined);

            // Not just a return from the NMI to normal flow, but to our expected code
            Assert.AreEqual((ushort)(priorSP - 2), cpu.SP.Joined);
            var pushedLow = board.Peek((ushort)(priorSP - 2));
            var pushedHigh = board.Peek((ushort)(priorSP - 1));
            var returnedTo = EightBit.Chip.MakeShort(pushedLow, pushedHigh);
            Assert.AreEqual(loop, returnedTo);
        }

        [TestMethod]
        public void TestNMIDisabledDoesNotTriggerDuringRenderLine()
        {
            this.DisableNMI();

            var board = this._board;
            var cpu = board.CPU;

            ushort destination = 0x4000;
            var loop = destination;
            board.Poke(destination++, 0x00);  // loop: NOP
            board.Poke(destination++, 0x18);  //       JR loop (-3)
            board.Poke(destination++, 0xfd);

            cpu.PC.Joined = loop;
            var priorSP = cpu.SP.Joined;

            var nmiCounter = 0;
            void CPU_LoweredNMI(object? s, EventArgs e) => nmiCounter++;
            cpu.LoweredNMI += CPU_LoweredNMI;

            this.ULA.RenderLine();
            Assert.AreEqual(0, nmiCounter);

            Assert.IsInRange(loop, loop + 2, cpu.PC.Joined);
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
            this.FreezeLINECNTR();
            Assert.AreEqual(0, this.ULA.LINECNTR);

            // Restart the line counter
            this.ThawLINECNTR();

            // Render a single line and make sure LINECNTR has triggered
            this.ULA.RenderLine();
            Assert.AreEqual(1, this.ULA.LINECNTR);

            // Stop and reset the line counter
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
