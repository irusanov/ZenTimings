using System.Collections.Generic;
using System.Windows.Controls;

namespace ZenTimings.Controls
{
    /// <summary>
    /// Interaction logic for DDR4TimingsPanel.xaml
    /// </summary>
    public partial class DDR4TimingsPanel : UserControl
    {
        private readonly List<BiosACPIFunction> biosFunctions = new List<BiosACPIFunction>();

        public DDR4TimingsPanel()
        {
            InitializeComponent();
        }

        private BiosACPIFunction GetFunctionByIdString(string name)
        {
            return biosFunctions.Find(x => x.IDString == name);
        }
    }
}
