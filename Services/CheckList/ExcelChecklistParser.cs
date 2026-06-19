using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using ExcelDataReader;
using System.Reflection;
using MCG_CheckList.Models.CheckList;
using MCG_CheckList.Utilities;

namespace MCG_CheckList.Services.CheckList
{
    /// <summary>
    /// Bộ phân tích dữ liệu Excel Checklist mẫu cho MacGregor.
    /// Sử dụng ExcelDataReader để đọc trực tiếp file nhị phân tốc độ cao mà không cần cài đặt MS Excel.
    /// </summary>
    public class ExcelChecklistParser : IExcelChecklistParser
    {
        #region Fields
        private const string LOG_PREFIX = "[ExcelChecklistParser]";
        #endregion

        #region Public Methods
        /// <summary>
        /// Đọc tệp tin Excel, bóc tách Metadata (Project No, Panel) và nạp danh sách câu hỏi kiểm tra.
        /// </summary>
        public ChecklistDocument Parse(string filePath)
        {
            Debug.WriteLine($"{LOG_PREFIX} Bắt đầu phân tích tệp Excel Checklist: {filePath}");

            if (string.IsNullOrEmpty(filePath))
            {
                Debug.WriteLine($"{LOG_PREFIX} LỖI: Đường dẫn tệp rỗng.");
                throw new ArgumentNullException(nameof(filePath));
            }

            if (!File.Exists(filePath))
            {
                Debug.WriteLine($"{LOG_PREFIX} CẢNH BÁO: Không tìm thấy tệp. Đang giải nén template mặc định...");
                bool extracted = ExtractDefaultTemplate(filePath);
                
                if (!extracted)
                {
                    Debug.WriteLine($"{LOG_PREFIX} LỖI: Không thể giải nén template mặc định.");
                    throw new FileNotFoundException("Checklist template not found and fallback extraction failed.", filePath);
                }
            }

            var document = new ChecklistDocument();
            var items = new List<ChecklistItem>();

            try
            {
                // Sử dụng FileShare.ReadWrite để tránh xung đột khi tệp đang được mở bởi người dùng khác (ví dụ: MS Excel)
                using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    using (var reader = ExcelReaderFactory.CreateReader(stream))
                    {
                        int rowIndex = 0;
                        string projectNo = string.Empty;
                        string panelName = string.Empty;

                        while (reader.Read())
                        {
                            // Đọc giá trị ở cột đầu tiên (index 0 - Cột A trong Excel)
                            object firstCellValueObj = reader.GetValue(0);
                            string cellValue = firstCellValueObj?.ToString()?.Trim() ?? string.Empty;

                            // Phân tích thông tin Metadata ở các dòng đầu tiên (Header của biểu mẫu)
                            if (rowIndex < 7)
                            {
                                if (cellValue.StartsWith("PROJECT NO:", StringComparison.OrdinalIgnoreCase))
                                {
                                    projectNo = ExtractValueAfterPrefix(cellValue, "PROJECT NO:");
                                    Debug.WriteLine($"{LOG_PREFIX} Tìm thấy PROJECT NO: {projectNo}");
                                }
                                else if (cellValue.StartsWith("PANEL:", StringComparison.OrdinalIgnoreCase))
                                {
                                    panelName = ExtractValueAfterPrefix(cellValue, "PANEL:");
                                    Debug.WriteLine($"{LOG_PREFIX} Tìm thấy PANEL: {panelName}");
                                }
                            }
                            else
                            {
                                // Bắt đầu quét danh sách câu hỏi kiểm tra từ dòng số 8 trở đi
                                if (!string.IsNullOrEmpty(cellValue))
                                {
                                    // Kiểm tra điều kiện dừng biểu mẫu (gặp dòng lưu ý cuối bảng)
                                    if (cellValue.StartsWith("Square boxes to be crossed", StringComparison.OrdinalIgnoreCase) ||
                                        cellValue.StartsWith("Signed original to be saved", StringComparison.OrdinalIgnoreCase))
                                    {
                                        Debug.WriteLine($"{LOG_PREFIX} Gặp dấu hiệu dòng kết thúc biểu mẫu tại dòng {rowIndex + 1}. Kết thúc quét.");
                                        break;
                                    }

                                    // Tạo mới câu hỏi checklist chuẩn mặc định (IsCustom = false)
                                    var checklistItem = new ChecklistItem(cellValue, isCustom: false);
                                    items.Add(checklistItem);
                                }
                            }

                            rowIndex++;
                        }

                        // Gán Metadata đã trích xuất được vào tài liệu
                        document.ProjectNo = projectNo;
                        document.PanelName = panelName;
                        document.Discipline = GetDisciplineFromPathOrFileName(filePath);
                        document.Items = items;

                        Debug.WriteLine($"{LOG_PREFIX} Phân tích hoàn tất. Số lượng câu hỏi bóc tách: {items.Count}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{LOG_PREFIX} LỖI nghiêm trọng khi bóc tách Excel: {ex.Message}");
                Debug.WriteLine($"{LOG_PREFIX} Stack trace:\n{ex.StackTrace}");
                throw;
            }

            return document;
        }
        #endregion

        #region Helpers
        /// <summary>
        /// Giải nén file Excel template từ Embedded Resource của Assembly.
        /// Tìm resource theo suffix tên file (không hardcode namespace) để tương thích mọi phiên bản MSBuild.
        /// </summary>
        private bool ExtractDefaultTemplate(string targetPath)
        {
            try
            {
                string fileName = Path.GetFileName(targetPath);
                var assembly = Assembly.GetExecutingAssembly();
                string[] allResources = assembly.GetManifestResourceNames();

                // Tìm resource khớp với tên file — dùng EndsWith thay vì Equals để tránh sai prefix namespace
                string resourceName = null;
                foreach (string name in allResources)
                {
                    if (name.EndsWith(fileName, StringComparison.OrdinalIgnoreCase))
                    {
                        resourceName = name;
                        break;
                    }
                }

                if (resourceName == null)
                {
                    string available = allResources.Length > 0
                        ? string.Join(", ", allResources)
                        : "(none — no .xlsx in Resources/ folder)";
                    string errorMsg = $"No embedded resource found matching '{fileName}'. " +
                                      $"Copy the Excel template to the project's Resources/ folder and rebuild. Available: [{available}]";
                    Debug.WriteLine($"{LOG_PREFIX} ERROR: {errorMsg}");
                    FileLogger.LogException(LOG_PREFIX, errorMsg, new Exception("ManifestResourceStream is null"));
                    return false;
                }

                using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null) return false;

                    // Đảm bảo thư mục cha tồn tại trước khi ghi file
                    string folder = Path.GetDirectoryName(targetPath);
                    if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder))
                        Directory.CreateDirectory(folder);

                    using (FileStream fileStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write))
                    {
                        stream.CopyTo(fileStream);
                    }
                }

                Debug.WriteLine($"{LOG_PREFIX} Extracted embedded resource '{resourceName}' → {targetPath}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{LOG_PREFIX} ExtractDefaultTemplate FAILED: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Trích xuất chuỗi ký tự đứng sau tiền tố tiêu đề
        /// </summary>
        private string ExtractValueAfterPrefix(string text, string prefix)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            int idx = text.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                return text.Substring(idx + prefix.Length).Trim().Trim(',', ';', ' ', '\t');
            }
            return string.Empty;
        }

        /// <summary>
        /// Dự đoán bộ môn kỹ thuật dựa trên từ khóa của tên tệp tin để gán Discipline tự động
        /// </summary>
        private string GetDisciplineFromPathOrFileName(string filePath)
        {
            string lowerPath = filePath.ToLower();
            if (lowerPath.Contains("structure")) return "Structure (Panel)";
            if (lowerPath.Contains("mechanical")) return "Mechanical";
            if (lowerPath.Contains("layout") || lowerPath.Contains("interface")) return "Layout / Interface";

            return "Structure (Panel)"; // Bộ môn mặc định dự phòng
        }
        #endregion
    }
}