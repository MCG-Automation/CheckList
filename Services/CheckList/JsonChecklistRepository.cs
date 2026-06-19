using System;
using System.Diagnostics;
using System.IO;
using Newtonsoft.Json;
using MCG_CheckList.Models.CheckList;

namespace MCG_CheckList.Services.CheckList
{
    /// <summary>
    /// Lớp hiện thực hóa kho lưu trữ JSON.
    /// Áp dụng kỹ thuật Atomic Write và cô lập file lỗi khi đọc đĩa xảy ra ngoại lệ.
    /// </summary>
    public class JsonChecklistRepository : IChecklistRepository
    {
        #region Fields
        private const string LOG_PREFIX = "[JsonChecklistRepository]";
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            DateFormatHandling = DateFormatHandling.IsoDateFormat,
            NullValueHandling = NullValueHandling.Ignore
        };
        #endregion

        #region Public Methods
        /// <summary>
        /// Đọc tệp tin JSON lên và giải nén thành đối tượng ChecklistDocument
        /// </summary>
        public ChecklistDocument LoadChecklist(string projectNo, string panelName, string discipline)
        {
            var path = ChecklistAppDataPaths.ChecklistFile(projectNo, panelName, discipline);
            Debug.WriteLine($"{LOG_PREFIX} Đang tải bộ đệm tại: {path}");

            if (!File.Exists(path))
            {
                Debug.WriteLine($"{LOG_PREFIX} Không tồn tại tệp tin cache. Trả về null.");
                return null;
            }

            try
            {
                var json = File.ReadAllText(path);
                var doc = JsonConvert.DeserializeObject<ChecklistDocument>(json, Settings);
                Debug.WriteLine($"{LOG_PREFIX} Tải thành công Checklist từ cache.");
                return doc;
            }
            catch (JsonException ex)
            {
                Debug.WriteLine($"{LOG_PREFIX} LỖI phân tích JSON, tiến hành cô lập file lỗi để debug: {ex.Message}");
                BackupCorruptFile(path);
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{LOG_PREFIX} LỖI không mong muốn khi đọc đĩa: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Lưu hồ sơ checklist xuống tệp tin JSON một cách an toàn thông qua cơ chế Atomic Write
        /// </summary>
        public void SaveChecklist(ChecklistDocument document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));

            var path = ChecklistAppDataPaths.ChecklistFile(document.ProjectNo, document.PanelName, document.Discipline);
            Debug.WriteLine($"{LOG_PREFIX} Đang ghi đè an toàn dữ liệu checklist xuống: {path}");

            try
            {
                ChecklistAppDataPaths.EnsurePanelFolder(document.ProjectNo, document.PanelName);
                WriteJsonAtomic(path, document);
                Debug.WriteLine($"{LOG_PREFIX} Ghi cache JSON thành công.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{LOG_PREFIX} LỖI nghiêm trọng khi ghi cache xuống đĩa: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Xóa sạch toàn bộ thư mục panel và dữ liệu con của nó
        /// </summary>
        public void DeletePanelCache(string projectNo, string panelName)
        {
            var folder = ChecklistAppDataPaths.PanelFolder(projectNo, panelName);
            if (Directory.Exists(folder))
            {
                Debug.WriteLine($"{LOG_PREFIX} Đang xóa đĩa thư mục panel: {folder}");
                Directory.Delete(folder, recursive: true);
            }
        }
        #endregion

        #region Helpers
        /// <summary>
        /// Thực hiện ghi tệp tin nguyên tử (ghi ra .tmp, thay thế tệp cũ, tạo tệp .bak dự phòng)
        /// </summary>
        private static void WriteJsonAtomic(string path, object obj)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var tmp = path + ".tmp";
            var json = JsonConvert.SerializeObject(obj, Settings);
            File.WriteAllText(tmp, json);

            if (File.Exists(path))
            {
                var bak = path + ".bak";
                File.Replace(tmp, path, bak, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(tmp, path);
            }
        }

        /// <summary>
        /// Sao lưu bảo toàn tệp tin bị hỏng phục vụ chuẩn đoán lỗi
        /// </summary>
        private static void BackupCorruptFile(string path)
        {
            try
            {
                var corruptPath = path + $".corrupt.{DateTime.UtcNow:yyyyMMddHHmmss}";
                File.Copy(path, corruptPath, overwrite: false);
                Debug.WriteLine($"{LOG_PREFIX} Đã cô lập file hỏng sang: {corruptPath}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{LOG_PREFIX} Không thể cô lập file hỏng: {ex.Message}");
            }
        }
        #endregion
    }
}