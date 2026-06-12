using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using ExcelDataReader;
using MCGCadPlugin.Models.CheckList;

namespace MCGCadPlugin.Services.CheckList
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
                Debug.WriteLine($"{LOG_PREFIX} LỖI: Không tìm thấy tệp tại đường dẫn: {filePath}");
                throw new FileNotFoundException("Không tìm thấy tệp Excel Checklist", filePath);
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