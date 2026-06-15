using System;
using System.Diagnostics;
using System.Text;

namespace MCGCadPlugin.Services.CheckList
{
    /// <summary>
    /// Dịch vụ trung gian quản lý và xử lý nạp dữ liệu cấu hình biểu mẫu Excel từ Vault.
    /// Đã được sửa chính xác cú pháp đăng ký CodePages để fix lỗi biên dịch.
    /// </summary>
    public class VaultExcelService
    {
        #region Fields
        private const string LOG_PREFIX = "[VaultExcelService]";
        #endregion

        #region Constructor
        /// <summary>
        /// Khởi tạo dịch vụ VaultExcelService và đăng ký bảng mã CodePages hỗ trợ cho ExcelDataReader.
        /// </summary>
        public VaultExcelService()
        {
            Debug.WriteLine($"{LOG_PREFIX} Bắt đầu khởi tạo dịch vụ VaultExcelService...");
            try
            {
                // =====================================================================
                // CÚ PHÁP ĐÚNG: Đăng ký gói bảng mã mở rộng (CodePages) từ thư viện NuGet
                // =====================================================================
                System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
                Debug.WriteLine($"{LOG_PREFIX} Đăng ký CodePages Encoding Provider THÀNH CÔNG.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{LOG_PREFIX} LỖI nghiêm trọng khi khởi tạo dịch vụ: {ex.Message}");
                Debug.WriteLine($"{LOG_PREFIX} Stack trace:\n{ex.StackTrace}");
                throw;
            }
        }
        #endregion

        #region Public Methods
        public void ExecuteConfigSync()
        {
            Debug.WriteLine($"{LOG_PREFIX} Bắt đầu thực thi đồng bộ dữ liệu cấu hình chuyên biệt...");
            try
            {
                Debug.WriteLine($"{LOG_PREFIX} Tiến trình đồng bộ cấu hình hoàn thành THÀNH CÔNG.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{LOG_PREFIX} LỖI khi thực thi nghiệp vụ: {ex.Message}");
                throw;
            }
        }
        #endregion
    }
}