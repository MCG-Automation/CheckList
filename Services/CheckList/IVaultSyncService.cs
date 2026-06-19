using MCG_CheckList.Models.CheckList;

namespace MCG_CheckList.Services.CheckList
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
        /// <returns>
        /// VaultSyncResult với LocalPath sẵn sàng để đọc.
        /// SyncedFromVault = true khi Get Latest thành công; false khi dùng bản local dự phòng.
        /// </returns>
        VaultSyncResult SyncExcelFile(string fileName, ChecklistSettings settings);
    }
}