using System.Diagnostics;
using System.Windows.Controls;

namespace MCGCadPlugin.Views.DetailDesign
{
    /// <summary>
    /// UserControl cho tab Detail Design trong PaletteSet
    /// </summary>
    public partial class DetailDesignView : UserControl
    {
        private const string LOG_PREFIX = "[DetailDesignView]";

        public DetailDesignView()
        {
            InitializeComponent();
            Debug.WriteLine($"{LOG_PREFIX} Khởi tạo.");
        }
    }
}
