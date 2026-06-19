using System;
using System.IO;

namespace MCG_CheckList.Services.CheckList
{
    /// <summary>
    /// Bộ quản lý các đường dẫn tệp tin và thư mục lưu trữ cache của MCG_CheckList trong %APPDATA%
    /// </summary>
    public static class ChecklistAppDataPaths
    {
        #region Properties
        /// <summary>Set non-null trong unit test để chuyển hướng cache sang thư mục tạm</summary>
        public static string RootOverride { get; set; }

        public static string Root
        {
            get
            {
                if (!string.IsNullOrEmpty(RootOverride)) return RootOverride;
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                return Path.Combine(appData, "MCG_CheckList");
            }
        }

        /// <summary>
        /// Đường dẫn thư mục dùng chung chứa file Excel template — cố định cho mọi user trên mạng CAS.
        /// </summary>
        public static readonly string SharedTemplatePath = @"C:\MacGregor_CAS_WF\Designs\90 Users\truonph";

        /// <summary>
        /// Trả về đường dẫn thư mục chứa Excel template dùng chung (cố định, không theo tên user).
        /// </summary>
        public static string GetDefaultDesignPath()
        {
            if (!Directory.Exists(SharedTemplatePath))
            {
                try { Directory.CreateDirectory(SharedTemplatePath); }
                catch { /* không tạo được thì để parser tự xử lý */ }
            }
            return SharedTemplatePath;
        }

        public static string SettingsFile => Path.Combine(Root, "settings.json");

        public static string ChecklistsFolder => Path.Combine(Root, "Checklists");
        #endregion

        #region Public Methods
        /// <summary>
        /// Trả về đường dẫn thư mục lưu trữ của riêng cấu kiện (Panel) cụ thể
        /// </summary>
        public static string PanelFolder(string projectNo, string panelName)
        {
            if (string.IsNullOrWhiteSpace(projectNo)) projectNo = "UnknownProject";
            if (string.IsNullOrWhiteSpace(panelName)) panelName = "UnknownPanel";
            
            return Path.Combine(ChecklistsFolder, $"{Sanitize(projectNo)}_{Sanitize(panelName)}");
        }

        /// <summary>
        /// Trả về đường dẫn tệp tin cache JSON chứa checklist của bộ môn tương ứng trong Panel
        /// </summary>
        public static string ChecklistFile(string projectNo, string panelName, string discipline)
        {
            if (string.IsNullOrWhiteSpace(discipline)) discipline = "General";
            var folder = PanelFolder(projectNo, panelName);
            return Path.Combine(folder, $"checklist_{Sanitize(discipline)}.json");
        }

        /// <summary>
        /// Đảm bảo thư mục lưu trữ Panel tồn tại trên hệ thống đĩa cứng
        /// </summary>
        public static void EnsurePanelFolder(string projectNo, string panelName)
        {
            Directory.CreateDirectory(PanelFolder(projectNo, panelName));
        }
        #endregion

        #region Helpers
        /// <summary>
        /// Loại bỏ các ký tự đặc biệt không hợp lệ của tên tệp tin trên Windows
        /// </summary>
        private static string Sanitize(string s)
        {
            if (string.IsNullOrEmpty(s)) return "_";
            foreach (var c in Path.GetInvalidFileNameChars())
            {
                s = s.Replace(c, '_');
            }
            return s;
        }
        #endregion
    }
}