using System.Diagnostics;
using System.Windows.Controls;

namespace MCGCadPlugin.Views.Weight
{
    /// <summary>
    /// UserControl cho tab Weight trong PaletteSet
    /// </summary>
    public partial class WeightView : UserControl
    {
        private const string LOG_PREFIX = "[WeightView]";

        public WeightView()
        {
            InitializeComponent();
            Debug.WriteLine($"{LOG_PREFIX} Khởi tạo.");
        }
    }
}
