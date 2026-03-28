using System.Windows.Controls;
using ZenStates.Core;

namespace ZenTimings.Controls
{
    /// <summary>
    /// Interaction logic for LegacyDDR5APUTimingsPanel.xaml
    /// </summary>
    public partial class LegacyDDR5APUTimingsPanel : UserControl
    {
        public LegacyDDR5APUTimingsPanel()
        {
            InitializeComponent();

            Cpu cpu = CpuSingleton.Instance;
            AOD aod = cpu.info.aod;

            if (aod == null || Utils.AllZero(aod.Table.RawAodTable))
                return;

            AodData Data = aod.Table.Data;
            if (Data != null)
            {
                //labelMemVdd.IsEnabled = true;
                //labelMemVddq.IsEnabled = true;
                //labelMemVpp.IsEnabled = true;
                labelApuVddio.IsEnabled = true;

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
                textBoxApuVddio.Text = Data.ApuVddio.ToString();

                try
                {
                    labelProcCaOdt.IsEnabled = true;
                    labelProcCkOdt.IsEnabled = true;
                    labelProcDqOdt.IsEnabled = true;
                    labelProcDqsOdt.IsEnabled = true;
                    textBoxProcCaOdt.Text = Data?.ProcCaOdt?.ToString() ?? "N/A";
                    textBoxProcCkOdt.Text = Data?.ProcCkOdt?.ToString() ?? "N/A";
                    textBoxProcDqOdt.Text = Data?.ProcDqOdt?.ToString() ?? "N/A";
                    textBoxProcDqsOdt.Text = Data?.ProcDqsOdt?.ToString() ?? "N/A";
                }
                catch { }

                textBoxCadBusDrvStren.Text = Data?.CadBusDrvStren?.ToString() ?? "N/A";
                textBoxDramDataDrvStren.Text = Data?.DramDataDrvStren?.ToString() ?? "N/A";
                textBoxProcDataDrvStren.Text = Data?.ProcDataDrvStrenApu?.ToString() ?? "N/A";

                textBoxRttWrD5.Text = Data?.RttWr?.ToString() ?? "N/A";
                textBoxRttNomWr.Text = Data?.RttNomWr?.ToString() ?? "N/A";
                textBoxRttNomRd.Text = Data?.RttNomRd?.ToString() ?? "N/A";
                textBoxRttParkD5.Text = Data?.RttPark?.ToString() ?? "N/A";
                textBoxRttParkDqs.Text = Data?.RttParkDqs?.ToString() ?? "N/A";
            }
        }
    }
}
