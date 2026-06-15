using System;

namespace MCGCadPlugin.Models.CheckList
{
    /// <summary>
    /// Lớp đại diện cho cấu hình tệp settings.json của module QA/QC Checklist.
    /// Đã đồng bộ chuẩn hóa đường dẫn cục bộ theo Workspace thực tế của người dùng.
    /// </summary>
    public class ChecklistSettings
    {
        /// <summary>Địa chỉ máy chủ Autodesk Vault Server nội bộ</summary>
        public string VaultServer { get; set; } = "VNHPH1-S0006";

        /// <summary>Tên cơ sở dữ liệu kho lưu trữ Vault hiện hành</summary>
        public string VaultName { get; set; } = "MacGregor_CAS";

        /// <summary>Đường dẫn ảo tương đối của thư mục chứa các biểu mẫu Excel trên Vault Server</summary>
        public string VaultExcelFolderPath { get; set; } = $"$/Designs/90 Users/{Environment.UserName}";

        /// <summary>Thư mục làm việc cục bộ đồng bộ chính xác theo liên kết động trên máy người dùng</summary>
        public string LastExcelFolder { get; set; } = $@"C:\MacGregor_CAS_WF\Designs\90 Users\{Environment.UserName}";

        /// <summary>Bộ môn thiết kế được lựa chọn gần nhất ở combobox</summary>
        public string LastSelectedDisciplineTag { get; set; } = "Temp Checklist - LayoutInterface.xlsx";
    }
}