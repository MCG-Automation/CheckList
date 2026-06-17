using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using MCGCadPlugin.Models.CheckList;

namespace MCGCadPlugin.Services.CheckList
{
    /// <summary>
    /// Bộ điều phối trung tâm kết hợp nạp tệp Excel, đồng bộ Vault và đồng bộ bộ đệm JSON cục bộ.
    /// Triển khai thuật toán Carry-over kế thừa dấu tích thông minh và giữ lại các câu hỏi tùy biến (custom items).
    /// </summary>
    public class ChecklistOrchestrator : IChecklistOrchestrator
    {
        #region Fields
        private const string LOG_PREFIX = "[ChecklistOrchestrator]";
        private readonly IExcelChecklistParser _excelParser;
        private readonly IChecklistRepository _cacheRepository;
        private readonly IVaultSyncService _vaultSyncService;
        #endregion

        #region Constructor
        /// <summary>
        /// Khởi tạo bộ điều phối chính và tiêm (inject) các phụ thuộc tương ứng thông qua nguyên lý SOLID.
        /// </summary>
        public ChecklistOrchestrator(
            IExcelChecklistParser excelParser, 
            IChecklistRepository cacheRepository,
            IVaultSyncService vaultSyncService)
        {
            _excelParser = excelParser ?? throw new ArgumentNullException(nameof(excelParser));
            _cacheRepository = cacheRepository ?? throw new ArgumentNullException(nameof(cacheRepository));
            _vaultSyncService = vaultSyncService ?? throw new ArgumentNullException(nameof(vaultSyncService));
            Debug.WriteLine($"{LOG_PREFIX} Khởi tạo bộ điều phối thành công.");
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Nạp tệp Excel (qua Vault hoặc trực tiếp từ đĩa), thực hiện Carry-over tự động kế thừa tiến độ cũ nếu đã có cache.
        /// </summary>
        public ChecklistDocument OpenChecklist(string filePathOrName, ChecklistSettings settings, bool useVault, ChecklistDocument dwgPreload = null)
        {
            Debug.WriteLine($"{LOG_PREFIX} Bắt đầu mở Checklist: {filePathOrName} (UseVault: {useVault}, DWG preload: {dwgPreload != null})");
            try
            {
                string targetLocalPath = filePathOrName;
                VaultSyncResult syncResult = null;

                // 1. Nếu kích hoạt Vault, tiến hành gọi dịch vụ đồng bộ tải phiên bản mới nhất về Working Folder trước
                if (useVault)
                {
                    Debug.WriteLine($"{LOG_PREFIX} Yêu cầu đồng bộ qua Vault. Gọi VaultSyncService...");
                    syncResult = _vaultSyncService.SyncExcelFile(filePathOrName, settings);
                    targetLocalPath = syncResult.LocalPath;
                    Debug.WriteLine($"{LOG_PREFIX} Vault sync: SyncedFromVault={syncResult.SyncedFromVault}, Path={targetLocalPath}");
                }
                else if (!Path.IsPathRooted(filePathOrName))
                {
                    // Khi không dùng Vault và chỉ có tên file, resolve về thư mục template dùng chung
                    targetLocalPath = Path.Combine(ChecklistAppDataPaths.GetDefaultDesignPath(), filePathOrName);
                    Debug.WriteLine($"{LOG_PREFIX} Non-Vault mode, resolved path: {targetLocalPath}");
                }

                // 2. Phân tích tệp Excel tại đường dẫn cục bộ đích để lấy Metadata và danh sách câu hỏi mẫu mới nhất
                // Lưu ý: _excelParser.Parse đã tích hợp sẵn cơ chế Fallback (giải nén từ Resource) nếu không tìm thấy file
                // nên không cần kiểm tra File.Exists tại đây để tránh ngăn cản logic dự phòng.
                var newDoc = _excelParser.Parse(targetLocalPath);

                // 3. Xác định nguồn cache: DWG được ưu tiên vì đi theo file bản vẽ (portable hơn JSON local)
                //    Nếu không có DWG preload thì fallback về JSON cache trên máy hiện tại.
                ChecklistDocument effectiveCache = dwgPreload;
                if (effectiveCache != null)
                {
                    Debug.WriteLine($"{LOG_PREFIX} Sử dụng dữ liệu DWG làm nguồn Carry-over cho Panel '{newDoc.PanelName}'.");
                }
                else
                {
                    effectiveCache = _cacheRepository.LoadChecklist(newDoc.ProjectNo, newDoc.PanelName, newDoc.Discipline);
                    if (effectiveCache != null)
                        Debug.WriteLine($"{LOG_PREFIX} Không có DWG cache, dùng JSON cache cho Panel '{newDoc.PanelName}'.");
                }

                if (effectiveCache != null)
                {
                    // Thực hiện thuật toán gộp trạng thái cũ vào cấu trúc câu hỏi mới
                    var mergedItems = MergeChecklistItems(newDoc.Items, effectiveCache.Items);
                    newDoc.Items = mergedItems;

                    // Kế thừa trạng thái phê duyệt (Approval workflow)
                    newDoc.Status = effectiveCache.Status;
                    newDoc.ApprovedBy = effectiveCache.ApprovedBy;
                    newDoc.ApprovedDate = effectiveCache.ApprovedDate;
                }
                else
                {
                    Debug.WriteLine($"{LOG_PREFIX} Không tìm thấy cache nào cho Panel '{newDoc.PanelName}'. Sử dụng danh sách Excel làm mặc định.");
                }

                // 4. Ghi trạng thái đồng bộ Vault vào document để tầng View hiển thị cho kỹ sư
                if (syncResult != null)
                {
                    newDoc.SyncedFromVault = syncResult.SyncedFromVault;
                    newDoc.SyncMessage = syncResult.ErrorMessage;
                }

                // 5. Tự động lưu trữ tiến độ lập tức để đồng bộ tệp cứng trên hệ thống đĩa
                _cacheRepository.SaveChecklist(newDoc);
                Debug.WriteLine($"{LOG_PREFIX} Đã đồng bộ an toàn cache cục bộ cho Checklist: {newDoc.PanelName}");

                return newDoc;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{LOG_PREFIX} LỖI khi nạp và xử lý gộp tiến trình Checklist: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Lưu tiến trình hiện tại của tài liệu xuống bộ đệm cục bộ.
        /// </summary>
        public void SaveProgress(ChecklistDocument document)
        {
            if (document == null)
            {
                Debug.WriteLine($"{LOG_PREFIX} Cảnh báo: Tài liệu lưu trữ trống (null).");
                return;
            }

            Debug.WriteLine($"{LOG_PREFIX} Đang tiến hành lưu tiến độ tương tác cho Panel: {document.PanelName}");
            try
            {
                _cacheRepository.SaveChecklist(document);
                Debug.WriteLine($"{LOG_PREFIX} Lưu tiến độ thành công.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{LOG_PREFIX} LỖI khi lưu tiến độ xuống đĩa: {ex.Message}");
                throw;
            }
        }
        #endregion

        #region Core Logic: Carry-over
        /// <summary>
        /// Thuật toán Carry-over:
        /// 1. Áp dụng so khớp chữ 100% (sau khi Trim) đối với các câu hỏi chuẩn từ Excel.
        /// 2. Bảo toàn trạng thái IsChecked, IsNotApplicable của các câu hỏi trùng khớp.
        /// 3. Giữ lại toàn bộ các câu hỏi tùy biến (IsCustom = true) do kỹ sư tự thêm trong quá trình làm việc trước đó.
        /// </summary>
        private List<ChecklistItem> MergeChecklistItems(List<ChecklistItem> templateItems, List<ChecklistItem> cachedItems)
        {
            var mergedList = new List<ChecklistItem>();

            // Tạo từ điển tra cứu nhanh cho các câu hỏi mẫu cũ trong cache để tối ưu hóa hiệu năng (O(N) thay vì O(N^2))
            var cacheLookup = new Dictionary<string, ChecklistItem>(StringComparer.OrdinalIgnoreCase);

            foreach (var cachedItem in cachedItems.Where(i => !i.IsCustom))
            {
                string key = cachedItem.Content?.Trim() ?? string.Empty;
                if (!string.IsNullOrEmpty(key) && !cacheLookup.ContainsKey(key))
                {
                    cacheLookup[key] = cachedItem;
                }
            }

            // 1. Quét qua danh sách mẫu mới bóc tách từ Excel
            foreach (var templateItem in templateItems)
            {
                string key = templateItem.Content?.Trim() ?? string.Empty;

                if (cacheLookup.TryGetValue(key, out var matchedCachedItem))
                {
                    // Nếu tìm thấy câu hỏi tương ứng trong cache cũ -> Kế thừa dấu tích và trạng thái không áp dụng (N/A)
                    templateItem.IsChecked = matchedCachedItem.IsChecked;
                    templateItem.IsNotApplicable = matchedCachedItem.IsNotApplicable;
                    // Đồng bộ lại GUID cũ của item để duy trì binding UI mượt mà
                    templateItem.Id = matchedCachedItem.Id;

                    Debug.WriteLine($"{LOG_PREFIX} Carry-over trùng khớp thành công câu hỏi: \"{templateItem.Content.Substring(0, Math.Min(30, templateItem.Content.Length))}...\"");
                }
                
                mergedList.Add(templateItem);
            }

            // 2. Bảo toàn và thêm lại toàn bộ các câu hỏi tự thêm (IsCustom = true) từ phiên làm việc trước
            var customCachedItems = cachedItems.Where(i => i.IsCustom).ToList();
            if (customCachedItems.Count > 0)
            {
                Debug.WriteLine($"{LOG_PREFIX} Phát hiện và khôi phục {customCachedItems.Count} câu hỏi tự thêm (Custom Items) của Kỹ sư.");
                mergedList.AddRange(customCachedItems);
            }

            return mergedList;
        }
        #endregion
    }
}