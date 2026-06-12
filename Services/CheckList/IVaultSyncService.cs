using MCGCadPlugin.Models.CheckList;

namespace MCGCadPlugin.Services.CheckList
{
    /// <summary>
    /// Giao diện dịch vụ tương tác với Autodesk Vault API để tải và đồng bộ các tệp tin Excel Checklist.
    /// </summary>
    public interface IVaultSyncService
    {
        /// <summary>
        /// Thực hiện kết nối tới Vault và tải về phiên bản mới nhất (Get Latest) của tệp Excel chỉ định.
        /// </summary>
        /// <param name="fileName">Tên tệp Excel cần tải (ví dụ: Temp Checklist - Structure.xlsx)</param>
        /// <param name="settings">Cấu hình cài đặt Vault hiện hành</param>
        /// <returns>Đường dẫn tệp tin cục bộ (Local Working Path) sẵn sàng để đọc, hoặc đường dẫn dự phòng nếu lỗi</returns>
        string SyncExcelFile(string fileName, ChecklistSettings settings);
    }
}