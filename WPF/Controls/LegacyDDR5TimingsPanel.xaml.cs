using System.Windows;
using System.Windows.Controls;
using ZenStates.Core;
using ZenStates.Core.Hardware.Aod;
using ZenTimings.Common;

namespace ZenTimings.Controls
{
    /// <summary>
    /// Interaction logic for DDR5xaml
    /// </summary>
    public partial class LegacyDDR5TimingsPanel : UserControl
    {
        public LegacyDDR5TimingsPanel() : this(LiveAodData(), CpuSingleton.Instance.info.family)
        {
        }

        /// <param name="Data">AOD fields to show - a debug report's in a mock window. Null leaves them N/A.</param>
        /// <param name="family">CPU family, picks the single ProcODT or the pull-up/pull-down pair.</param>
        public LegacyDDR5TimingsPanel(AodData Data, Cpu.Family family)
        {
            InitializeComponent();

            if (Data != null)
            {
                //labelMemVdd.IsEnabled = true;
                //labelMemVddq.IsEnabled = true;
                //rowMemVpp.IsEnabled = true;
                //rowApuVddio.IsEnabled = true;

                rowProcCaDs.IsEnabled = true;
                rowProcDqDs.IsEnabled = true;
                rowDramDqDs.IsEnabled = true;
                rowRttWrD5.IsEnabled = true;
                rowRttNomWr.IsEnabled = true;
                rowRttNomRd.IsEnabled = true;
                rowRttParkD5.IsEnabled = true;
                rowRttParkDqs.IsEnabled = true;

                //textBoxMemVddio.Text = Data.MemVddio.ToString();
                //textBoxMemVddq.Text = Data.MemVddq.ToString();
                //textBoxMemVpp.Text = Data.MemVpp.ToString();
                //rowApuVddio.Value = Data.ApuVddio.ToString();

                try
                {
                    if (family == Cpu.Family.FAMILY_1AH && Data?.ProcOdtPullUp != null)
                    {
                        rowProcODT.Visibility = Visibility.Collapsed;
                        procOdtDivider1.Visibility = Visibility.Collapsed;
                        procOdtDivider1.Visibility = Visibility.Collapsed;
                        rowProcOdtPullUp.Visibility = Visibility.Visible;
                        rowProcOdtPullUp.IsEnabled = true;
                        rowProcOdtPullDown.Visibility = Visibility.Visible;
                        rowProcOdtPullDown.IsEnabled = true;
                        rowProcOdtPullUp.Value = Data.ProcOdtPullUp.ToString();
                        rowProcOdtPullDown.Value = Data.ProcOdtPullDown.ToString();
                    }
                    else
                    {
                        rowProcODT.IsEnabled = true;
                        rowProcODT.Value = Data.ProcOdt.ToString();
                    }
                }
                catch { }

                rowProcCaDs.Value = Data?.CadBusDrvStren?.ToString() ?? "N/A";
                rowDramDqDs.Value = Data?.DramDataDrvStren?.ToString() ?? "N/A";
                rowProcDqDs.Value = Data?.ProcDataDrvStren?.ToString() ?? "N/A";

                rowRttWrD5.Value = Data.RttWr.ToString();
                rowRttNomWr.Value = Data.RttNomWr.ToString();
                rowRttNomRd.Value = Data.RttNomRd.ToString();
                rowRttParkD5.Value = Data.RttPark.ToString();
                rowRttParkDqs.Value = Data.RttParkDqs.ToString();
            }
        }

        // The live machine's AOD fields, or null when its AOD table is missing or blank.
        internal static AodData LiveAodData()
        {
            AOD aod = CpuSingleton.Instance.info.aod;

            if (aod == null || ZenStates.Core.Utils.AllZero(aod.Table.RawAodTable))
                return null;

            return aod.Table.Data;
        }
    }
}
