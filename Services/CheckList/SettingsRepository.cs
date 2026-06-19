using System;
using System.Diagnostics;
using System.IO;
using Newtonsoft.Json;
using MCG_CheckList.Models.CheckList;

namespace MCG_CheckList.Services.CheckList
{
    /// <summary>
    /// Kho lưu trữ và xử lý đọc/ghi cấu hình settings.json của ứng dụng.
    /// Áp dụng kỹ thuật ghi tệp nguyên tử (Atomic Write) an toàn tuyệt đối chống hỏng tệp tin.
    /// </summary>
    public class SettingsRepository
    {
        #region Fields
        private const string LOG_PREFIX = "[SettingsRepository]";
        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore
        };
        #endregion

        #region Public Methods
        /// <summary>
        /// Nạp dữ liệu cấu hình từ settings.json lên bộ nhớ. Trả về cấu hình mặc định nếu tệp chưa tồn tại hoặc bị lỗi.
        /// </summary>
        public ChecklistSettings Load()
        {
            var path = ChecklistAppDataPaths.SettingsFile;
            Debug.WriteLine($"{LOG_PREFIX} Đang tải tệp cấu hình tại: {path}");

            if (!File.Exists(path))
            {
                Debug.WriteLine($"{LOG_PREFIX} Không tồn tại tệp tin cấu hình. Tiến hành khởi tạo và lưu cấu hình mặc định.");
                var defaultSettings = new ChecklistSettings();
                Save(defaultSettings);
                return defaultSettings;
            }

            try
            {
                var json = File.ReadAllText(path);
                var settings = JsonConvert.DeserializeObject<ChecklistSettings>(json, JsonSettings);
                return settings ?? new ChecklistSettings();
            }
            catch (JsonException ex)
            {
                Debug.WriteLine($"{LOG_PREFIX} LỖI cú pháp JSON tệp cấu hình settings.json, tiến hành khôi phục mặc định: {ex.Message}");
                return new ChecklistSettings();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{LOG_PREFIX} LỖI không xác định khi nạp cấu hình settings.json: {ex.Message}");
                return new ChecklistSettings();
            }
        }

        /// <summary>
        /// Ghi đè cấu hình hiện tại xuống settings.json một cách an toàn bằng luồng tệp tin tạm và dự phòng
        /// </summary>
        public void Save(ChecklistSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            var path = ChecklistAppDataPaths.SettingsFile;
            Debug.WriteLine($"{LOG_PREFIX} Đang ghi đè tệp cấu hình một cách an toàn xuống: {path}");

            try
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                var tmp = path + ".tmp";
                File.WriteAllText(tmp, JsonConvert.SerializeObject(settings, JsonSettings));

                if (File.Exists(path))
                {
                    File.Replace(tmp, path, path + ".bak", ignoreMetadataErrors: true);
                }
                else
                {
                    File.Move(tmp, path);
                }
                Debug.WriteLine($"{LOG_PREFIX} Ghi tệp cấu hình settings.json thành công.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{LOG_PREFIX} LỖI nghiêm trọng khi ghi tệp cấu hình settings.json xuống đĩa: {ex.Message}");
                throw;
            }
        }
        #endregion
    }
}