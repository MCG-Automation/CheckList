namespace MCG_CheckList.Models.CheckList
{
    /// <summary>
    /// Kết quả trả về sau khi đồng bộ tệp từ Vault Server.
    /// Cho phép tầng View phân biệt giữa "lấy được bản mới nhất từ Vault" và "dùng bản cũ trên máy".
    /// </summary>
    public class VaultSyncResult
    {
        /// <summary>Đường dẫn tệp cục bộ sẵn sàng để đọc (Vault WF hoặc fallback AppData)</summary>
        public string LocalPath { get; set; }

        /// <summary>True nếu AcquireFiles từ Vault thành công; False nếu dùng bản local dự phòng</summary>
        public bool SyncedFromVault { get; set; }

        /// <summary>Thông báo lỗi ngắn gọn khi SyncedFromVault = false; null khi thành công</summary>
        public string ErrorMessage { get; set; }
    }
}
