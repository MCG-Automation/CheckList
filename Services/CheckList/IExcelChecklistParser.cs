using MCGCadPlugin.Models.CheckList;

namespace MCGCadPlugin.Services.CheckList
{
    /// <summary>
    /// Giao diện dịch vụ bóc tách dữ liệu từ file tệp Excel mẫu của MacGregor.
    /// </summary>
    public interface IExcelChecklistParser
    {
        /// <summary>
        /// Đọc và phân tích tệp tin Excel để trích xuất thông tin Metadata và danh sách câu hỏi.
        /// </summary>
        /// <param name="filePath">Đường dẫn đầy đủ tới file Excel (.xlsx)</param>
        /// <returns>Đối tượng ChecklistDocument chứa dữ liệu bóc tách được</returns>
        ChecklistDocument Parse(string filePath);
    }
}