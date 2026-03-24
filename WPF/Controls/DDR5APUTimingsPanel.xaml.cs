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
                labelApuVddio.IsEnabled = true;
                textBoxApuVddio.Text = Data.ApuVddio.ToString();
            }
        }
    }
}
