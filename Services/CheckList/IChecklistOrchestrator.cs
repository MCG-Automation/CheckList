using MCGCadPlugin.Models.CheckList;

namespace MCGCadPlugin.Services.CheckList
{
    /// <summary>
    /// Giao diện điều phối chính kết hợp ExcelParser, CacheRepository và VaultSyncService.
    /// Thực hiện Carry-over kế thừa dấu tích thông minh và quản lý đồng bộ Vault.
    /// </summary>
    public interface IChecklistOrchestrator
    {
        /// <summary>
        /// Nạp một file Excel Checklist (đồng bộ qua Vault nếu useVault = true), kiểm tra cache và gộp tiến độ Carry-over.
        /// </summary>
        /// <param name="filePathOrName">Đường dẫn tệp cục bộ hoặc tên tệp tin ảo trong Vault</param>
        /// <param name="settings">Cấu hình kết nối Vault và đường dẫn hiện hành</param>
        /// <param name="useVault">True nếu muốn kích hoạt đồng bộ hóa tự động từ Vault Server trước khi nạp</param>
        /// <returns>Đối tượng ChecklistDocument chứa dữ liệu bóc tách đã gộp tiến độ</returns>
        ChecklistDocument OpenChecklist(string filePathOrName, ChecklistSettings settings, bool useVault);

        /// <summary>
        /// Lưu tiến trình hiện tại của Checklist xuống bộ nhớ cache JSON.
        /// </summary>
        /// <param name="document">Hồ sơ checklist hiện tại của giao diện</param>
        void SaveProgress(ChecklistDocument document);
    }
}