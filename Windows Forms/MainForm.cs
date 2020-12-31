//#define BETA

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Management;
using System.Security.Permissions;
using System.Threading;
using System.Windows.Forms;
using ZenStates.Core;

namespace ZenTimings
{
    public partial class MainForm : Form
    {
        public const uint F17H_M01H_SVI = 0x0005A000;
        public const uint F17H_M02H_SVI = 0x0006F000; // Renoir only?
        public const uint F17H_M01H_SVI_TEL_PLANE0 = (F17H_M01H_SVI + 0xC);
        public const uint F17H_M01H_SVI_TEL_PLANE1 = (F17H_M01H_SVI + 0x10);
        public const uint F17H_M30H_SVI_TEL_PLANE0 = (F17H_M01H_SVI + 0x14);
        public const uint F17H_M30H_SVI_TEL_PLANE1 = (F17H_M01H_SVI + 0x10);
        public const uint F17H_M60H_SVI_TEL_PLANE0 = (F17H_M02H_SVI + 0x38);
        public const uint F17H_M60H_SVI_TEL_PLANE1 = (F17H_M02H_SVI + 0x3C);
        public const uint F17H_M70H_SVI_TEL_PLANE0 = (F17H_M01H_SVI + 0x10);
        public const uint F17H_M70H_SVI_TEL_PLANE1 = (F17H_M01H_SVI + 0xC);
        public const uint F19H_M21H_SVI_TEL_PLANE0 = (F17H_M01H_SVI + 0x10);
        public const uint F19H_M21H_SVI_TEL_PLANE1 = (F17H_M01H_SVI + 0xC);

        private readonly List<MemoryModule> modules = new List<MemoryModule>();
        private readonly List<BiosACPIFunction> biosFunctions = new List<BiosACPIFunction>();
        private readonly Cpu cpu = new Cpu();
        private readonly MemoryConfig MEMCFG = new MemoryConfig();
        private readonly BiosMemController BMC;
        private readonly PowerTable PowerTable;
        private readonly SystemInfo SI;
        private readonly uint dramBaseAddress = 0;
        private bool compatMode = false;
        private BackgroundWorker backgroundWorker1;
        private readonly AppSettings settings = new AppSettings();
        private readonly uint[] table = new uint[PowerTable.tableSize / 4];
#if DEBUG
        readonly TextWriterTraceListener[] listeners = new TextWriterTraceListener[] {
            //new TextWriterTraceListener("debug.txt")
        };
#endif

        private static void ExitApplication()
        {
            if (Application.MessageLoop)
                Application.Exit();
            else
                Environment.Exit(1);
        }

        private void FloatToVoltageString(object sender, ConvertEventArgs cevent)
        {
            // The method converts only to string type. Test this using the DesiredType.
            if (cevent.DesiredType != typeof(string)) return;

            float value = (float)cevent.Value;
            cevent.Value = value != 0 ? $"{(float)cevent.Value:F4}V" : "N/A";
        }

        private void FloatToFrequencyString(object sender, ConvertEventArgs cevent)
        {
            // The method converts only to string type. Test this using the DesiredType.
            if (cevent.DesiredType != typeof(string)) return;

            float value = (float)cevent.Value;
            cevent.Value = value != 0 ? $"{(float)cevent.Value:F2}" : "N/A";
        }

        private BiosACPIFunction GetFunctionByIdString(string name)
        {
            return biosFunctions.Find(x => x.IDString == name);
        }

        private void BindControls()
        {
            // First column
            textBoxFreq.DataBindings.Add("Text", MEMCFG, "Frequency");
            textBoxBGS.DataBindings.Add("Text", MEMCFG, "BGS");
            textBoxGDM.DataBindings.Add("Text", MEMCFG, "GDM");
            textBoxCL.DataBindings.Add("Text", MEMCFG, "CL");
            textBoxRCDWR.DataBindings.Add("Text", MEMCFG, "RCDWR");
            textBoxRCDRD.DataBindings.Add("Text", MEMCFG, "RCDRD");
            textBoxRP.DataBindings.Add("Text", MEMCFG, "RP");
            textBoxRAS.DataBindings.Add("Text", MEMCFG, "RAS");
            textBoxRC.DataBindings.Add("Text", MEMCFG, "RC");
            textBoxRRDS.DataBindings.Add("Text", MEMCFG, "RRDS");
            textBoxRRDL.DataBindings.Add("Text", MEMCFG, "RRDL");
            textBoxFAW.DataBindings.Add("Text", MEMCFG, "FAW");
            textBoxWTRS.DataBindings.Add("Text", MEMCFG, "WTRS");
            textBoxWTRL.DataBindings.Add("Text", MEMCFG, "WTRL");
            textBoxWR.DataBindings.Add("Text", MEMCFG, "WR");
            textBoxRFC.DataBindings.Add("Text", MEMCFG, "RFC");
            textBoxRFC2.DataBindings.Add("Text", MEMCFG, "RFC2");
            textBoxRFC4.DataBindings.Add("Text", MEMCFG, "RFC4");
            textBoxRFCns.DataBindings.Add("Text", MEMCFG, "RFCns");
            textBoxMOD.DataBindings.Add("Text", MEMCFG, "MOD");
            textBoxMODPDA.DataBindings.Add("Text", MEMCFG, "MODPDA");


            // Second column
            textBoxCapacity.DataBindings.Add("Text", MEMCFG, "TotalCapacity");
            textBoxBGSAlt.DataBindings.Add("Text", MEMCFG, "BGSAlt");
            textBoxCmd2T.DataBindings.Add("Text", MEMCFG, "Cmd2T");
            textBoxRDRDSCL.DataBindings.Add("Text", MEMCFG, "RDRDSCL");
            textBoxWRWRSCL.DataBindings.Add("Text", MEMCFG, "WRWRSCL");
            textBoxCWL.DataBindings.Add("Text", MEMCFG, "CWL");
            textBoxRTP.DataBindings.Add("Text", MEMCFG, "RTP");
            textBoxRDWR.DataBindings.Add("Text", MEMCFG, "RDWR");
            textBoxWRRD.DataBindings.Add("Text", MEMCFG, "WRRD");
            textBoxRDRDSC.DataBindings.Add("Text", MEMCFG, "RDRDSC");
            textBoxRDRDSD.DataBindings.Add("Text", MEMCFG, "RDRDSD");
            textBoxRDRDDD.DataBindings.Add("Text", MEMCFG, "RDRDDD");
            textBoxWRWRSC.DataBindings.Add("Text", MEMCFG, "WRWRSC");
            textBoxWRWRSD.DataBindings.Add("Text", MEMCFG, "WRWRSD");
            textBoxWRWRDD.DataBindings.Add("Text", MEMCFG, "WRWRDD");
            textBoxCKE.DataBindings.Add("Text", MEMCFG, "CKE");
            textBoxREFI.DataBindings.Add("Text", MEMCFG, "REFI");
            textBoxREFIns.DataBindings.Add("Text", MEMCFG, "REFIns");
            textBoxSTAG.DataBindings.Add("Text", MEMCFG, "STAG");
            textBoxMRD.DataBindings.Add("Text", MEMCFG, "MRD");
            textBoxMRDPDA.DataBindings.Add("Text", MEMCFG, "MRDPDA");
        }

        private void BindAdvancedControls()
        {
            if (settings.AdvancedMode)
            {
                Binding b;
                // Third column
                b = new Binding("Text", PowerTable, "MCLK");
                b.Format += new ConvertEventHandler(FloatToFrequencyString);
                textBoxMCLK.DataBindings.Add(b);

                b = new Binding("Text", PowerTable, "FCLK");
                b.Format += new ConvertEventHandler(FloatToFrequencyString);
                textBoxFCLK.DataBindings.Add(b);

                b = new Binding("Text", PowerTable, "UCLK");
                b.Format += new ConvertEventHandler(FloatToFrequencyString);
                textBoxUCLK.DataBindings.Add(b);

                b = new Binding("Text", PowerTable, "CLDO_VDDP");
                b.Format += new ConvertEventHandler(FloatToVoltageString);
                textBoxCLDO_VDDP.DataBindings.Add(b);

                b = new Binding("Text", PowerTable, "CLDO_VDDG_IOD");
                b.Format += new ConvertEventHandler(FloatToVoltageString);
                textBoxCLDO_VDDG_IOD.DataBindings.Add(b);

                b = new Binding("Text", PowerTable, "CLDO_VDDG_CCD");
                b.Format += new ConvertEventHandler(FloatToVoltageString);
                textBoxCLDO_VDDG_CCD.DataBindings.Add(b);
            }
        }

        private void ReadMemoryModulesInfo()
        {
            using (var searcher = new ManagementObjectSearcher("select * from Win32_PhysicalMemory"))
            {
                try
                {
                    foreach (var queryObject in searcher.Get().Cast<ManagementObject>())
                    {
                        ulong capacity = 0UL;
                        uint clockSpeed = 0U;
                        string partNumber = "N/A";
                        string bankLabel = "";
                        string manufacturer = "";
                        string deviceLocator = "";
                        object temp;

                        temp = WMI.TryGetProperty(queryObject, "Capacity");
                        if (temp != null) capacity = (ulong)temp;

                        temp = WMI.TryGetProperty(queryObject, "ConfiguredClockSpeed");
                        if (temp != null) clockSpeed = (uint)temp;

                        temp = WMI.TryGetProperty(queryObject, "partNumber");
                        if (temp != null) partNumber = (string)temp;

                        temp = WMI.TryGetProperty(queryObject, "BankLabel");
                        if (temp != null) bankLabel = (string)temp;

                        temp = WMI.TryGetProperty(queryObject, "Manufacturer");
                        if (temp != null) manufacturer = (string)temp;

                        temp = WMI.TryGetProperty(queryObject, "DeviceLocator");
                        if (temp != null) deviceLocator = (string)temp;

                        modules.Add(new MemoryModule(partNumber, bankLabel, manufacturer, deviceLocator, capacity, clockSpeed));

                        string bl = bankLabel.Length > 0 ? new string(bankLabel.Where(char.IsDigit).ToArray()) : "";
                        //string dl = deviceLocator.Length > 0 ? new string(deviceLocator.Where(char.IsDigit).ToArray()) : "";

                        comboBoxPartNumber.Items.Add($"#{bl}: {partNumber}");

                        comboBoxPartNumber.SelectedIndex = 0;
                    }

                    if (modules.Count > 0)
                    {
                        var totalCapacity = 0UL;

                        foreach (var module in modules)
                        {
                            totalCapacity += module.Capacity;
                        }

                        if (modules.FirstOrDefault().ClockSpeed != 0)
                            MEMCFG.Frequency = modules.FirstOrDefault().ClockSpeed;

                        if (totalCapacity != 0)
                            MEMCFG.TotalCapacity = $"{totalCapacity / 1024 / (1024 * 1024)}GB";
                    }
                }
                catch
                {
                    MessageBox.Show("Failed to get installed memory parameters. Corresponding fields will be empty.",
                        "Warning",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
            }
        }
        private void ReadPowerTable()
        {
            if (dramBaseAddress > 0)
            {
                for (int i = 0; i < table.Length; ++i)
                {
                    cpu.utils.GetPhysLong((UIntPtr)(dramBaseAddress + (i * 4)), out uint data);
                    table[i] = data;
                }

                if (table.Any(v => v != 0))
                {
                    PowerTable.ConfiguredClockSpeed = MEMCFG.Frequency;
                    PowerTable.Table = table;
                }
            }
        }

        private void ReadPowerConfig()
        {
#if DEBUG
            /*uint prev1 = 0;
            uint prev2 = 0;
            for (; ; )
            {
                uint cmd1 = 0, cmd2 = 0;
                uint arg1 = 0, arg2 = 0;
                uint rsp1 = 0, rsp2 = 0;

                ops.SmuReadReg(0x03B10564, ref arg1);
                ops.SmuReadReg(0x03B10528, ref cmd1);
                ops.SmuReadReg(0x03B10598, ref rsp1);
                if (cmd1 != prev1)
                {
                    prev1 = cmd1;
                    Console.WriteLine($"1 -> 0x{cmd1:X2}: 0x{arg1:X8}: 0x{rsp1:X8}");
                }

                ops.SmuReadReg(ops.Smu.SMU_ADDR_ARG, ref arg2);
                ops.SmuReadReg(ops.Smu.SMU_ADDR_MSG, ref cmd2);
                ops.SmuReadReg(ops.Smu.SMU_ADDR_RSP, ref rsp2);
                if (cmd2 != prev2)
                {
                    prev2 = cmd2;
                    Console.WriteLine($"2 -> 0x{cmd2:X2}: 0x{arg2:X8}: 0x{rsp2:X8}");
                }
            }*/
#endif
            if (dramBaseAddress > 0)
            {
                try
                {
                    SMU.Status status = cpu.TransferTableToDram();

                    //if (status != SMU.Status.OK)
                    //    status = cpu.TransferTableToDram(); // retry

                    if (status != SMU.Status.OK)
                        return;

                    ReadPowerTable();
                }
                catch (EntryPointNotFoundException ex)
                {
                    throw new ApplicationException(ex.Message);
                }
                catch (DllNotFoundException ex)
                {
                    throw new ApplicationException(ex.Message);
                }
            }
        }

        private void ReadSVI()
        {
            uint sviSocAddress, sviCoreaddress;
            // SVI2 interface
            switch (cpu.info.codeName/*si.ExtendedModel*/)
            {
                //case 0x1:  // Zen
                //case 0x8:  // Zen+
                //case 0x11: // Zen APU
                case Cpu.CodeName.SummitRidge:
                case Cpu.CodeName.PinnacleRidge:
                case Cpu.CodeName.RavenRidge:
                case Cpu.CodeName.Fenghuang:
                    sviCoreaddress = F17H_M01H_SVI_TEL_PLANE0;
                    sviSocAddress = F17H_M01H_SVI_TEL_PLANE1;
                    break;

                case Cpu.CodeName.Threadripper:
                case Cpu.CodeName.Naples:
                case Cpu.CodeName.Colfax:
                    sviCoreaddress = F17H_M01H_SVI_TEL_PLANE1;
                    sviSocAddress = F17H_M01H_SVI_TEL_PLANE0;
                    break;

                //case 0x31: // Zen2 Threadripper/EPYC
                case Cpu.CodeName.CastlePeak:
                case Cpu.CodeName.Rome:
                    sviCoreaddress = F17H_M30H_SVI_TEL_PLANE0;
                    sviSocAddress = F17H_M30H_SVI_TEL_PLANE1;
                    break;

                //case 0x18: // Zen+ APU
                //case 0x60: // Zen2 APU
                //case 0x71: // Zen2 Ryzen
                case Cpu.CodeName.Picasso:
                case Cpu.CodeName.Matisse:
                    sviCoreaddress = F17H_M70H_SVI_TEL_PLANE0;
                    sviSocAddress = F17H_M70H_SVI_TEL_PLANE1;
                    break;

                // Renoir
                case Cpu.CodeName.Renoir:
                    sviCoreaddress = F17H_M60H_SVI_TEL_PLANE0;
                    sviSocAddress = F17H_M60H_SVI_TEL_PLANE1;
                    break;

                case Cpu.CodeName.Vermeer:
                case Cpu.CodeName.Genesis:
                    sviCoreaddress = F19H_M21H_SVI_TEL_PLANE0;
                    sviSocAddress = F19H_M21H_SVI_TEL_PLANE1;
                    break;

                default:
                    sviCoreaddress = F17H_M01H_SVI_TEL_PLANE0;
                    sviSocAddress = F17H_M01H_SVI_TEL_PLANE1;
                    break;
            }

            ushort timeout = 20;
            uint plane1_value;
            do
                plane1_value = cpu.ReadDword(sviSocAddress);
            while ((plane1_value & 0xFF00) != 0 && --timeout > 0);

            if (timeout > 0)
            {
                uint vddcr_soc = (plane1_value >> 16) & 0xFF;
                textBoxVSOC.Text = $"{cpu.utils.VidToVoltage(vddcr_soc):F4}V";
            }
            //uint vcore = (ops.ReadDword(sviCoreaddress) >> 16) & 0xFF;
        }

        private void ReadMemoryConfig()
        {
            string scope = "root\\wmi";
            string className = "AMD_ACPI";

            try
            {
                string instanceName = WMI.GetInstanceName(scope, className);

                ManagementBaseObject pack;
                ManagementObject classInstance = new ManagementObject(scope,
                    $"{className}.InstanceName='{instanceName}'",
                    null);

                // Get possible values (index) of a memory option in BIOS
                /*pack = WMI.InvokeMethod(classInstance, "Getdvalues", "pack", "ID", 0x20007);
                if (pack != null)
                {
                    uint[] DValuesBuffer = (uint[])pack.GetPropertyValue("DValuesBuffer");
                    for (var i = 0; i < DValuesBuffer.Length; i++)
                    {
                        Debug.WriteLine("{0}", DValuesBuffer[i]);
                    }
                }*/


                // Get function names with their IDs
                string[] functionObjects = { "GetObjectID", "GetObjectID2" };
                foreach (var functionObject in functionObjects)
                {
                    try
                    {
                        pack = WMI.InvokeMethod(classInstance, functionObject, "pack", null, 0);
                        if (pack != null)
                        {
                            uint[] ID = (uint[])pack.GetPropertyValue("ID");
                            string[] IDString = (string[])pack.GetPropertyValue("IDString");
                            byte Length = (byte)pack.GetPropertyValue("Length");

                            for (var i = 0; i < Length; ++i)
                            {
                                biosFunctions.Add(new BiosACPIFunction(IDString[i], ID[i]));
                                Console.WriteLine("{0}: {1:X8}", IDString[i], ID[i]);
                            }
                        }
                    }
                    catch { }
                }

                // Get APCB config from BIOS. Holds memory parameters.
                BiosACPIFunction cmd = GetFunctionByIdString("Get APCB Config");
                if (cmd == null)
                    throw new Exception();

                byte[] apcbConfig = WMI.RunCommand(classInstance, cmd.ID);

                cmd = GetFunctionByIdString("Get memory voltages");
                if (cmd != null)
                {
                    byte[] voltages = WMI.RunCommand(classInstance, cmd.ID);

                    // MEM_VDDIO is ushort, offset 27
                    // MEM_VTT is ushort, offset 29
                    for (var i = 27; i <= 30; i++)
                    {
                        byte value = voltages[i];
                        if (value > 0)
                            apcbConfig[i] = value;
                    }
                }

                BMC.Table = apcbConfig;
                bool allZero = !BMC.Table.Any(v => v != 0);

                // When ProcODT is 0, then all other resistance values are 0
                // Happens when one DIMM installed in A1 or A2 slot
                if (allZero || BMC.Table == null || BMC.Config.ProcODT < 1)
                {
                    throw new Exception();
                }

                textBoxProcODT.Text = BMC.GetProcODTString(BMC.Config.ProcODT);

                textBoxClkDrvStren.Text = BMC.GetDrvStrenString(BMC.Config.ClkDrvStren);
                textBoxAddrCmdDrvStren.Text = BMC.GetDrvStrenString(BMC.Config.AddrCmdDrvStren);
                textBoxCsOdtCmdDrvStren.Text = BMC.GetDrvStrenString(BMC.Config.CsOdtCmdDrvStren);
                textBoxCkeDrvStren.Text = BMC.GetDrvStrenString(BMC.Config.CkeDrvStren);

                textBoxRttNom.Text = BMC.GetRttString(BMC.Config.RttNom);
                textBoxRttWr.Text = BMC.GetRttWrString(BMC.Config.RttWr);
                textBoxRttPark.Text = BMC.GetRttString(BMC.Config.RttPark);

                textBoxAddrCmdSetup.Text = $"{BMC.Config.AddrCmdSetup}";
                textBoxCsOdtSetup.Text = $"{BMC.Config.CsOdtSetup}";
                textBoxCkeSetup.Text = $"{BMC.Config.CkeSetup}";
            }
            catch (Exception ex)
            {
                compatMode = true;
                MessageBox.Show("Failed to read AMD ACPI. Some parameters will be empty.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                Console.WriteLine(ex.Message);
            }

            BMC.Dispose();
        }

        private void ReadTimings()
        {
            uint offset = 0;
            bool enabled = false;

            // Get the offset by probing the IMC0 to IMC7
            // It appears that offsets 0x80 and 0x84 are DIMM config registers
            // When a DIMM is DR, bit 0 is set to 1
            // 0x50000
            // offset 0, bit 0 when set to 1 means DIMM1 is installed
            // offset 8, bit 0 when set to 1 means DIMM2 is installed
            for (var i = 0; i < 8 && !enabled; i++)
            {
                offset = (uint)i << 20;
                bool channel = cpu.utils.GetBits(cpu.ReadDword(offset | 0x50DF0), 19, 1) == 0;
                bool dimm1 = cpu.utils.GetBits(cpu.ReadDword(offset | 0x50000), 0, 1) == 1;
                bool dimm2 = cpu.utils.GetBits(cpu.ReadDword(offset | 0x50008), 0, 1) == 1;
                enabled = channel && (dimm1 || dimm2);
            }

            // Reset here in case no enabled channel is detected
            // Try and read from the first IMC (IMC0);
            if (!enabled) offset = 0;

            uint powerDown = cpu.ReadDword(offset | 0x5012C);
            uint umcBase = cpu.ReadDword(offset | 0x50200);
            uint bgsa0 = cpu.ReadDword(offset | 0x500D0);
            uint bgsa1 = cpu.ReadDword(offset | 0x500D4);
            uint bgs0 = cpu.ReadDword(offset | 0x50050);
            uint bgs1 = cpu.ReadDword(offset | 0x50058);
            uint timings5 = cpu.ReadDword(offset | 0x50204);
            uint timings6 = cpu.ReadDword(offset | 0x50208);
            uint timings7 = cpu.ReadDword(offset | 0x5020C);
            uint timings8 = cpu.ReadDword(offset | 0x50210);
            uint timings9 = cpu.ReadDword(offset | 0x50214);
            uint timings10 = cpu.ReadDword(offset | 0x50218);
            uint timings11 = cpu.ReadDword(offset | 0x5021C);
            uint timings12 = cpu.ReadDword(offset | 0x50220);
            uint timings13 = cpu.ReadDword(offset | 0x50224);
            uint timings14 = cpu.ReadDword(offset | 0x50228);
            uint timings15 = cpu.ReadDword(offset | 0x50230);
            uint timings16 = cpu.ReadDword(offset | 0x50234);
            uint timings17 = cpu.ReadDword(offset | 0x50250);
            uint timings18 = cpu.ReadDword(offset | 0x50254);
            uint timings19 = cpu.ReadDword(offset | 0x50258);
            uint timings20 = cpu.ReadDword(offset | 0x50260);
            uint timings21 = cpu.ReadDword(offset | 0x50264);
            uint timings22 = cpu.ReadDword(offset | 0x5028C);
            uint timings23 = timings20 != timings21 ? (timings20 != 0x21060138 ? timings20 : timings21) : timings20;

            MEMCFG.BGS = (bgs0 == 0x87654321 && bgs1 == 0x87654321) ? "Disabled" : "Enabled";
            MEMCFG.BGSAlt = cpu.utils.GetBits(bgsa0, 4, 7) > 0 || cpu.utils.GetBits(bgsa1, 4, 7) > 0 ? "Enabled" : "Disabled";
            MEMCFG.GDM = cpu.utils.GetBits(umcBase, 11, 1) > 0 ? "Enabled" : "Disabled";
            MEMCFG.Cmd2T = cpu.utils.GetBits(umcBase, 10, 1) > 0 ? "2T" : "1T";

            MEMCFG.CL = cpu.utils.GetBits(timings5, 0, 6);
            MEMCFG.RAS = cpu.utils.GetBits(timings5, 8, 7);
            MEMCFG.RCDRD = cpu.utils.GetBits(timings5, 16, 6);
            MEMCFG.RCDWR = cpu.utils.GetBits(timings5, 24, 6);

            MEMCFG.RC = cpu.utils.GetBits(timings6, 0, 8);
            MEMCFG.RP = cpu.utils.GetBits(timings6, 16, 6);

            MEMCFG.RRDS = cpu.utils.GetBits(timings7, 0, 5);
            MEMCFG.RRDL = cpu.utils.GetBits(timings7, 8, 5);
            MEMCFG.RTP = cpu.utils.GetBits(timings7, 24, 5);

            MEMCFG.FAW = cpu.utils.GetBits(timings8, 0, 8);

            MEMCFG.CWL = cpu.utils.GetBits(timings9, 0, 6);
            MEMCFG.WTRS = cpu.utils.GetBits(timings9, 8, 5);
            MEMCFG.WTRL = cpu.utils.GetBits(timings9, 16, 7);

            MEMCFG.WR = cpu.utils.GetBits(timings10, 0, 8);

            MEMCFG.TRCPAGE = cpu.utils.GetBits(timings11, 20, 12);

            MEMCFG.RDRDDD = cpu.utils.GetBits(timings12, 0, 4);
            MEMCFG.RDRDSD = cpu.utils.GetBits(timings12, 8, 4);
            MEMCFG.RDRDSC = cpu.utils.GetBits(timings12, 16, 4);
            MEMCFG.RDRDSCL = cpu.utils.GetBits(timings12, 24, 6);

            MEMCFG.WRWRDD = cpu.utils.GetBits(timings13, 0, 4);
            MEMCFG.WRWRSD = cpu.utils.GetBits(timings13, 8, 4);
            MEMCFG.WRWRSC = cpu.utils.GetBits(timings13, 16, 4);
            MEMCFG.WRWRSCL = cpu.utils.GetBits(timings13, 24, 6);

            MEMCFG.RDWR = cpu.utils.GetBits(timings14, 8, 5);
            MEMCFG.WRRD = cpu.utils.GetBits(timings14, 0, 4);

            MEMCFG.REFI = cpu.utils.GetBits(timings15, 0, 16);

            MEMCFG.MODPDA = cpu.utils.GetBits(timings16, 24, 6);
            MEMCFG.MRDPDA = cpu.utils.GetBits(timings16, 16, 6);
            MEMCFG.MOD = cpu.utils.GetBits(timings16, 8, 6);
            MEMCFG.MRD = cpu.utils.GetBits(timings16, 0, 6);

            MEMCFG.STAG = cpu.utils.GetBits(timings17, 16, 8);

            MEMCFG.XP = cpu.utils.GetBits(timings18, 0, 6);
            MEMCFG.CKE = cpu.utils.GetBits(timings18, 24, 5);

            MEMCFG.PHYWRL = cpu.utils.GetBits(timings19, 8, 5);
            MEMCFG.PHYRDL = cpu.utils.GetBits(timings19, 16, 6);
            MEMCFG.PHYWRD = cpu.utils.GetBits(timings19, 24, 3);
            
            MEMCFG.RFC = cpu.utils.GetBits(timings23, 0, 11);
            MEMCFG.RFC2 = cpu.utils.GetBits(timings23, 11, 11);
            MEMCFG.RFC4 = cpu.utils.GetBits(timings23, 22, 11);

            MEMCFG.PowerDown = cpu.utils.GetBits(powerDown, 28, 1) == 1 ? "Enabled" : "Disabled";

            var configured = MEMCFG.Frequency;
            var freqFromRatio = cpu.utils.GetBits(umcBase, 0, 7) / 3.0f * 200;

            // Fallback to ratio when ConfiguredClockSpeed fails
            if (configured == 0 || freqFromRatio > configured)
            {
                MEMCFG.Frequency = freqFromRatio;
                //PowerTable.ConfiguredClockSpeed = freqFromRatio;
            }
        }

        private void SwitchToCompactMode()
        {
            tableLayoutPanelValues.Controls.Remove(labelUCLK);
            tableLayoutPanelValues.Controls.Remove(labelFCLK);
            tableLayoutPanelValues.Controls.Remove(textBoxUCLK);
            tableLayoutPanelValues.Controls.Remove(textBoxFCLK);
            tableLayoutPanelValues.Controls.Remove(labelMCLK);
            tableLayoutPanelValues.Controls.Remove(textBoxMCLK);
            tableLayoutPanelValues.Controls.Remove(labelCLDO_VDDP);
            tableLayoutPanelValues.Controls.Remove(labelCLDO_VDDG_IOD);
            tableLayoutPanelValues.Controls.Remove(labelCLDO_VDDG_CCD);
            tableLayoutPanelValues.Controls.Remove(textBoxCkeSetup);
            tableLayoutPanelValues.Controls.Remove(textBoxAddrCmdSetup);
            tableLayoutPanelValues.Controls.Remove(textBoxCsOdtSetup);
            tableLayoutPanelValues.Controls.Remove(textBoxCkeDrvStren);
            tableLayoutPanelValues.Controls.Remove(textBoxAddrCmdDrvStren);
            tableLayoutPanelValues.Controls.Remove(textBoxCsOdtCmdDrvStren);
            tableLayoutPanelValues.Controls.Remove(textBoxClkDrvStren);
            tableLayoutPanelValues.Controls.Remove(textBoxRttPark);
            tableLayoutPanelValues.Controls.Remove(textBoxRttWr);
            tableLayoutPanelValues.Controls.Remove(textBoxRttNom);
            tableLayoutPanelValues.Controls.Remove(textBoxProcODT);
            tableLayoutPanelValues.Controls.Remove(labelCkeSetup);
            tableLayoutPanelValues.Controls.Remove(labelAddrCmdSetup);
            tableLayoutPanelValues.Controls.Remove(labelCsOdtSetup);
            tableLayoutPanelValues.Controls.Remove(labelCkeDrvStren);
            tableLayoutPanelValues.Controls.Remove(labelAddrCmdDrvStren);
            tableLayoutPanelValues.Controls.Remove(labelCsOdtCmdDrvStren);
            tableLayoutPanelValues.Controls.Remove(labelClkDrvStren);
            tableLayoutPanelValues.Controls.Remove(labelRttPark);
            tableLayoutPanelValues.Controls.Remove(labelRttWr);
            tableLayoutPanelValues.Controls.Remove(labelRttNom);
            tableLayoutPanelValues.Controls.Remove(labelProcODT);
            tableLayoutPanelValues.Controls.Remove(textBoxCLDO_VDDP);
            tableLayoutPanelValues.Controls.Remove(textBoxCLDO_VDDG_IOD);
            tableLayoutPanelValues.Controls.Remove(textBoxCLDO_VDDG_CCD);
            tableLayoutPanelValues.Controls.Remove(textBoxVSOC);
            tableLayoutPanelValues.Controls.Remove(labelVSOC);
            Controls.Remove(dividerTop);

            tableLayoutPanelValues.ColumnStyles[7].SizeType = SizeType.Absolute;
            tableLayoutPanelValues.ColumnStyles[7].Width = 0;
            tableLayoutPanelValues.ColumnStyles[8].SizeType = SizeType.Absolute;
            tableLayoutPanelValues.ColumnStyles[8].Width = 0;

            buttonScreenshot.Location = new Point(240, buttonScreenshot.Location.Y);
            var h = tableLayoutPanel3.Height;
            Controls.Remove(tableLayoutPanel3);

            Height -= h;
            Width = 275;
        }

        private void ButtonScreenshot_Click(object sender, EventArgs e)
        {
            Screenshot screenshot = new Screenshot();
            Bitmap bitmap = screenshot.CaptureActiveWindow();

            using (Form saveForm = new SaveForm(bitmap))
            {
                saveForm.ShowDialog();
                screenshot.Dispose();
            }
        }

        private bool WaitForDriverLoad()
        {
            Stopwatch timer = new Stopwatch();
            timer.Start();

            bool temp;
            // Refresh until driver is opened
            do
                temp = cpu.utils.IsInpOutDriverOpen();
            while (!temp && timer.Elapsed.TotalMilliseconds < 10000);

            timer.Stop();

            return temp;
        }

        private bool WaitForPowerTable()
        {
            if (WaitForDriverLoad() && cpu.utils.WinIoStatus == Utils.LibStatus.OK)
            {
                Stopwatch timer = new Stopwatch();
                timer.Start();
                short timeout = 10000;

                uint temp;
                SMU.Status status;

                // Refresh until table is transferred to DRAM or timeout
                do
                {
                    status = cpu.TransferTableToDram();
                    cpu.utils.GetPhysLong((UIntPtr)dramBaseAddress, out temp);
                }
                while ((temp == 0 || status != SMU.Status.OK) && timer.Elapsed.TotalMilliseconds < timeout);

                timer.Stop();

                if (temp == 0)
                    HandleError("Could not get power table.\nClose the application and try again.");

                return temp != 0;
            }
            else
            {
                HandleError("Driver is not responding or not loaded.");
                return false;
            }
        }

        public static bool CheckConfigFileIsPresent()
        {
            return File.Exists(AppDomain.CurrentDomain.SetupInformation.ConfigurationFile);
        }
        
        private void StartAutoRefresh()
        {
            if (settings.AutoRefresh && settings.AdvancedMode)
            {
                PowerCfgTimer.Interval = settings.AutoRefreshInterval;
                PowerCfgTimer.Start();
            }
        }

        private void DisableInactiveControls()
        {
            foreach (Control ctrl in tableLayoutPanelValues.Controls)
            {
                if (ctrl.GetType() == typeof(Label) && ctrl.Text == "N/A")
                {
                    var prop = ctrl.Name.Replace("textBox", "");
                    ctrl.Enabled = false;
                    var label = tableLayoutPanelValues.Controls[$"label{prop}"];
                    if (label != null)
                        label.Enabled = false;
                }
            }
        }

        /// <summary>
        /// Gets triggered right before the form gets displayd
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MainForm_Load(object sender, EventArgs e)
        {
            Text = $"{Application.ProductName} {Application.ProductVersion.Substring(0, Application.ProductVersion.LastIndexOf('.'))} L";
#if BETA
            Text += " beta 4";
#endif
#if DEBUG
            Text += " (debug)";
#endif
            labelCPU.Text = SI.CpuName;
            labelMB.Text = $"{SI.MbName} | BIOS {SI.BiosVersion} | SMU {SI.GetSmuVersionString()}";

            if (compatMode)
                Text += " (compatibility)";
#if DEBUG
            foreach (TextWriterTraceListener listener in listeners)
            {
                listener.Flush();
            }
#endif
        }

        private void PowerCfgTimer_Tick(object sender, EventArgs e)
        {
            //ReadTimings();
            //ReadMemoryConfig();
            ReadSVI();
            ReadPowerConfig();
        }
        
        public MainForm()
        {
            try
            {

                if (cpu.info.family != Cpu.Family.FAMILY_17H && cpu.info.family != Cpu.Family.FAMILY_19H)
                {
                    HandleError("CPU is not supported.");
                    ExitApplication();
                }

                SI = new SystemInfo(cpu);

#if BETA
                MessageBox.Show("This is a BETA version of the application. Some functions might be working incorrectly.\n\n" +
                    "Please report if something is not working as expected.");
#endif
#if DEBUG
                Debug.Listeners.AddRange(listeners);
#endif
                InitializeComponent();
                BindControls();
                ReadMemoryModulesInfo();
                ReadTimings();

                if (settings.AdvancedMode)
                {
					// Get first base address
                    dramBaseAddress = (uint)(cpu.GetDramBaseAddress() & 0xFFFFFFFF);
                    PowerTable = new PowerTable(cpu.smu.SMU_TYPE);
                    BMC = new BiosMemController();
                    PowerCfgTimer.Interval = 2000;
                    PowerCfgTimer.Tick += new EventHandler(PowerCfgTimer_Tick);

                    BindAdvancedControls();
                    ReadMemoryConfig();
                    ReadSVI();

                    if (WaitForPowerTable())
                    {
                        ReadPowerTable();
                    } 

                    StartAutoRefresh();
                }
                else
                {
                	SwitchToCompactMode();
                }
            }
            catch (ApplicationException ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                // Dispose();
                ExitApplication();
            }
        }

        private void TableLayoutPanel1_CellPaint(object sender, TableLayoutCellPaintEventArgs e)
        {
            using (SolidBrush brush = new SolidBrush((Color)new ColorConverter().ConvertFrom("#eeeeee")))
            {
                if ((e.Row + 1) % 2 == 0)
                {
                    e.Graphics.FillRectangle(brush, e.CellBounds);
                }
            }
        }

        public void HandleError(string message, string title = "Error")
        {
            MessageBox.Show(message, title);
        }

        private void Restart()
        {
            settings.IsRestarting = true;
            settings.Save();
            Application.Restart();
        }

        private void ShowWindow()
        {
            Show();
            Activate();
            BringToFront();
            WindowState = FormWindowState.Normal;
        }

        private static void MinimizeFootprint()
        {
            InteropMethods.EmptyWorkingSet(Process.GetCurrentProcess().Handle);
        }

        private void MainForm_Shown(object sender, EventArgs e)
        {
            //SplashForm.CloseForm();
            ShowWindow();
            MinimizeFootprint();
        }

        private void MainForm_Resize(object sender, EventArgs e)
        {
            MinimizeFootprint();
        }

        [SecurityPermission(SecurityAction.LinkDemand, Flags = SecurityPermissionFlag.UnmanagedCode)]
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == InteropMethods.WM_SHOWME)
            {
                ShowWindow();
            }
            base.WndProc(ref m);
        }

        private void ExitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Dispose();
            ExitApplication();
        }

        private void AboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AboutBox aboutWnd = new AboutBox();
            aboutWnd.ShowDialog();
        }

        private void OptionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Form optionsWnd = new OptionsDialog(settings, PowerCfgTimer);
            optionsWnd.ShowDialog();
        }

        private void DebugToolstripItem_Click(object sender, EventArgs e)
        {
            if (settings.AdvancedMode)
            {
                Form debugWnd = new DebugDialog(dramBaseAddress, modules, MEMCFG, SI, BMC, PowerTable, cpu);
                debugWnd.ShowDialog();
            }
            else
            {
                DialogResult result = MessageBox.Show(
                    "Debug functionality requires Advanced Mode.\n\n" +
                    "Do you want to enable it now (the application will restart automatically)?",
                    "Debug Report",
                    MessageBoxButtons.YesNoCancel);

                if (result == DialogResult.Yes)
                {
                    settings.AdvancedMode = true;
                    Restart();
                }
            }
        }
    }
}
