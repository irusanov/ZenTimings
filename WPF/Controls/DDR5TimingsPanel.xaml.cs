using System.Windows;
using System.Windows.Controls;
using ZenStates.Core;

namespace ZenTimings.Controls
{
    /// <summary>
    /// Interaction logic for DDR5xaml
    /// </summary>
    public partial class DDR5TimingsPanel : UserControl
    {
        public DDR5TimingsPanel()
        {
            InitializeComponent();

            Cpu cpu = CpuSingleton.Instance;
            AOD aod = cpu.info.aod;

            if (aod == null || Utils.AllZero(aod.Table.RawAodTable))
                return;

            AodData Data = aod.Table.Data;
            if (Data != null)
            {
                labelMemVdd.IsEnabled = true;
                labelMemVddq.IsEnabled = true;
                labelMemVpp.IsEnabled = true;
                labelApuVddio.IsEnabled = true;

                labelProcCaDs.IsEnabled = true;
                labelProcDqDs.IsEnabled = true;
                labelDramDqDs.IsEnabled = true;
                labelRttWrD5.IsEnabled = true;
                labelRttNomWr.IsEnabled = true;
                labelRttNomRd.IsEnabled = true;
                labelRttParkD5.IsEnabled = true;
                labelRttParkDqs.IsEnabled = true;

                textBoxMemVddio.Text = Data.MemVddio.ToString();
                textBoxMemVddq.Text = Data.MemVddq.ToString();
                textBoxMemVpp.Text = Data.MemVpp.ToString();
                textBoxApuVddio.Text = Data.ApuVddio.ToString();

                try
                {
                    Cpu.CodeName codeName = cpu.info.codeName;

                    if (cpu.info.family == Cpu.Family.FAMILY_1AH && Data?.ProcOdtPullUp != null)
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

                textBoxCadBusDrvStren.Text = Data.CadBusDrvStren.ToString();
                textBoxDramDataDrvStren.Text = Data.DramDataDrvStren.ToString();
                textBoxProcDataDrvStren.Text = Data.ProcDataDrvStren.ToString();

                textBoxRttWrD5.Text = Data.RttWr.ToString();
                textBoxRttNomWr.Text = Data.RttNomWr.ToString();
                textBoxRttNomRd.Text = Data.RttNomRd.ToString();
                textBoxRttParkD5.Text = Data.RttPark.ToString();
                textBoxRttParkDqs.Text = Data.RttParkDqs.ToString();
            }
        }
    }
}
