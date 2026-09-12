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
        public void TestRealPortReadResetsScanLineAndRasterOffset()
        {
            var board = this._board;
            var cpu = board.CPU;

            ushort destination = 0x4000;
            var loop = destination;
            board.Poke(destination++, 0x00);  // loop: NOP
            board.Poke(destination++, 0x18);  //       JR loop (-3)
            board.Poke(destination++, 0xfd);
            cpu.PC.Joined = loop;

            for (var i = 0; i < 5; i++)
            {
                this.ULA.RenderLine();
            }
            Assert.AreEqual(5, this.ULA.ScanLine);

            this.FreezeLINECNTR();

            Assert.AreEqual(0, this.ULA.ScanLine);
            Assert.AreEqual(0, this.ULA.RasterOffset);
        }

        [TestMethod]
        public void TestRenderLineHoldsAtLastScanLineWithoutVSync()
        {
            var board = this._board;
            var cpu = board.CPU;

            ushort destination = 0x4000;
            var loop = destination;
            board.Poke(destination++, 0x00);  // loop: NOP
            board.Poke(destination++, 0x18);  //       JR loop (-3)
            board.Poke(destination++, 0xfd);
            cpu.PC.Joined = loop;

            var rasterHeight = this._configuration.Timings.RasterHeight;

            // Deliberately run well past RasterHeight - no port read occurs,
            // so nothing should ever reset ScanLine back to 0.
            for (var i = 0; i < rasterHeight + 20; i++)
            {
                this.ULA.RenderLine();
            }

            Assert.AreEqual(rasterHeight - 1, this.ULA.ScanLine);
        }

        [TestMethod]
        public void TestRenderLineAdvancesScanLineAndResetsRasterOffset()
        {
            var board = this._board;
            var cpu = board.CPU;

            ushort destination = 0x4000;
            var loop = destination;
            board.Poke(destination++, 0x00);  // loop: NOP
            board.Poke(destination++, 0x18);  //       JR loop (-3)
            board.Poke(destination++, 0xfd);
            cpu.PC.Joined = loop;

            Assert.AreEqual(0, this.ULA.ScanLine);

            this.ULA.RenderLine();

            Assert.AreEqual(1, this.ULA.ScanLine);
            Assert.AreEqual(0, this.ULA.RasterOffset);
        }

        [TestMethod]
        public void TestRenderCharacterSequenceStaysAlignedAcrossMultipleCharacters()
        {
            var board = this._board;
            var cpu = board.CPU;

            cpu.IV = 0x40; // RAM-backed "character ROM" page, as before
            this.ULA.SetLineCounter(0);

            // Three consecutive display-file bytes (bit 6 clear - genuine characters)
            board.Poke(0xC000, 5);
            board.Poke(0xC001, 10);
            board.Poke(0xC002, 15);

            // Distinct bitmaps at each code's computed character address (line 0)
            board.Poke(0x4000 + (5 << 3), 0b10000001);
            board.Poke(0x4000 + (10 << 3), 0b01000010);
            board.Poke(0x4000 + (15 << 3), 0b00100100);

            cpu.PC.Joined = 0xC000;

            // Prime _renderingCharacter/_character: nothing has been fetched yet, so
            // the very first RenderCharacter() call would still reflect whatever
            // state existed before this test ran. Force one real fetch first so the
            // loop below starts from a known-correct state rather than stale boot data.
            _ = cpu.Step();

            var pixels = this.ULA.Pixels!;
            var expectedBitmaps = new byte[] { 0b10000001, 0b01000010, 0b00100100 };

            for (var character = 0; character < 3; ++character)
            {
                this.ULA.RenderCharacter();
            }

            for (var character = 0; character < 3; ++character)
            {
                var bitmap = expectedBitmaps[character];
                for (var bit = 0; bit < 8; ++bit)
                {
                    var expectInk = (bitmap & (1 << bit)) != 0;
                    var actual = pixels[character * 8 + bit];
                    Assert.AreEqual(expectInk ? 1u : 0u, actual,
                        $"character {character}, bit {bit} mismatch (expected bitmap {bitmap:X2})");
                }
            }
        }

        [TestMethod]
        public void TestRenderCharacterPaintsBitmapWhenRenderingCharacterTrue()
        {
            var board = this._board;
            var cpu = board.CPU;

            cpu.IV = 0x40; // NOT 0x1E - that's the real (read-only) character ROM page
            this.ULA.SetLineCounter(3);
            this.ULA.Character = 5;
            this.ULA.SetRenderingCharacter(true);

            var bitmap = (byte)0b10110000;
            board.Poke(this.ULA.ComputeCharacterAddress(), bitmap); // now 0x402B - genuine RAM

            this.ULA.RenderCharacter();

            var pixels = this.ULA.Pixels!;
            for (var bit = 0; bit < 8; ++bit)
            {
                var expectInk = (bitmap & (1 << bit)) != 0;
                Assert.AreEqual(expectInk ? 1u : 0u, pixels[bit], $"bit {bit} mismatch");
            }
        }

        [TestMethod]
        public void TestRenderCharacterPaintsBlankWhenRenderingCharacterFalse()
        {
            var board = this._board;

            this.ULA.SetRenderingCharacter(false);
            // Deliberately poke a non-zero byte at whatever address CharacterAddress()
            // would compute, to prove it's genuinely ignored on this path, not just
            // coincidentally zero.
            board.Poke(this.ULA.ComputeCharacterAddress(), 0xFF);

            this.ULA.RenderCharacter();

            var pixels = this.ULA.Pixels!;
            for (var bit = 0; bit < 8; ++bit)
            {
                Assert.AreEqual(0u, pixels[bit], $"bit {bit} should be paper colour");
            }
        }

        [TestMethod]
        public void TestRenderingTextIsStaleAfterInstructionCompletes()
        {
            var board = this._board;
            var cpu = board.CPU;

            board.Poke(0xC000, 0x04); // bit 6 clear - would be "rendering" if checked during the actual fetch
            cpu.PC.Joined = 0xC000;

            _ = cpu.Step(); // fetch + execute completes - M1 is back to Raised by now

            var result = this.ULA.CheckRenderingText();

            Assert.IsFalse(result,
                "RenderingText() no longer reflects the fetch that already happened - " +
                "RenderCharacter() cannot safely re-derive this after the fact; it needs " +
                "a value captured at the moment of the real CPU_ReadMemory event instead.");
        }

        [TestMethod]
        public void TestRenderingTextTrueWhenAddressingAndBit6Clear()
        {
            var cpu = this._board.CPU;
            this._board.Poke(0xC000, 0x04);   // INC B — bit 6 clear, should be intercepted

            cpu.PC.Joined = 0xC000;
            cpu.B = 0x55;
            _ = cpu.Step();

            Assert.AreEqual((byte)0x55, cpu.B);  // unchanged: real INC B never ran
        }

        [TestMethod]
        public void TestRenderingTextFalseWhenBit6Set()
        {
            var cpu = this._board.CPU;
            this._board.Poke(0xC000, 0x44);   // LD B,H — bit 6 set, should NOT be intercepted

            cpu.PC.Joined = 0xC000;
            cpu.B = 0x00;
            cpu.H = 0x99;
            _ = cpu.Step();

            Assert.AreEqual((byte)0x99, cpu.B);  // real instruction ran: B <- H
        }

        [TestMethod]
        public void TestRenderingTextFalseWhenAddressBit15Clear()
        {
            var cpu = this._board.CPU;
            this._board.Poke(0x4000, 0x04);   // INC B — bit 6 clear, but addressing bit fails

            cpu.PC.Joined = 0x4000;
            cpu.B = 0x55;
            _ = cpu.Step();

            Assert.AreEqual((byte)0x56, cpu.B);  // real INC B ran
        }

        [TestMethod]
        [DataRow((byte)0x1E, (byte)5, 3, (ushort)0x1E2B)]
        [DataRow((byte)0x1E, (byte)40, 5, (ushort)0x1F45)]  // code's low 6 bits >= 32 — the case that used to lose its carry
        [DataRow((byte)0x1F, (byte)0, 0, (ushort)0x1F00)]
        public void TestCharacterAddress(byte iv, byte code, int lineCounter, ushort expected)
        {
            this._board.CPU.IV = iv;
            this.ULA.SetLineCounter(lineCounter);
            this.ULA.Character = code;

            Assert.AreEqual(expected, this.ULA.ComputeCharacterAddress());
        }

        [TestMethod]
        public void TestCharacterAddressIgnoresInverseBit()
        {
            this._board.CPU.IV = 0x1E;
            this.ULA.SetLineCounter(5);

            this.ULA.Character = 40;
            var normal = this.ULA.ComputeCharacterAddress();

            this.ULA.Character = (byte)(0x80 | 40);  // same base character, inverse video
            var inverse = this.ULA.ComputeCharacterAddress();

            Assert.AreEqual(normal, inverse);
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

            Assert.IsInRange(0x66, 0x80, cpu.PC.Joined);    // Somewhere within the NMI handler

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
        public void TestRenderingTextTriggersUnderRunDrivenExecution()
        {
            var board = this._board;
            var cpu = board.CPU;

            board.Poke(0xC000, 0x04);   // INC B
            cpu.PC.Joined = 0xC000;
            cpu.B = 0x55;

            _ = cpu.Run(1);

            Assert.AreEqual((byte)0x55, cpu.B);  // if this fails, Run() doesn't see it the way Step() does
        }

        [TestMethod]
        public void TestMaskableInterruptNeededOnFallingEdge()
        {
            var cpu = this._board.CPU;

            cpu.REFRESH = 0x40; // bit 6 set
            _ = this.ULA.CheckMaskableInterruptNeeded();

            cpu.REFRESH = 0x00; // bit 6 now clear - falling edge
            Assert.IsTrue(this.ULA.CheckMaskableInterruptNeeded());
        }

        [TestMethod]
        public void TestMaskableInterruptNotNeededWithoutFallingEdge()
        {
            var cpu = this._board.CPU;

            cpu.REFRESH = 0x00;
            _ = this.ULA.CheckMaskableInterruptNeeded();

            cpu.REFRESH = 0x40; // rising edge, not falling
            Assert.IsFalse(this.ULA.CheckMaskableInterruptNeeded());
        }

        [TestMethod]
        public void TestMaskableInterruptEventuallyFiresDuringExecution()
        {
            var board = this._board;
            var cpu = board.CPU;

            ushort destination = 0x4000;
            var loop = destination;
            board.Poke(destination++, 0x00);  // loop: NOP
            board.Poke(destination++, 0x18);  //       JR loop (-3)
            board.Poke(destination++, 0xfd);
            cpu.PC.Joined = loop;

            var intLowered = false;
            void CPU_LoweredINT(object? s, EventArgs e) => intLowered = true;
            cpu.LoweredINT += CPU_LoweredINT; // assuming this event exists, mirroring LoweredNMI

            for (var i = 0; i < 500 && !intLowered; i++)
            {
                _ = cpu.Step();
            }

            Assert.IsTrue(intLowered);
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

            _ = cpu.Step();  // LD A, $data
            _ = cpu.Step();  // OUT ($port), A

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

            _ = cpu.Step();  // IN A, ($port)

            Assert.AreEqual(code + 2, cpu.PC.Joined);
        }
    }
}
