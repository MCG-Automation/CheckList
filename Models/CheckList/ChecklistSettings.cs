namespace MCGCadPlugin.Models.CheckList
{
    /// <summary>
    /// Lớp đại diện cho cấu hình tệp settings.json của module QA/QC Checklist.
    /// Bao gồm các tham số xác thực kết nối Autodesk Vault và bộ lưu vết phiên làm việc.
    /// </summary>
    public class ChecklistSettings
    {
        /// <summary>
        /// URL hoặc địa chỉ IP của máy chủ Autodesk Vault Server (ví dụ: http://localhost)
        /// </summary>
        public string VaultServer { get; set; } = "http://localhost";

        /// <summary>
        /// Tên cơ sở dữ liệu kho lưu trữ Vault (ví dụ: Designs)
        /// </summary>
        public string VaultName { get; set; } = "Designs";

        /// <summary>
        /// Đường dẫn ảo tương đối của thư mục chứa các biểu mẫu Excel trên Vault Server
        /// </summary>
        public string VaultExcelFolderPath { get; set; } = "$/Designs/90 Users/truonph/";

        /// <summary>
        /// Thư mục làm việc cục bộ được ghi nhớ gần nhất từ lượt chọn thủ công của kỹ sư
        /// </summary>
        public string LastExcelFolder { get; set; } = @"C:\MacGregor_CAS_WF\Designs\90 Users\truonph";

        /// <summary>
        /// Bộ môn thiết kế được lựa chọn gần nhất ở combobox để khôi phục phiên làm việc sau khi khởi động lại
        /// </summary>
        public string LastSelectedDisciplineTag { get; set; } = "Temp Checklist - LayoutInterface.xlsx";
    }
}