using System.Windows.Controls;
using ZenStates.Core;
using ZenStates.Core.Hardware.Aod;

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
                //labelApuVddio.IsEnabled = true;

                labelProcCaDs.IsEnabled = Data?.CadBusDrvStren != null && !string.Equals(Data?.CadBusDrvStren?.ToString(), "N/A");
                labelProcDqDs.IsEnabled = Data?.ProcDataDrvStrenApu != null && !string.Equals(Data?.ProcDataDrvStrenApu?.ToString(), "N/A");
                labelDramDqDs.IsEnabled = Data?.DramDataDrvStren != null && !string.Equals(Data?.DramDataDrvStren?.ToString(), "N/A");

                labelRttWrD5.IsEnabled = Data?.RttWr != null && !string.Equals(Data?.RttWr?.ToString(), "N/A");
                labelRttNomWr.IsEnabled = Data?.RttNomWr != null && !string.Equals(Data?.RttNomWr?.ToString(), "N/A");
                labelRttNomRd.IsEnabled = Data?.RttNomRd != null && !string.Equals(Data?.RttNomRd?.ToString(), "N/A");
                labelRttParkD5.IsEnabled = Data?.RttPark != null && !string.Equals(Data?.RttPark?.ToString(), "N/A");
                labelRttParkDqs.IsEnabled = Data?.RttParkDqs != null && !string.Equals(Data?.RttParkDqs?.ToString(), "N/A");

                //textBoxMemVddio.Text = Data.MemVddio.ToString();
                //textBoxMemVddq.Text = Data.MemVddq.ToString();
                //textBoxMemVpp.Text = Data.MemVpp.ToString();
                //textBoxApuVddio.Text = Data.ApuVddio.ToString();

                try
                {
                    labelProcCaOdt.IsEnabled = Data?.ProcCaOdt != null && !string.Equals(Data?.ProcCaOdt?.ToString(), "N/A");
                    labelProcCkOdt.IsEnabled = Data?.ProcCkOdt != null && !string.Equals(Data?.ProcCkOdt?.ToString(), "N/A");
                    labelProcDqOdt.IsEnabled = Data?.ProcDqOdt != null && !string.Equals(Data?.ProcDqOdt?.ToString(), "N/A");
                    labelProcDqsOdt.IsEnabled = Data?.ProcDqsOdt != null && !string.Equals(Data?.ProcDqsOdt?.ToString(), "N/A");
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
