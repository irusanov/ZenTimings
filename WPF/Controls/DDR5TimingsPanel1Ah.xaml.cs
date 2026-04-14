using System.Windows.Controls;
using ZenStates.Core;

namespace ZenTimings.Controls
{
    /// <summary>
    /// Interaction logic for DDR5xaml
    /// </summary>
    public partial class DDR5TimingsPanel1Ah : UserControl
    {
        public DDR5TimingsPanel1Ah()
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
