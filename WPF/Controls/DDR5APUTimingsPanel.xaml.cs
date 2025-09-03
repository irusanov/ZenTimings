using System.Windows;
using System.Windows.Controls;
using ZenStates.Core;

namespace ZenTimings.Controls
{
    /// <summary>
    /// Interaction logic for DDR5APUTimingsPanel.xaml
    /// </summary>
    public partial class DDR5APUTimingsPanel : UserControl
    {
        public DDR5APUTimingsPanel()
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
                    labelProcCaOdt.IsEnabled = true;
                    labelProcCkOdt.IsEnabled = true;
                    labelProcDqOdt.IsEnabled = true;
                    labelProcDqsOdt.IsEnabled = true;
                    textBoxProcCaOdt.Text = Data?.ProcCaOdt?.ToString() ?? "";
                    textBoxProcCkOdt.Text = Data?.ProcCkOdt?.ToString() ?? "";
                    textBoxProcDqOdt.Text = Data?.ProcDqOdt?.ToString() ?? "";
                    textBoxProcDqsOdt.Text = Data?.ProcDqsOdt?.ToString() ?? "";
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
