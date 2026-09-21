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
                //labelMemVpp.IsEnabled = true;
                //labelApuVddio.IsEnabled = true;

                labelProcCaDs.IsEnabled = true;
                labelProcDqDs.IsEnabled = true;
                labelDramDqDs.IsEnabled = true;
                labelRttWrD5.IsEnabled = true;
                labelRttNomWr.IsEnabled = true;
                labelRttNomRd.IsEnabled = true;
                labelRttParkD5.IsEnabled = true;
                labelRttParkDqs.IsEnabled = true;

                //textBoxMemVddio.Text = Data.MemVddio.ToString();
                //textBoxMemVddq.Text = Data.MemVddq.ToString();
                //textBoxMemVpp.Text = Data.MemVpp.ToString();
                //textBoxApuVddio.Text = Data.ApuVddio.ToString();

                try
                {
                    if (family == Cpu.Family.FAMILY_1AH && Data?.ProcOdtPullUp != null)
                    {
                        labelProcODT.Visibility = Visibility.Collapsed;
                        textBoxProcODT.Visibility = Visibility.Collapsed;
                        procOdtDivider1.Visibility = Visibility.Collapsed;
                        procOdtDivider2.Visibility = Visibility.Collapsed;
                        labelProcOdtPullUp.Visibility = Visibility.Visible;
                        labelProcOdtPullUp.IsEnabled = true;
                        labelProcOdtPullDown.Visibility = Visibility.Visible;
                        labelProcOdtPullDown.IsEnabled = true;
                        textBoxProcOdtPullUp.Visibility = Visibility.Visible;
                        textBoxProcOdtPullDown.Visibility = Visibility.Visible;
                        textBoxProcOdtPullUp.Text = Data.ProcOdtPullUp.ToString();
                        textBoxProcOdtPullDown.Text = Data.ProcOdtPullDown.ToString();
                    }
                    else
                    {
                        labelProcODT.IsEnabled = true;
                        textBoxProcODT.Text = Data.ProcOdt.ToString();
                    }
                }
                catch { }

                textBoxCadBusDrvStren.Text = Data?.CadBusDrvStren?.ToString() ?? "N/A";
                textBoxDramDataDrvStren.Text = Data?.DramDataDrvStren?.ToString() ?? "N/A";
                textBoxProcDataDrvStren.Text = Data?.ProcDataDrvStren?.ToString() ?? "N/A";

                textBoxRttWrD5.Text = Data.RttWr.ToString();
                textBoxRttNomWr.Text = Data.RttNomWr.ToString();
                textBoxRttNomRd.Text = Data.RttNomRd.ToString();
                textBoxRttParkD5.Text = Data.RttPark.ToString();
                textBoxRttParkDqs.Text = Data.RttParkDqs.ToString();
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
