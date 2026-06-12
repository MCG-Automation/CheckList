using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using MCGCadPlugin.Models.CheckList;
using MCGCadPlugin.Services.CheckList;

namespace MCGCadPlugin.Views.CheckList
{
    /// <summary>
    /// Code-behind của bảng điều khiển QaChecklistView (AutoCAD Palette Tab).
    /// Triển khai Data Binding trực tiếp, Auto-save Debounce 500ms và Approval Workflow.
    /// </summary>
    public partial class QaChecklistView : UserControl, INotifyPropertyChanged
    {
        #region Fields
        private const string LOG_PREFIX = "[QaChecklistView]";
        private const string PLACEHOLDER_TEXT = "Enter custom item content here...";
        private const string DEFAULT_EXCEL_DIR = @"C:\MacGregor_CAS_WF\Designs\90 Users\truonph";

        private readonly IChecklistOrchestrator _orchestrator;
        private readonly SettingsRepository _settingsRepo;
        private ChecklistDocument _document;
        private ObservableCollection<ChecklistItem> _checklistItems;

        private readonly DispatcherTimer _debounceSaveTimer;
        #endregion

        #region Properties
        public ChecklistDocument Document
        {
            get => _document;
            set
            {
                if (_document == value) return;
                _document = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsApproved));
                OnPropertyChanged(nameof(IsEditable));
                OnPropertyChanged(nameof(IsGridEnabled));
                OnPropertyChanged(nameof(StatusColor));
                UpdateProgress();
            }
        }

        public ObservableCollection<ChecklistItem> ChecklistItems
        {
            get => _checklistItems;
            set
            {
                if (_checklistItems == value) return;
                _checklistItems = value;
                OnPropertyChanged();
                UpdateProgress();
            }
        }

        private double _progressValue;
        public double ProgressValue
        {
            get => _progressValue;
            set
            {
                _progressValue = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ProgressText));
            }
        }

        public string ProgressText => $"{Math.Round(ProgressValue)}%";

        public bool IsApproved => Document?.Status == "APPROVED";

        public bool IsEditable => Document != null && !IsApproved;

        public Brush StatusColor => IsApproved ? Brushes.ForestGreen : Brushes.OrangeRed;

        // ================================================================= -->
        // THÊM MỚI: Các thuộc tính Binding theo dõi luồng bất đồng bộ        -->
        // ================================================================= -->
        private bool _isLoading;
        /// <summary>True khi tệp đang thực hiện đồng bộ Vault ngầm</summary>
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (_isLoading == value) return;
                _isLoading = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsNotLoading));
                OnPropertyChanged(nameof(IsGridEnabled));
            }
        }

        public bool IsNotLoading => !IsLoading;

        /// <summary>Khóa lưới tương tác chỉnh sửa khi tệp đang tải hoặc đã được duyệt</summary>
        public bool IsGridEnabled => !IsLoading && IsEditable;
        // ================================================================= -->
        #endregion

        #region Constructor
        public QaChecklistView()
        {
            InitializeComponent();
            DataContext = this;

            // Khởi tạo các dịch vụ phụ thuộc thủ công (DI thủ công tại entry point)
            var excelParser = new ExcelChecklistParser();
            var cacheRepo = new JsonChecklistRepository();
            var vaultSync = new VaultSyncService();
            _orchestrator = new ChecklistOrchestrator(excelParser, cacheRepo, vaultSync);
            _settingsRepo = new SettingsRepository();

            // Cấu hình Timer cho chức năng Auto-save Debounce 500ms
            _debounceSaveTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _debounceSaveTimer.Tick += DebounceSaveTimer_Tick;

            // Cài đặt textbox placeholder ban đầu
            ResetTextBoxPlaceholder();

            // Tự động tải Excel dựa trên bộ môn đang được chọn mặc định trong ComboBox
            TriggerInitialSelection();

            Debug.WriteLine($"{LOG_PREFIX} Giao diện được khởi tạo THÀNH CÔNG.");
        }
        #endregion

        #region Method: Trigger Initial Selection
        /// <summary>
        /// Kích hoạt nạp tệp Excel tương ứng cho bộ môn được chọn làm mặc định khi mở Palette
        /// </summary>
        private async void TriggerInitialSelection()
        {
            if (_orchestrator == null) return;

            if (CboDiscipline.SelectedItem is ComboBoxItem selectedItem)
            {
                string fileName = selectedItem.Tag as string;
                if (!string.IsNullOrEmpty(fileName))
                {
                    await LoadChecklistFileAsync(fileName, useVault: true);
                }
            }
        }
        #endregion

        #region Event Handlers: Load Excel
        /// <summary>
        /// Sự kiện thay đổi lựa chọn trên Dropdown Bộ môn (Được nâng cấp chạy Bất đồng bộ)
        /// </summary>
        private async void CboDiscipline_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_orchestrator == null) return;

            if (CboDiscipline.SelectedItem is ComboBoxItem selectedItem)
            {
                string fileName = selectedItem.Tag as string;
                if (!string.IsNullOrEmpty(fileName))
                {
                    Debug.WriteLine($"{LOG_PREFIX} Dropdown changed. Syncing with Vault & Loading: {fileName}");
                    await LoadChecklistFileAsync(fileName, useVault: true);
                }
            }
        }

        /// <summary>
        /// Sự kiện click nút tải Excel ngoài thủ công (Nút ba chấm "...")
        /// </summary>
        private async void BtnLoadExcel_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine($"{LOG_PREFIX} Click chọn file Excel Checklist thủ công...");
            
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Excel Files (*.xlsx)|*.xlsx",
                Title = "Select Excel Checklist Template"
            };

            // Thiết lập thư mục khởi đầu mặc định theo yêu cầu người dùng (nếu tồn tại)
            if (Directory.Exists(DEFAULT_EXCEL_DIR))
            {
                openFileDialog.InitialDirectory = DEFAULT_EXCEL_DIR;
                Debug.WriteLine($"{LOG_PREFIX} Đã thiết lập đường dẫn mặc định khởi tạo: {DEFAULT_EXCEL_DIR}");
            }

            if (openFileDialog.ShowDialog() == true)
            {
                // Gọi bất đồng bộ (useVault = false khi chọn tệp thủ công cục bộ ngoài Vault)
                await LoadChecklistFileAsync(openFileDialog.FileName, useVault: false);
            }
        }

        /// <summary>
        /// TIÊU ĐIỂM: Thư mục nạp tệp Async chống treo luồng UI của AutoCAD.
        /// Chạy tác vụ đồng bộ Vault & bóc tách tệp Excel nhị phân hoàn toàn trên luồng chạy ngầm (Task.Run)
        /// </summary>
        private async Task LoadChecklistFileAsync(string filePathOrName, bool useVault)
        {
            if (string.IsNullOrEmpty(filePathOrName)) return;

            // Kích hoạt lớp phủ mờ ProgressBar
            IsLoading = true;

            try
            {
                // Giải phóng sự kiện theo dõi Grid cũ trước khi nạp
                if (ChecklistItems != null)
                {
                    foreach (var item in ChecklistItems)
                    {
                        item.PropertyChanged -= Item_PropertyChanged;
                    }
                }

                ChecklistItems = null;

                // 1. Thực thi đồng bộ Vault Server và phân tích Excel hoàn toàn trên luồng chạy ngầm Task.Run
                var settings = _settingsRepo.Load();
                ChecklistDocument loadedDoc = await Task.Run(() => 
                    _orchestrator.OpenChecklist(filePathOrName, settings, useVault)
                );

                // Trả focus về bản vẽ AutoCAD sau khi tác vụ ngầm hoàn thành
                Autodesk.AutoCAD.Internal.Utils.SetFocusToDwgView();

                // 2. Cập nhật lại các liên kết WPF trên luồng UI chính
                Document = loadedDoc;
                ChecklistItems = new ObservableCollection<ChecklistItem>(loadedDoc.Items);

                // Đăng ký theo dõi sự thay đổi trạng thái tick của từng câu hỏi mới
                foreach (var item in ChecklistItems)
                {
                    item.PropertyChanged += Item_PropertyChanged;
                }

                UpdateProgress();
                Debug.WriteLine($"{LOG_PREFIX} Load Checklist success: Project {Document.ProjectNo}, Panel {Document.PanelName}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{LOG_PREFIX} LỖI nghiêm trọng khi nạp Excel Async: {ex.Message}");
                MessageBox.Show($"Error parsing or syncing Excel file data:\n{ex.Message}", "System Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // Tắt lớp phủ mờ trả quyền kiểm soát cho kỹ sư
                IsLoading = false;
            }
        }
        #endregion

        #region Event Handlers: Palette Set Close Control
        /// <summary>
        /// Sự kiện click nút đóng Palette (✕) ở góc trên bên phải giao diện
        /// </summary>
        private void BtnClosePalette_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine($"{LOG_PREFIX} Bắt đầu xử lý đóng ẩn bảng điều khiển PaletteSet...");
            try
            {
                // Truy cập Singleton PaletteManager để ẩn PaletteSet chính thức
                MCGCadPlugin.Commands.PaletteManager.Instance?.Hide();
                Debug.WriteLine($"{LOG_PREFIX} Đã ẩn PaletteSet thành công qua PaletteManager.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{LOG_PREFIX} LỖI khi ẩn PaletteSet: {ex.Message}");
            }
        }
        #endregion

        #region Event Handlers: Items Interaction & Auto-save Debounce
        /// <summary>
        /// Kích hoạt khi có bất kỳ thuộc tính nào của câu hỏi thay đổi (được Tick hoặc gõ chữ)
        /// </summary>
        private void Item_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ChecklistItem.IsChecked) || 
                e.PropertyName == nameof(ChecklistItem.IsNotApplicable) ||
                e.PropertyName == nameof(ChecklistItem.Content))
            {
                UpdateProgress();

                // Reset timer: Nếu người dùng đang liên tục thao tác gõ hoặc tick liên tục, Timer sẽ reset 
                // và chỉ tiến hành ghi cứng xuống đĩa sau 500ms tĩnh lặng (Debounce)
                _debounceSaveTimer.Stop();
                _debounceSaveTimer.Start();
            }
        }

        /// <summary>
        /// Thực hiện hành động Auto-save sau 500ms
        /// </summary>
        private void DebounceSaveTimer_Tick(object sender, EventArgs e)
        {
            _debounceSaveTimer.Stop();
            if (Document != null && ChecklistItems != null)
            {
                Debug.WriteLine($"{LOG_PREFIX} Tự động lưu cache (Auto-save) sau 500ms tĩnh lặng.");
                Document.Items = ChecklistItems.ToList();
                _orchestrator.SaveProgress(Document);
            }
        }
        #endregion

        #region Event Handlers: Custom Items Control
        /// <summary>
        /// Sự kiện thêm câu hỏi tự chọn (Custom Item)
        /// </summary>
        private void BtnAddCustomItem_Click(object sender, RoutedEventArgs e)
        {
            string contentText = TxtCustomItem.Text?.Trim();
            if (string.IsNullOrEmpty(contentText) || contentText == PLACEHOLDER_TEXT)
            {
                return;
            }

            if (Document == null || ChecklistItems == null)
            {
                MessageBox.Show("Please load an Excel file before adding custom items.", "Notification", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Tạo item tùy biến và đăng ký PropertyChanged
            var customItem = new ChecklistItem(contentText, isCustom: true);
            customItem.PropertyChanged += Item_PropertyChanged;

            ChecklistItems.Add(customItem);
            ResetTextBoxPlaceholder();
            UpdateProgress();

            // Lưu trực tiếp
            _debounceSaveTimer.Stop();
            _debounceSaveTimer.Start();

            Debug.WriteLine($"{LOG_PREFIX} Thêm thành công câu hỏi tự chọn.");
        }

        /// <summary>
        /// Sự kiện xóa một câu hỏi tự chọn
        /// </summary>
        private void BtnDeleteCustomItem_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var itemToRemove = button?.DataContext as ChecklistItem;

            if (itemToRemove != null && ChecklistItems != null)
            {
                itemToRemove.PropertyChanged -= Item_PropertyChanged;
                ChecklistItems.Remove(itemToRemove);
                UpdateProgress();

                // Kích hoạt lưu
                _debounceSaveTimer.Stop();
                _debounceSaveTimer.Start();

                Debug.WriteLine($"{LOG_PREFIX} Xóa thành công câu hỏi tự chọn.");
            }
        }
        #endregion

        #region Event Handlers: Approval Workflow
        /// <summary>
        /// Sự kiện ký duyệt hồ sơ checklist (Approve)
        /// </summary>
        private void BtnApprove_Click(object sender, RoutedEventArgs e)
        {
            if (Document == null || ChecklistItems == null) return;

            // Kiểm tra xem đã hoàn thành hết checklist chưa
            int remainingUnchecked = ChecklistItems.Count(i => !i.IsChecked && !i.IsNotApplicable);
            if (remainingUnchecked > 0)
            {
                var result = MessageBox.Show(
                    $"There are still {remainingUnchecked} unchecked items.\nAre you sure you want to approve this document?",
                    "Progress Warning", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                
                if (result != MessageBoxResult.Yes) return;
            }

            // Đóng băng trạng thái phê duyệt
            Document.Status = "APPROVED";
            Document.ApprovedBy = Environment.UserName;
            Document.ApprovedDate = DateTime.Now.ToString("g");

            OnPropertyChanged(nameof(IsApproved));
            OnPropertyChanged(nameof(IsEditable));
            OnPropertyChanged(nameof(IsGridEnabled));
            OnPropertyChanged(nameof(StatusColor));

            // Lưu lập tức bộ đệm
            _debounceSaveTimer.Stop();
            Document.Items = ChecklistItems.ToList();
            _orchestrator.SaveProgress(Document);

            Debug.WriteLine($"{LOG_PREFIX} Ký duyệt phê duyệt THÀNH CÔNG bởi: {Document.ApprovedBy}");
        }

        /// <summary>
        /// Sự kiện mở khóa hồ sơ để kiểm tra lại
        /// </summary>
        private void BtnReopen_Click(object sender, RoutedEventArgs e)
        {
            if (Document == null) return;

            Document.Status = "PENDING";
            Document.ApprovedBy = null;
            Document.ApprovedDate = null;

            OnPropertyChanged(nameof(IsApproved));
            OnPropertyChanged(nameof(IsEditable));
            OnPropertyChanged(nameof(IsGridEnabled));
            OnPropertyChanged(nameof(StatusColor));

            _debounceSaveTimer.Stop();
            _orchestrator.SaveProgress(Document);

            Debug.WriteLine($"{LOG_PREFIX} Khôi phục hồ sơ về trạng thái PENDING.");
        }
        #endregion

        #region Helpers
        /// <summary>
        /// Tính toán phần trăm tiến độ checklist
        /// </summary>
        private void UpdateProgress()
        {
            if (ChecklistItems == null || ChecklistItems.Count == 0)
            {
                ProgressValue = 0;
                return;
            }

            int totalCount = ChecklistItems.Count;
            // Số lượng câu hỏi hoàn thành = Checked hoặc được đánh dấu Không áp dụng (N/A)
            int completedCount = ChecklistItems.Count(i => i.IsChecked || i.IsNotApplicable);

            ProgressValue = ((double)completedCount / totalCount) * 100;
        }

        // Cấu hình Placeholder văn bản cho Textbox nhập
        private void ResetTextBoxPlaceholder()
        {
            TxtCustomItem.Text = PLACEHOLDER_TEXT;
            // Sử dụng màu chữ xám hệ thống động để hài hòa trên cả nền Sáng và Tối
            TxtCustomItem.Foreground = SystemColors.GrayTextBrush;
        }

        private void TxtCustomItem_GotFocus(object sender, RoutedEventArgs e)
        {
            if (TxtCustomItem.Text == PLACEHOLDER_TEXT)
            {
                TxtCustomItem.Text = string.Empty;
                TxtCustomItem.Foreground = SystemColors.ControlTextBrush;
            }
        }

        private void TxtCustomItem_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtCustomItem.Text))
            {
                ResetTextBoxPlaceholder();
            }
        }
        #endregion

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }
}