using System.Windows.Controls;
using ZenStates.Core.Hardware.Aod;

namespace ZenTimings.Controls
{
    /// <summary>
    /// Interaction logic for LegacyDDR5APUTimingsPanel.xaml
    /// </summary>
    public partial class LegacyDDR5APUTimingsPanel : UserControl
    {
        public LegacyDDR5APUTimingsPanel() : this(LegacyDDR5TimingsPanel.LiveAodData())
        {
        }

        /// <param name="Data">AOD fields to show - a debug report's in a mock window. Null leaves them N/A.</param>
        public LegacyDDR5APUTimingsPanel(AodData Data)
        {
            InitializeComponent();

            if (Data != null)
            {
                //labelMemVdd.IsEnabled = true;
                //labelMemVddq.IsEnabled = true;
                //labelMemVpp.IsEnabled = true;
                //rowApuVddio.IsEnabled = true;

                rowProcCaDs.IsEnabled = Data?.CadBusDrvStren != null && !string.Equals(Data?.CadBusDrvStren?.ToString(), "N/A");
                rowProcDqDs.IsEnabled = Data?.ProcDataDrvStrenApu != null && !string.Equals(Data?.ProcDataDrvStrenApu?.ToString(), "N/A");
                rowDramDqDs.IsEnabled = Data?.DramDataDrvStren != null && !string.Equals(Data?.DramDataDrvStren?.ToString(), "N/A");

                rowRttWrD5.IsEnabled = Data?.RttWr != null && !string.Equals(Data?.RttWr?.ToString(), "N/A");
                rowRttNomWr.IsEnabled = Data?.RttNomWr != null && !string.Equals(Data?.RttNomWr?.ToString(), "N/A");
                rowRttNomRd.IsEnabled = Data?.RttNomRd != null && !string.Equals(Data?.RttNomRd?.ToString(), "N/A");
                rowRttParkD5.IsEnabled = Data?.RttPark != null && !string.Equals(Data?.RttPark?.ToString(), "N/A");
                rowRttParkDqs.IsEnabled = Data?.RttParkDqs != null && !string.Equals(Data?.RttParkDqs?.ToString(), "N/A");

                //textBoxMemVddio.Text = Data.MemVddio.ToString();
                //textBoxMemVddq.Text = Data.MemVddq.ToString();
                //textBoxMemVpp.Text = Data.MemVpp.ToString();
                //rowApuVddio.Value = Data.ApuVddio.ToString();

                try
                {
                    rowProcCaOdt.IsEnabled = Data?.ProcCaOdt != null && !string.Equals(Data?.ProcCaOdt?.ToString(), "N/A");
                    rowProcCkOdt.IsEnabled = Data?.ProcCkOdt != null && !string.Equals(Data?.ProcCkOdt?.ToString(), "N/A");
                    rowProcDqOdt.IsEnabled = Data?.ProcDqOdt != null && !string.Equals(Data?.ProcDqOdt?.ToString(), "N/A");
                    rowProcDqsOdt.IsEnabled = Data?.ProcDqsOdt != null && !string.Equals(Data?.ProcDqsOdt?.ToString(), "N/A");
                    rowProcCaOdt.Value = Data?.ProcCaOdt?.ToString() ?? "N/A";
                    rowProcCkOdt.Value = Data?.ProcCkOdt?.ToString() ?? "N/A";
                    rowProcDqOdt.Value = Data?.ProcDqOdt?.ToString() ?? "N/A";
                    rowProcDqsOdt.Value = Data?.ProcDqsOdt?.ToString() ?? "N/A";
                }
                catch { }

                rowProcCaDs.Value = Data?.CadBusDrvStren?.ToString() ?? "N/A";
                rowDramDqDs.Value = Data?.DramDataDrvStren?.ToString() ?? "N/A";
                rowProcDqDs.Value = Data?.ProcDataDrvStrenApu?.ToString() ?? "N/A";

                rowRttWrD5.Value = Data?.RttWr?.ToString() ?? "N/A";
                rowRttNomWr.Value = Data?.RttNomWr?.ToString() ?? "N/A";
                rowRttNomRd.Value = Data?.RttNomRd?.ToString() ?? "N/A";
                rowRttParkD5.Value = Data?.RttPark?.ToString() ?? "N/A";
                rowRttParkDqs.Value = Data?.RttParkDqs?.ToString() ?? "N/A";
            }
        }
    }
}
