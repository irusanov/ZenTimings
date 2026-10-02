using System;
using System.Collections.Generic;
using System.Windows.Controls;
using ZenStates.Core.Hardware.Apob;
using ZenTimings.Common;

namespace ZenTimings.Controls
{
    /// <summary>
    /// LPDDR5 timings (Rembrandt): the LPDDR5 timings (WCK, per bank precharge and refresh, mode register access)
    /// instead of the DDR5 ones, and the DRAM ODT / drive strength / Vrefs from the mode registers the ABL leaves in
    /// the APOB. Rembrandt has no AOD table and the DDR5 ODT / RTT settings do not apply to LPDDR5; the LPDDR5X APUs
    /// keep the AOD based LegacyDDR5APUTimingsPanel (MainWindow.UseLpddr5Panel).
    /// </summary>
    public partial class LPDDR5TimingsPanel : UserControl
    {
        public LPDDR5TimingsPanel() : this(CpuSingleton.Instance?.info.apob?.ActiveLpddr5ModeRegisters)
        {
        }

        /// <param name="modeRegisters">
        /// The LPDDR5 mode registers of the active APOB timing block - a debug report's in a mock window. Null leaves
        /// the rows N/A.
        /// </param>
        public LPDDR5TimingsPanel(ApobLpddr5ModeRegisters modeRegisters)
        {
            InitializeComponent();

            if (modeRegisters == null)
                return;

            SetRow(rowDqOdt, modeRegisters.DqOdt.ToString());
            SetRow(rowNtOdt, modeRegisters.NtDqOdt.ToString());
            SetRow(rowCaOdt, modeRegisters.CaOdtDisabled ? "Off" : modeRegisters.CaOdt.ToString());
            SetRow(rowWckOdt, modeRegisters.WckOdt.ToString());
            SetRow(rowCkCsOdt, Setting(modeRegisters, "CK/CS ODT"));
            SetRow(rowSocOdt, modeRegisters.SocOdt.ToString());
            SetRow(rowPdds, modeRegisters.Pdds.ToString());
            SetRow(rowVrefCa, Setting(modeRegisters, "VrefCA"));
            SetRow(rowVrefDq, Setting(modeRegisters, "VrefDQ"));
        }

        // The decoded setting whose name starts with the prefix, with the raw value ("38.0% (0x38)")
        private static string Setting(ApobLpddr5ModeRegisters registers, string prefix)
        {
            foreach (KeyValuePair<string, string> setting in registers.GetSettings())
            {
                if (setting.Key.StartsWith(prefix, StringComparison.Ordinal))
                    return setting.Value;
            }
            return null;
        }

        // Enabled with the value when there is one, otherwise left N/A and disabled
        private static void SetRow(TimingRow row, string value)
        {
            bool known = !string.IsNullOrEmpty(value) && !string.Equals(value, "N/A");
            row.Value = known ? value : "N/A";
            row.IsEnabled = known;
        }
    }
}
