using System.Diagnostics;
using System.Windows.Controls;

namespace MCGCadPlugin.Views.TableOfContent
{
    /// <summary>
    /// UserControl cho tab Table of Content trong PaletteSet
    /// </summary>
    public partial class TableOfContentView : UserControl
    {
        private const string LOG_PREFIX = "[TableOfContentView]";

        public TableOfContentView()
        {
            InitializeComponent();
            Debug.WriteLine($"{LOG_PREFIX} Khởi tạo.");
        }
    }
}
