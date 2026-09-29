using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace FTDI_I2C_XP
{
    public partial class Form1 : Form
    {
        // ============================================================
        // FTDI D2XX
        // ============================================================

        private static class FTDI
        {
            [DllImport("ftd2xx.dll")]
            public static extern uint FT_Open(
                int deviceNumber,
                ref IntPtr handle);

            [DllImport("ftd2xx.dll")]
            public static extern uint FT_Close(
                IntPtr handle);

            [DllImport("ftd2xx.dll")]
            public static extern uint FT_SetBitMode(
                IntPtr handle,
                byte mask,
                byte mode);

            [DllImport("ftd2xx.dll")]
            public static extern uint FT_GetBitMode(
                IntPtr handle,
                ref byte value);

            [DllImport("ftd2xx.dll")]
            public static extern uint FT_Write(
                IntPtr handle,
                byte[] buffer,
                uint bytesToWrite,
                ref uint bytesWritten);

            [DllImport("ftd2xx.dll")]
            public static extern uint FT_SetBaudRate(
                IntPtr handle,
                uint baudRate);

            [DllImport("ftd2xx.dll")]
            public static extern uint FT_SetLatencyTimer(
                IntPtr handle,
                byte latency);

            public const uint FT_OK = 0;
        }

        private IntPtr ftHandle = IntPtr.Zero;

        // ============================================================
        // FT232R PIN ASSIGNMENT
        //
        // D2 / RTS = SCL
        // D3 / CTS = SDA
        //
        // D1 / RXD intentionally unused.
        // ============================================================

        private const byte SCL = 0x04;
        private const byte SDA = 0x08;

        // Open-drain emulation:
        //
        // direction bit 1 = OUTPUT
        // direction bit 0 = INPUT
        //
        // LOW  = OUTPUT + 0
        // HIGH = INPUT + external pull-up

        private byte directionMask = 0x00;
        private const byte outputValue = 0x00;

        private bool opened = false;

        // Slow on purpose.
        private int delayMs = 1;

        // Sequence interpreter
        private byte sequenceAddress = 0x76;
        private bool sequenceStopRequested = false;
        private bool sequenceRunning = false;

        // ============================================================
        // GUI
        // ============================================================

        private Button btnOpen = new Button();
        private Button btnScan = new Button();
        private Button btnWrite = new Button();
        private Button btnRead = new Button();
        private Button btnCombined = new Button();
        private Button btnClear = new Button();

        private Button btnRunSequence = new Button();
        private Button btnStopSequence = new Button();

        private TextBox txtAddress = new TextBox();
        private TextBox txtWrite = new TextBox();
        private TextBox txtReadCount = new TextBox();

        private TextBox txtCombinedWrite = new TextBox();
        private TextBox txtCombinedReadCount = new TextBox();

        private TextBox txtSequence = new TextBox();
        private TextBox txtLog = new TextBox();

        private Label lblSCL = new Label();
        private Label lblSDA = new Label();
        private Label lblStatus = new Label();

        // ============================================================
        // CONSTRUCTOR
        // ============================================================

        public Form1()
        {
            InitializeComponent();
            BuildGui();
        }

        // ============================================================
        // BUILD GUI
        // ============================================================

        private void BuildGui()
        {
            Text = "FT232R I2C Workbench - XP";

            ClientSize = new Size(900, 820);

            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;

            // --------------------------------------------------------
            // FTDI
            // --------------------------------------------------------

            Controls.Add(Header("FTDI", 20, 20));

            btnOpen.Text = "OPEN FTDI #0";
            btnOpen.SetBounds(20, 50, 160, 35);
            btnOpen.Click += new EventHandler(BtnOpen_Click);
            Controls.Add(btnOpen);

            lblStatus.Text = "Closed";
            lblStatus.SetBounds(200, 50, 180, 35);
            lblStatus.BorderStyle = BorderStyle.FixedSingle;
            lblStatus.TextAlign = ContentAlignment.MiddleCenter;
            Controls.Add(lblStatus);

            Controls.Add(LabelText("SCL D2/RTS:", 420, 57));

            SetupLed(lblSCL, 510, 52);
            Controls.Add(lblSCL);

            Controls.Add(LabelText("SDA D3/CTS:", 560, 57));

            SetupLed(lblSDA, 660, 52);
            Controls.Add(lblSDA);

            // --------------------------------------------------------
            // SCANNER
            // --------------------------------------------------------

            Controls.Add(Header("I2C SCANNER", 20, 110));

            btnScan.Text = "SCAN 0x08 - 0x77";
            btnScan.SetBounds(20, 140, 200, 35);
            btnScan.Click += new EventHandler(BtnScan_Click);
            Controls.Add(btnScan);

            // --------------------------------------------------------
            // MANUAL ACCESS
            // --------------------------------------------------------

            Controls.Add(Header("MANUAL ACCESS", 20, 200));

            Controls.Add(LabelText("Address:", 20, 240));

            txtAddress.Text = "76";
            txtAddress.SetBounds(90, 236, 70, 28);
            Controls.Add(txtAddress);

            Controls.Add(LabelText("(7-bit HEX)", 170, 240));

            // WRITE

            Controls.Add(LabelText("WRITE:", 20, 290));

            txtWrite.Text = "AA";
            txtWrite.SetBounds(90, 286, 430, 28);
            txtWrite.Font =
                new Font(FontFamily.GenericMonospace, 10);

            Controls.Add(txtWrite);

            btnWrite.Text = "WRITE";
            btnWrite.SetBounds(540, 284, 120, 32);
            btnWrite.Click += new EventHandler(BtnWrite_Click);
            Controls.Add(btnWrite);

            // READ

            Controls.Add(LabelText("READ bytes:", 20, 340));

            txtReadCount.Text = "1";
            txtReadCount.SetBounds(110, 336, 60, 28);
            Controls.Add(txtReadCount);

            btnRead.Text = "READ";
            btnRead.SetBounds(540, 334, 120, 32);
            btnRead.Click += new EventHandler(BtnRead_Click);
            Controls.Add(btnRead);

            // COMBINED

            Controls.Add(
                Header(
                    "WRITE -> REPEATED START -> READ",
                    20,
                    390));

            Controls.Add(LabelText("Write:", 20, 425));

            txtCombinedWrite.Text = "D0";
            txtCombinedWrite.SetBounds(80, 421, 250, 28);
            txtCombinedWrite.Font =
                new Font(FontFamily.GenericMonospace, 10);

            Controls.Add(txtCombinedWrite);

            Controls.Add(LabelText("Read:", 350, 425));

            txtCombinedReadCount.Text = "1";
            txtCombinedReadCount.SetBounds(395, 421, 50, 28);
            Controls.Add(txtCombinedReadCount);

            Controls.Add(LabelText("bytes", 450, 425));

            btnCombined.Text = "EXECUTE";
            btnCombined.SetBounds(540, 419, 120, 32);
            btnCombined.Click += new EventHandler(BtnCombined_Click);
            Controls.Add(btnCombined);

            // ========================================================
            // SEQUENCE
            // ========================================================

            Controls.Add(Header("SEQUENCE", 20, 485));

            Label syntax = new Label();

            syntax.Text =
                "Syntax - one command per line:\r\n" +
                "ADDR 76       set 7-bit slave address\r\n" +
                "W F4 27       write bytes\r\n" +
                "R 8           read 8 bytes\r\n" +
                "WR D0 1       write D0 -> restart -> read 1 byte\r\n" +
                "DELAY 100     wait 100 ms\r\n" +
                "PRINT text    print text to log\r\n" +
                "CLEARLOG      clear log\r\n" +
                "# text        comment";

            syntax.SetBounds(20, 515, 430, 150);

            syntax.Font =
                new Font(
                    FontFamily.GenericMonospace,
                    9);

            Controls.Add(syntax);

            txtSequence.Multiline = true;
            txtSequence.ScrollBars = ScrollBars.Both;
            txtSequence.WordWrap = false;

            txtSequence.Font =
                new Font(
                    FontFamily.GenericMonospace,
                    9);

            txtSequence.SetBounds(
                470,
                500,
                400,
                165);

            txtSequence.Text =
                "# BME280 basic test\r\n" +
                "ADDR 76\r\n" +
                "\r\n" +
                "PRINT === CHIP ID ===\r\n" +
                "WR D0 1\r\n" +
                "\r\n" +
                "PRINT === CALIBRATION BLOCK 1 ===\r\n" +
                "WR 88 26\r\n" +
                "\r\n" +
                "PRINT === CALIBRATION BLOCK 2 ===\r\n" +
                "WR E1 7\r\n";

            Controls.Add(txtSequence);

            btnRunSequence.Text = "RUN";

            btnRunSequence.SetBounds(
                470,
                675,
                120,
                35);

            btnRunSequence.Click +=
                new EventHandler(BtnRunSequence_Click);

            Controls.Add(btnRunSequence);

            btnStopSequence.Text = "STOP";

            btnStopSequence.SetBounds(
                600,
                675,
                120,
                35);

            btnStopSequence.Enabled = false;

            btnStopSequence.Click +=
                new EventHandler(BtnStopSequence_Click);

            Controls.Add(btnStopSequence);

            // ========================================================
            // LOG
            // ========================================================

            Controls.Add(Header("BUS LOG", 20, 675));

            txtLog.SetBounds(
                20,
                705,
                430,
                90);

            txtLog.Multiline = true;
            txtLog.ScrollBars = ScrollBars.Vertical;
            txtLog.ReadOnly = true;

            txtLog.Font =
                new Font(
                    FontFamily.GenericMonospace,
                    9);

            Controls.Add(txtLog);

            btnClear.Text = "Clear Log";

            btnClear.SetBounds(
                750,
                675,
                120,
                35);

            btnClear.Click +=
                new EventHandler(BtnClear_Click);

            Controls.Add(btnClear);

            FormClosing +=
                new FormClosingEventHandler(
                    Form1_FormClosing);
        }

        // ============================================================
        // GUI EVENTS
        // ============================================================

        private void BtnStopSequence_Click(
            object sender,
            EventArgs e)
        {
            sequenceStopRequested = true;
        }

        private void BtnClear_Click(
            object sender,
            EventArgs e)
        {
            txtLog.Clear();
        }

        // ============================================================
        // GUI HELPERS
        // ============================================================

        private Label LabelText(
            string text,
            int x,
            int y)
        {
            Label l = new Label();

            l.Text = text;
            l.AutoSize = true;
            l.Location = new Point(x, y);

            return l;
        }

        private Label Header(
            string text,
            int x,
            int y)
        {
            Label l =
                LabelText(text, x, y);

            l.Font =
                new Font(
                    l.Font,
                    FontStyle.Bold);

            return l;
        }

        private void SetupLed(
            Label l,
            int x,
            int y)
        {
            l.Text = "*";

            l.Font =
                new Font(
                    "Arial",
                    18,
                    FontStyle.Bold);

            l.ForeColor = Color.DarkRed;

            l.SetBounds(
                x,
                y,
                30,
                30);
        }

        // ============================================================
        // OPEN / CLOSE FTDI
        // ============================================================

        private void BtnOpen_Click(
            object sender,
            EventArgs e)
        {
            if (opened)
            {
                CloseFTDI();
                return;
            }

            try
            {
                uint status =
                    FTDI.FT_Open(
                        0,
                        ref ftHandle);

                if (status != FTDI.FT_OK)
                {
                    throw new Exception(
                        "FT_Open failed: " +
                        status.ToString());
                }

                FTDI.FT_SetLatencyTimer(
                    ftHandle,
                    1);

                FTDI.FT_SetBaudRate(
                    ftHandle,
                    9600);

                directionMask = 0x00;

                status =
                    FTDI.FT_SetBitMode(
                        ftHandle,
                        directionMask,
                        0x01);

                if (status != FTDI.FT_OK)
                {
                    throw new Exception(
                        "FT_SetBitMode failed: " +
                        status.ToString());
                }

                opened = true;

                btnOpen.Text = "CLOSE";
                lblStatus.Text = "FTDI OPEN";

                ReleaseSCL();
                ReleaseSDA();

                Log("");
                Log("FTDI opened");
                Log("D2 / RTS = SCL");
                Log("D3 / CTS = SDA");

                UpdateBusLeds();
            }
            catch (DllNotFoundException)
            {
                MessageBox.Show(
                    "ftd2xx.dll not found.",
                    "FTDI");
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "FTDI");

                CloseFTDI();
            }
        }

        private void CloseFTDI()
        {
            sequenceStopRequested = true;

            if (ftHandle != IntPtr.Zero)
            {
                try
                {
                    directionMask = 0x00;

                    FTDI.FT_SetBitMode(
                        ftHandle,
                        directionMask,
                        0x01);

                    FTDI.FT_SetBitMode(
                        ftHandle,
                        0x00,
                        0x00);

                    FTDI.FT_Close(ftHandle);
                }
                catch
                {
                }
            }

            ftHandle = IntPtr.Zero;
            opened = false;

            btnOpen.Text = "OPEN FTDI #0";
            lblStatus.Text = "Closed";

            lblSCL.ForeColor = Color.DarkRed;
            lblSDA.ForeColor = Color.DarkRed;
        }

        // ============================================================
        // LOW LEVEL GPIO
        // ============================================================

        private void ApplyDirection()
        {
            uint status =
                FTDI.FT_SetBitMode(
                    ftHandle,
                    directionMask,
                    0x01);

            if (status != FTDI.FT_OK)
            {
                throw new Exception(
                    "FT_SetBitMode: " +
                    status.ToString());
            }

            uint written = 0;

            byte[] b = new byte[1];
            b[0] = outputValue;

            status =
                FTDI.FT_Write(
                    ftHandle,
                    b,
                    1,
                    ref written);

            if (status != FTDI.FT_OK)
            {
                throw new Exception(
                    "FT_Write: " +
                    status.ToString());
            }

            Thread.Sleep(delayMs);
        }

        private void SDA_Low()
        {
            directionMask =
                (byte)(directionMask | SDA);

            ApplyDirection();
        }

        private void ReleaseSDA()
        {
            directionMask =
                (byte)(directionMask & ~SDA);

            ApplyDirection();
        }

        private void SCL_Low()
        {
            directionMask =
                (byte)(directionMask | SCL);

            ApplyDirection();
        }

        private void ReleaseSCL()
        {
            directionMask =
                (byte)(directionMask & ~SCL);

            ApplyDirection();
        }

        private byte ReadPins()
        {
            byte value = 0;

            uint status =
                FTDI.FT_GetBitMode(
                    ftHandle,
                    ref value);

            if (status != FTDI.FT_OK)
            {
                throw new Exception(
                    "FT_GetBitMode: " +
                    status.ToString());
            }

            return value;
        }

        private bool ReadSDA()
        {
            return (ReadPins() & SDA) != 0;
        }

        private bool ReadSCL()
        {
            return (ReadPins() & SCL) != 0;
        }

        // ============================================================
        // I2C
        // ============================================================

        private void I2C_Start(
            bool repeated)
        {
            ReleaseSDA();
            ReleaseSCL();

            Thread.Sleep(delayMs);

            SDA_Low();

            Thread.Sleep(delayMs);

            SCL_Low();

            if (repeated)
                LogRaw("Sr ");
            else
                LogRaw("S ");
        }

        private void I2C_Start()
        {
            I2C_Start(false);
        }

        private void I2C_Stop()
        {
            SDA_Low();

            ReleaseSCL();

            Thread.Sleep(delayMs);

            ReleaseSDA();

            Thread.Sleep(delayMs);

            LogRaw("P\r\n");

            UpdateBusLeds();
        }

        private void WriteBit(bool bit)
        {
            SCL_Low();

            if (bit)
                ReleaseSDA();
            else
                SDA_Low();

            Thread.Sleep(delayMs);

            ReleaseSCL();

            Thread.Sleep(delayMs);

            SCL_Low();
        }

        private bool ReadBit()
        {
            SCL_Low();

            ReleaseSDA();

            Thread.Sleep(delayMs);

            ReleaseSCL();

            Thread.Sleep(delayMs);

            bool bit = ReadSDA();

            SCL_Low();

            return bit;
        }

        private bool WriteByte(byte value)
        {
            int bit;

            for (bit = 7; bit >= 0; bit--)
            {
                bool b =
                    ((value >> bit) & 1) != 0;

                WriteBit(b);
            }

            bool nack = ReadBit();
            bool ack = !nack;

            LogRaw(
                value.ToString("X2") +
                (ack ? " ACK " : " NACK "));

            return ack;
        }

        private byte ReadByte(bool ack)
        {
            byte value = 0;
            int bit;

            for (bit = 7; bit >= 0; bit--)
            {
                if (ReadBit())
                {
                    value =
                        (byte)(
                            value |
                            (byte)(1 << bit));
                }
            }

            WriteBit(!ack);

            LogRaw(
                value.ToString("X2") +
                (ack ? " ACK " : " NACK "));

            return value;
        }

        private bool AddressWrite(byte address)
        {
            return
                WriteByte(
                    (byte)(address << 1));
        }

        private bool AddressRead(byte address)
        {
            return
                WriteByte(
                    (byte)((address << 1) | 1));
        }

        // ============================================================
        // TRANSACTIONS
        // ============================================================

        private bool TransactionWrite(
            byte address,
            byte[] data)
        {
            int i;

            I2C_Start();

            if (!AddressWrite(address))
            {
                I2C_Stop();
                return false;
            }

            for (i = 0; i < data.Length; i++)
            {
                if (!WriteByte(data[i]))
                {
                    I2C_Stop();
                    return false;
                }
            }

            I2C_Stop();

            return true;
        }

        private byte[] TransactionRead(
            byte address,
            int count)
        {
            int i;

            I2C_Start();

            if (!AddressRead(address))
            {
                I2C_Stop();

                throw new Exception(
                    "Address READ NACK");
            }

            byte[] result =
                new byte[count];

            for (i = 0; i < count; i++)
            {
                bool ack =
                    i < count - 1;

                result[i] =
                    ReadByte(ack);
            }

            I2C_Stop();

            return result;
        }

        private byte[] TransactionCombined(
            byte address,
            byte[] writeData,
            int readCount)
        {
            int i;

            I2C_Start();

            if (!AddressWrite(address))
            {
                I2C_Stop();

                throw new Exception(
                    "Address WRITE NACK");
            }

            for (i = 0; i < writeData.Length; i++)
            {
                if (!WriteByte(writeData[i]))
                {
                    I2C_Stop();

                    throw new Exception(
                        "Write data NACK");
                }
            }

            I2C_Start(true);

            if (!AddressRead(address))
            {
                I2C_Stop();

                throw new Exception(
                    "Address READ NACK");
            }

            byte[] result =
                new byte[readCount];

            for (i = 0; i < readCount; i++)
            {
                bool ack =
                    i < readCount - 1;

                result[i] =
                    ReadByte(ack);
            }

            I2C_Stop();

            return result;
        }

        // ============================================================
        // SCANNER
        // ============================================================

        private void BtnScan_Click(
            object sender,
            EventArgs e)
        {
            if (!CheckOpen())
                return;

            try
            {
                Log("");
                Log("Scanning 0x08 - 0x77...");

                int found = 0;
                int a;

                for (a = 0x08; a <= 0x77; a++)
                {
                    byte address = (byte)a;

                    I2C_Start();

                    bool ack =
                        AddressWrite(address);

                    I2C_Stop();

                    if (ack)
                    {
                        Log(
                            "FOUND: 0x" +
                            address.ToString("X2"));

                        found++;
                    }

                    Application.DoEvents();
                }

                Log(
                    "Scan finished. Devices: " +
                    found.ToString());
            }
            catch (Exception ex)
            {
                Log(
                    "ERROR: " +
                    ex.Message);

                BusRecovery();
            }
        }

        // ============================================================
        // MANUAL WRITE
        // ============================================================

        private void BtnWrite_Click(
            object sender,
            EventArgs e)
        {
            if (!CheckOpen())
                return;

            try
            {
                byte address =
                    ParseHexByte(
                        txtAddress.Text);

                byte[] data =
                    ParseHex(
                        txtWrite.Text);

                Log("");

                Log(
                    "WRITE -> 0x" +
                    address.ToString("X2"));

                if (!TransactionWrite(address, data))
                    Log("NACK");
            }
            catch (Exception ex)
            {
                Log(
                    "ERROR: " +
                    ex.Message);

                BusRecovery();
            }
        }

        // ============================================================
        // MANUAL READ
        // ============================================================

        private void BtnRead_Click(
            object sender,
            EventArgs e)
        {
            if (!CheckOpen())
                return;

            try
            {
                byte address =
                    ParseHexByte(
                        txtAddress.Text);

                int count =
                    int.Parse(
                        txtReadCount.Text.Trim());

                ValidateReadCount(count);

                Log("");

                Log(
                    "READ <- 0x" +
                    address.ToString("X2"));

                byte[] result =
                    TransactionRead(
                        address,
                        count);

                Log(
                    "RESULT: " +
                    BytesToHex(result));
            }
            catch (Exception ex)
            {
                Log(
                    "ERROR: " +
                    ex.Message);

                BusRecovery();
            }
        }

        // ============================================================
        // MANUAL COMBINED
        // ============================================================

        private void BtnCombined_Click(
            object sender,
            EventArgs e)
        {
            if (!CheckOpen())
                return;

            try
            {
                byte address =
                    ParseHexByte(
                        txtAddress.Text);

                byte[] writeData =
                    ParseHex(
                        txtCombinedWrite.Text);

                int count =
                    int.Parse(
                        txtCombinedReadCount.Text.Trim());

                ValidateReadCount(count);

                Log("");

                Log(
                    "COMBINED -> 0x" +
                    address.ToString("X2"));

                byte[] result =
                    TransactionCombined(
                        address,
                        writeData,
                        count);

                Log(
                    "RESULT: " +
                    BytesToHex(result));
            }
            catch (Exception ex)
            {
                Log(
                    "ERROR: " +
                    ex.Message);

                BusRecovery();
            }
        }

        // ============================================================
        // SEQUENCE
        // ============================================================

        private void BtnRunSequence_Click(
            object sender,
            EventArgs e)
        {
            if (!CheckOpen())
                return;

            if (sequenceRunning)
                return;

            sequenceRunning = true;
            sequenceStopRequested = false;

            btnRunSequence.Enabled = false;
            btnStopSequence.Enabled = true;

            try
            {
                RunSequence(
                    txtSequence.Lines);
            }
            finally
            {
                sequenceRunning = false;

                btnRunSequence.Enabled = true;
                btnStopSequence.Enabled = false;
            }
        }

        private void RunSequence(
            string[] lines)
        {
            int lineIndex;

            Log("");
            Log("=== SEQUENCE START ===");

            for (
                lineIndex = 0;
                lineIndex < lines.Length;
                lineIndex++)
            {
                Application.DoEvents();

                if (sequenceStopRequested)
                {
                    Log(
                        "=== SEQUENCE STOPPED ===");

                    return;
                }

                string originalLine =
                    lines[lineIndex];

                string line =
                    originalLine.Trim();

                int humanLine =
                    lineIndex + 1;

                if (line.Length == 0)
                    continue;

                if (line.StartsWith("#"))
                    continue;

                try
                {
                    ExecuteSequenceLine(line);
                }
                catch (Exception ex)
                {
                    Log("");

                    Log(
                        "SEQUENCE ERROR at line " +
                        humanLine.ToString());

                    Log(
                        "> " +
                        originalLine);

                    Log(ex.Message);

                    BusRecovery();

                    Log(
                        "=== SEQUENCE ABORTED ===");

                    return;
                }
            }

            Log(
                "=== SEQUENCE FINISHED ===");
        }

        private void ExecuteSequenceLine(
            string line)
        {
            string[] firstSplit =
                line.Split(
                    new char[] { ' ', '\t' },
                    2,
                    StringSplitOptions.RemoveEmptyEntries);

            if (firstSplit.Length == 0)
                return;

            string command =
                firstSplit[0].ToUpperInvariant();

            string args = "";

            if (firstSplit.Length > 1)
                args = firstSplit[1].Trim();

            switch (command)
            {
                case "ADDR":
                    {
                        if (args.Length == 0)
                        {
                            throw new Exception(
                                "ADDR requires address.");
                        }

                        sequenceAddress =
                            ParseHexByte(args);

                        if (sequenceAddress > 0x7F)
                        {
                            throw new Exception(
                                "I2C address must be 7-bit.");
                        }

                        Log(
                            "> ADDR 0x" +
                            sequenceAddress.ToString("X2"));

                        break;
                    }

                case "W":
                    {
                        byte[] data =
                            ParseHex(args);

                        if (data.Length == 0)
                        {
                            throw new Exception(
                                "W requires at least one byte.");
                        }

                        Log(
                            "> W " +
                            BytesToHex(data));

                        bool ok =
                            TransactionWrite(
                                sequenceAddress,
                                data);

                        if (!ok)
                        {
                            throw new Exception(
                                "I2C NACK");
                        }

                        break;
                    }

                case "R":
                    {
                        int count =
                            int.Parse(args);

                        ValidateReadCount(count);

                        Log(
                            "> R " +
                            count.ToString());

                        byte[] result =
                            TransactionRead(
                                sequenceAddress,
                                count);

                        Log(
                            "  " +
                            BytesToHex(result));

                        break;
                    }

                case "WR":
                    {
                        string[] tokens =
                            args.Split(
                                new char[] { ' ', '\t' },
                                StringSplitOptions.RemoveEmptyEntries);

                        if (tokens.Length < 2)
                        {
                            throw new Exception(
                                "WR syntax: WR <write bytes> <read count>");
                        }

                        int readCount =
                            int.Parse(
                                tokens[tokens.Length - 1]);

                        ValidateReadCount(
                            readCount);

                        StringBuilder writeText =
                            new StringBuilder();

                        int i;

                        for (
                            i = 0;
                            i < tokens.Length - 1;
                            i++)
                        {
                            if (i > 0)
                                writeText.Append(' ');

                            writeText.Append(tokens[i]);
                        }

                        byte[] writeData =
                            ParseHex(
                                writeText.ToString());

                        Log(
                            "> WR " +
                            BytesToHex(writeData) +
                            " / " +
                            readCount.ToString());

                        byte[] result =
                            TransactionCombined(
                                sequenceAddress,
                                writeData,
                                readCount);

                        Log(
                            "  " +
                            BytesToHex(result));

                        break;
                    }

                case "DELAY":
                    {
                        int ms =
                            int.Parse(args);

                        if (ms < 0)
                        {
                            throw new Exception(
                                "DELAY cannot be negative.");
                        }

                        Log(
                            "> DELAY " +
                            ms.ToString() +
                            " ms");

                        DelayWithEvents(ms);

                        break;
                    }

                case "PRINT":
                    {
                        Log(args);
                        break;
                    }

                case "CLEARLOG":
                    {
                        txtLog.Clear();
                        break;
                    }

                default:
                    {
                        throw new Exception(
                            "Unknown command: " +
                            command);
                    }
            }
        }

        private void DelayWithEvents(
            int milliseconds)
        {
            int remaining =
                milliseconds;

            while (remaining > 0)
            {
                if (sequenceStopRequested)
                    return;

                int slice;

                if (remaining > 20)
                    slice = 20;
                else
                    slice = remaining;

                Thread.Sleep(slice);

                remaining -= slice;

                Application.DoEvents();
            }
        }

        // ============================================================
        // BUS RECOVERY
        // ============================================================

        private void BusRecovery()
        {
            try
            {
                int i;

                ReleaseSDA();

                for (i = 0; i < 9; i++)
                {
                    SCL_Low();
                    ReleaseSCL();
                }

                I2C_Stop();

                Log(
                    "Bus recovery executed.");
            }
            catch
            {
            }
        }

        // ============================================================
        // PARSING
        // ============================================================

        private byte ParseHexByte(
            string text)
        {
            string p =
                text.Trim();

            if (
                p.StartsWith(
                    "0x",
                    StringComparison.OrdinalIgnoreCase))
            {
                p = p.Substring(2);
            }

            return
                Convert.ToByte(
                    p,
                    16);
        }

        private byte[] ParseHex(
            string text)
        {
            string cleaned =
                text
                    .Replace(",", " ")
                    .Replace("-", " ")
                    .Replace(";", " ");

            string[] parts =
                cleaned.Split(
                    new char[]
                    {
                        ' ',
                        '\t',
                        '\r',
                        '\n'
                    },
                    StringSplitOptions.RemoveEmptyEntries);

            byte[] result =
                new byte[parts.Length];

            int i;

            for (
                i = 0;
                i < parts.Length;
                i++)
            {
                result[i] =
                    ParseHexByte(parts[i]);
            }

            return result;
        }

        private void ValidateReadCount(
            int count)
        {
            if (count < 1 || count > 256)
            {
                throw new Exception(
                    "Read count must be 1..256.");
            }
        }

        private string BytesToHex(
            byte[] data)
        {
            StringBuilder sb =
                new StringBuilder();

            int i;

            for (
                i = 0;
                i < data.Length;
                i++)
            {
                if (i > 0)
                    sb.Append(' ');

                sb.Append(
                    data[i].ToString("X2"));
            }

            return sb.ToString();
        }

        // ============================================================
        // GENERAL
        // ============================================================

        private bool CheckOpen()
        {
            if (opened)
                return true;

            MessageBox.Show(
                "Open FTDI first.");

            return false;
        }

        private void UpdateBusLeds()
        {
            if (!opened)
                return;

            try
            {
                if (ReadSCL())
                    lblSCL.ForeColor = Color.LimeGreen;
                else
                    lblSCL.ForeColor = Color.DarkRed;

                if (ReadSDA())
                    lblSDA.ForeColor = Color.LimeGreen;
                else
                    lblSDA.ForeColor = Color.DarkRed;
            }
            catch
            {
            }
        }

        private void Log(
            string text)
        {
            txtLog.AppendText(
                text +
                Environment.NewLine);

            txtLog.SelectionStart =
                txtLog.TextLength;

            txtLog.ScrollToCaret();
        }

        private void LogRaw(
            string text)
        {
            txtLog.AppendText(text);

            txtLog.SelectionStart =
                txtLog.TextLength;

            txtLog.ScrollToCaret();
        }

        // ============================================================
        // EXIT
        // ============================================================

        private void Form1_FormClosing(
            object sender,
            FormClosingEventArgs e)
        {
            sequenceStopRequested = true;
            CloseFTDI();
        }
    }
}
