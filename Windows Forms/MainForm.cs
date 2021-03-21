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
using System.Windows.Threading;
using ZenStates.Core;

namespace ZenTimings
{
    public partial class MainForm : Form
    {
        private readonly List<MemoryModule> modules = new List<MemoryModule>();
        private readonly List<BiosACPIFunction> biosFunctions = new List<BiosACPIFunction>();
        private readonly Cpu cpu = new Cpu();
        private readonly MemoryConfig MEMCFG = new MemoryConfig();
        private readonly BiosMemController BMC;
        private readonly AppSettings settings = new AppSettings();
        private bool compatMode = false;
        private readonly AsusWMI AsusWmi = new AsusWMI();

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
                b = new Binding("Text", cpu.powerTable, "MCLK");
                b.Format += new ConvertEventHandler(FloatToFrequencyString);
                textBoxMCLK.DataBindings.Add(b);

                b = new Binding("Text", cpu.powerTable, "FCLK");
                b.Format += new ConvertEventHandler(FloatToFrequencyString);
                textBoxFCLK.DataBindings.Add(b);

                b = new Binding("Text", cpu.powerTable, "UCLK");
                b.Format += new ConvertEventHandler(FloatToFrequencyString);
                textBoxUCLK.DataBindings.Add(b);

                b = new Binding("Text", cpu.powerTable, "CLDO_VDDP");
                b.Format += new ConvertEventHandler(FloatToVoltageString);
                textBoxCLDO_VDDP.DataBindings.Add(b);

                b = new Binding("Text", cpu.powerTable, "CLDO_VDDG_IOD");
                b.Format += new ConvertEventHandler(FloatToVoltageString);
                textBoxCLDO_VDDG_IOD.DataBindings.Add(b);

                b = new Binding("Text", cpu.powerTable, "CLDO_VDDG_CCD");
                b.Format += new ConvertEventHandler(FloatToVoltageString);
                textBoxCLDO_VDDG_CCD.DataBindings.Add(b);
            }
        }

        private void ReadChannelsInfo()
        {
            int dimmIndex = 0;

            // Get the offset by probing the IMC0 to IMC7
            // It appears that offsets 0x80 and 0x84 are DIMM config registers
            // When a DIMM is DR, bit 0 is set to 1
            // 0x50000
            // offset 0, bit 0 when set to 1 means DIMM1 is installed
            // offset 8, bit 0 when set to 1 means DIMM2 is installed
            for (var i = 0; i < 8; i++)
            {
                uint channelOffset = (uint)i << 20;
                bool channel = cpu.utils.GetBits(cpu.ReadDword(channelOffset | 0x50DF0), 19, 1) == 0;
                bool dimm1 = cpu.utils.GetBits(cpu.ReadDword(channelOffset | 0x50000), 0, 1) == 1;
                bool dimm2 = cpu.utils.GetBits(cpu.ReadDword(channelOffset | 0x50008), 0, 1) == 1;

                if (channel && (dimm1 || dimm2))
                {
                    if (dimm1)
                    {
                        MemoryModule module = modules[dimmIndex++];
                        module.Slot = $"{Convert.ToChar(i + 65)}1";
                        module.DctOffset = channelOffset;
                        module.DualRank = cpu.utils.GetBits(cpu.ReadDword(channelOffset | 0x50080), 0, 1) == 1;
                    }

                    if (dimm2)
                    {
                        MemoryModule module = modules[dimmIndex++];
                        module.Slot = $"{Convert.ToChar(i + 65)}2";
                        module.DctOffset = channelOffset;
                        module.DualRank = cpu.utils.GetBits(cpu.ReadDword(channelOffset | 0x50084), 0, 1) == 1;
                    }
                }
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

                        modules.Add(new MemoryModule(partNumber.Trim(), bankLabel.Trim(), manufacturer.Trim(), deviceLocator, capacity, clockSpeed));

                        //string bl = bankLabel.Length > 0 ? new string(bankLabel.Where(char.IsDigit).ToArray()) : "";
                        //string dl = deviceLocator.Length > 0 ? new string(deviceLocator.Where(char.IsDigit).ToArray()) : "";

                        //comboBoxPartNumber.Items.Add($"#{bl}: {partNumber}");
                        //comboBoxPartNumber.SelectedIndex = 0;
                    }

                    ReadChannelsInfo();

                    if (modules.Count > 0)
                    {
                        ulong totalCapacity = 0UL;

                        foreach (var module in modules)
                        {
                            string rank = module.DualRank ? "DR" : "SR";
                            totalCapacity += module.Capacity;
                            comboBoxPartNumber.Items.Add($"{module.Slot}: {module.PartNumber} ({module.Capacity / 1024 / (1024 * 1024)}GB, {rank})");
                        }

                        if (modules.FirstOrDefault().ClockSpeed != 0)
                            MEMCFG.Frequency = modules.FirstOrDefault().ClockSpeed;

                        if (totalCapacity != 0)
                            MEMCFG.TotalCapacity = $"{totalCapacity / 1024 / (1024 * 1024)}GB";

                        comboBoxPartNumber.SelectedIndex = 0;
                        comboBoxPartNumber.SelectedIndexChanged += new EventHandler(ComboBoxPartNumber_SelectionChanged);
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

        private bool RefreshPowerTable()
        {
            return cpu.RefreshPowerTable() == SMU.Status.OK;
        }

        private void ReadSVI()
        {
            ushort timeout = 20;
            uint plane1_value;
            do
                plane1_value = cpu.ReadDword(cpu.info.SVI2.SocAddress);
            while ((plane1_value & 0xFF00) != 0 && --timeout > 0);

            if (timeout > 0)
            {
                uint vddcr_soc = (plane1_value >> 16) & 0xFF;
                textBoxVSOC.Text = $"{cpu.utils.VidToVoltage(vddcr_soc):F4}V";
            }
            //uint vcore = (ops.ReadDword(cpu.info.SVI2.CoreAddress) >> 16) & 0xFF;
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

                MessageBox.Show(
                    "Failed to read AMD ACPI. Some parameters will be empty.",
                    "Warning",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                Console.WriteLine(ex.Message);
            }

            BMC.Dispose();
        }

        private void ReadTimings(uint offset = 0)
        {
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

            float configured = MEMCFG.Frequency;
            float ratio = cpu.utils.GetBits(umcBase, 0, 7) / 3.0f;
            float freqFromRatio = ratio * 200;

            MEMCFG.Ratio = ratio;

            // Fallback to ratio when ConfiguredClockSpeed fails
            if (configured == 0.0f || freqFromRatio > configured)
            {
                MEMCFG.Frequency = freqFromRatio;
            }

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
            if (cpu.powerTable.dramBaseAddress == 0)
            {
                HandleError("Could not initialize power table.\nClose the application and try again.");
                return false;
            }

            if (WaitForDriverLoad() && cpu.utils.WinIoStatus == Utils.LibStatus.OK)
            {
                cpu.powerTable.ConfiguredClockSpeed = MEMCFG.Frequency;
                cpu.powerTable.MemRatio = MEMCFG.Ratio;

                SMU.Status status = cpu.RefreshPowerTable();
                uint temp = 0;
                Stopwatch timer = new Stopwatch();
                timer.Start();
                short timeout = 10000;

                // Refresh until table is transferred to DRAM or timeout
                do
                {
                    // if refresh failed, try again
                    if (status != SMU.Status.OK)
                        status = cpu.RefreshPowerTable();
                    else
                        temp = cpu.powerTable.Table[0];
                }
                while ((temp == 0 || status != SMU.Status.OK) && timer.Elapsed.TotalMilliseconds < timeout);

                timer.Stop();

                if (temp == 0 || status != SMU.Status.OK)
                {
                    HandleError("Could not get power table.\nSkipping power table.");
                    return false;
                }

                return true;
            }
            else
            {
                HandleError("I/O driver is not responding or not loaded.");
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

        private void PowerCfgTimer_Tick(object sender, EventArgs e)
        {
			// Run refresh operation in a new thread
            new Thread(() => 
            {
                Thread.CurrentThread.IsBackground = true;

	            //ReadTimings();
	            //ReadMemoryConfig();
                RefreshPowerTable();
                Invoke(new MethodInvoker(delegate { ReadSVI(); }));
            }).Start();
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
            labelCPU.Text = cpu.systemInfo.CpuName;
            labelMB.Text = $"{cpu.systemInfo.MbName} | BIOS {cpu.systemInfo.BiosVersion} | SMU {cpu.systemInfo.GetSmuVersionString()}";

            if (compatMode)
                Text += " (compatibility)";
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
                else if (cpu.info.codeName == Cpu.CodeName.Unsupported)
                {
                    HandleError("CPU model is not supported.\n" +
                        "Please run a debug report and send to the developer.");
                }

#if BETA
                MessageBox.Show("This is a BETA version of the application. Some functions might be working incorrectly.\n\n" +
                    "Please report if something is not working as expected.");
#endif
                InitializeComponent();
                BindControls();
                ReadMemoryModulesInfo();
                // Read from first enabled DCT
                if (modules.Count > 0)
                    ReadTimings(modules[0].DctOffset);


                if (settings.AdvancedMode)
                {
                    if (cpu.info.codeName != Cpu.CodeName.Unsupported)
                    {
	                    PowerCfgTimer.Interval = 2000;
	                    PowerCfgTimer.Tick += new EventHandler(PowerCfgTimer_Tick);

	                    ReadSVI();
	
	
	                    if (WaitForPowerTable())
	                    {
							// refresh the table again, to avoid displaying initial fclk, mclk and uclk values,
	                        // which seem to be a little off when transferring the table for the "first" time,
	                        // after an idle period
	                        RefreshPowerTable();
	                    } 
                        else
                        {
	
                            HandleError("Power table error!");
                        }
                        
                        if (!AsusWmi.Init())
                        {
                            AsusWmi.Dispose();
                            AsusWmi = null;
                        }
	
	                    StartAutoRefresh();
                    }

                    BMC = new BiosMemController();
                    BindAdvancedControls();
                    ReadMemoryConfig();
                }
                else
                {
                	SwitchToCompactMode();
                }
            }
            catch (ApplicationException ex)
            {
                HandleError(ex.Message);
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
            MessageBox.Show(
                message,
                title,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
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
                Form debugWnd = new DebugDialog(cpu, modules, MEMCFG, BMC, AsusWmi);
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
        
        private void ComboBoxPartNumber_SelectionChanged(object sender, EventArgs e)
        {
            ComboBox combo = sender as ComboBox;
            ReadTimings(modules[combo.SelectedIndex].DctOffset);
        }
    }
}
