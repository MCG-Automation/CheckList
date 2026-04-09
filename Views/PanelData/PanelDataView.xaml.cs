using System.Diagnostics;
using System.Windows.Controls;

namespace MCGCadPlugin.Views.PanelData
{
    /// <summary>
    /// UserControl cho tab Panel Data trong PaletteSet
    /// </summary>
    public partial class PanelDataView : UserControl
    {
        private const string LOG_PREFIX = "[PanelDataView]";

        public PanelDataView()
        {
            InitializeComponent();
            Debug.WriteLine($"{LOG_PREFIX} Khởi tạo.");
        }
    }
}
