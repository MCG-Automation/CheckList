using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Autodesk.Connectivity.WebServices;
using Autodesk.DataManagement.Client.Framework.Vault.Currency.Connections;
using Autodesk.DataManagement.Client.Framework.Vault.Currency.Properties;
using Autodesk.DataManagement.Client.Framework.Vault.Settings;
using VDF = Autodesk.DataManagement.Client.Framework;
using MCGCadPlugin.Models.CheckList;

namespace MCGCadPlugin.Services.CheckList
{
    /// <summary>
    /// Dịch vụ kết nối và đồng bộ hóa tệp tin tự động từ Autodesk Vault Server.
    /// Sử dụng Autodesk ID Auth, tự động phân giải Working Folder và hỗ trợ chế độ làm việc ngoại tuyến (Offline Mode Fallback).
    /// </summary>
    public class VaultSyncService : IVaultSyncService
    {
        #region Fields
        private const string LOG_PREFIX = "[VaultSyncService]";
        #endregion

        #region Public Methods
        /// <summary>
        /// Đồng bộ tệp tin Excel từ Vault Server xuống thư mục làm việc cục bộ của kỹ sư.
        /// </summary>
        public string SyncExcelFile(string fileName, ChecklistSettings settings)
        {
            Debug.WriteLine($"{LOG_PREFIX} Initiating synchronization for file: {fileName}");

            if (settings == null)
            {
                Debug.WriteLine($"{LOG_PREFIX} LỖI: Cấu hình cài đặt rỗng (null).");
                return GetFallbackLocalPath(fileName, settings);
            }

            Connection connection = null;
            try
            {
                // 1. Khởi tạo kết nối tới Vault Server sử dụng Autodesk ID Authentication token ngầm định từ AutoCAD
                Debug.WriteLine($"{LOG_PREFIX} Connecting to Vault Server '{settings.VaultServer}'...");
                
                VDF.Vault.Results.LogInResult logInResult = VDF.Vault.Library.ConnectionManager.LogIn(
                    settings.VaultServer,
                    settings.VaultName,
                    null, // IAutodeskAccount: null = dùng phiên Autodesk ID hiện hành (SSO từ AutoCAD)
                    Autodesk.DataManagement.Client.Framework.Vault.Currency.Connections.AuthenticationFlags.AutodeskAuthentication,
                    null  // Func<string, LoginStates, bool> callback: null = không cần báo cáo tiến trình
                );

                // Connection được trả về qua logInResult.Connection, không phải out parameter
                connection = logInResult?.Connection;

                if (logInResult == null || !logInResult.Success || connection == null)
                {
                    Debug.WriteLine($"{LOG_PREFIX} Connection failed: Invalid Autodesk ID authentication token or Vault Server offline.");
                    return GetFallbackLocalPath(fileName, settings);
                }

                Debug.WriteLine($"{LOG_PREFIX} Connected to Vault successfully.");

                // 2. Truy vấn động đường dẫn thư mục làm việc cục bộ (Local Working Folder Mapping) qua API
                string localWorkingFolder = string.Empty;
                try
                {
                    // GetWorkingFolder() trả về FolderPathAbsolute, cần gọi .ToString() để lấy chuỗi đường dẫn
                    var workingFolderPath = connection.WorkingFoldersManager.GetWorkingFolder(settings.VaultExcelFolderPath);
                    localWorkingFolder = workingFolderPath?.ToString() ?? string.Empty;
                    Debug.WriteLine($"{LOG_PREFIX} Dynamic Working Folder resolved: {localWorkingFolder}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"{LOG_PREFIX} Unable to resolve Working Folder via API: {ex.Message}. Falling back to default.");
                    localWorkingFolder = settings.LastExcelFolder;
                }

                if (string.IsNullOrEmpty(localWorkingFolder))
                {
                    localWorkingFolder = settings.LastExcelFolder;
                }

                string finalLocalFilePath = Path.Combine(localWorkingFolder, fileName);

                // 3. Tiến hành tìm kiếm tệp tin tương đối trong Vault Server
                string vaultRelativeFilePath = CombineVaultPath(settings.VaultExcelFolderPath, fileName);
                Debug.WriteLine($"{LOG_PREFIX} Looking for Vault file: {vaultRelativeFilePath}");

                // Định nghĩa đầy đủ namespace tránh xung đột lớp tĩnh System.IO.File
                Autodesk.Connectivity.WebServices.File vaultFile = FindVaultFile(connection, vaultRelativeFilePath);
                if (vaultFile == null)
                {
                    Debug.WriteLine($"{LOG_PREFIX} File '{fileName}' not found in Vault Folder: {settings.VaultExcelFolderPath}");
                    return GetFallbackLocalPath(fileName, settings);
                }

                // 4. Gọi lệnh "Get Latest Version" của Vault API và ghi đè cưỡng bức xuống đĩa cục bộ
                Debug.WriteLine($"{LOG_PREFIX} Downloading the latest version from Vault...");
                
                // Khởi tạo cài đặt nạp tệp tin tiêu chuẩn
                var acquireSettings = new Autodesk.DataManagement.Client.Framework.Vault.Settings.AcquireFilesSettings(connection);

                // Thiết lập tùy chọn ghi đè cưỡng bức (Force Overwrite) bằng thuộc tính OptionsResolution.OverwriteOption
                // Sử dụng tùy chọn ghi đè "Always" tương thích tối đa với bộ thư viện SDK 2023/2024
                acquireSettings.OptionsResolution.OverwriteOption = 
                    Autodesk.DataManagement.Client.Framework.Vault.Settings.AcquireFilesSettings.AcquireFileResolutionOptions.OverwriteOptions.ForceOverwriteAll;

                // Tạo đối tượng FileIteration tương ứng với tệp tin ảo trong Vault
                var fileIteration = new Autodesk.DataManagement.Client.Framework.Vault.Currency.Entities.FileIteration(connection, vaultFile);
                
                // Thêm tệp tin vào hàng đợi tải và chỉ định rõ intent là Download (Thao tác truyền 2 tham số chuẩn SDK)
                acquireSettings.AddFileToAcquire(
                    fileIteration, 
                    Autodesk.DataManagement.Client.Framework.Vault.Settings.AcquireFilesSettings.AcquisitionOption.Download
                );

                // Thực thi đồng bộ tải tệp tin từ Vault Server
                connection.FileManager.AcquireFiles(acquireSettings);
                Debug.WriteLine($"{LOG_PREFIX} File sync completed successfully: {finalLocalFilePath}");

                return finalLocalFilePath;
            }
            catch (Exception ex)
            {
                // OFFLINE FALLBACK: Nếu xảy ra bất kỳ lỗi gì (Lỗi mạng, Vault Server sập, tệp cục bộ bị khóa do đang mở Excel)
                Debug.WriteLine($"{LOG_PREFIX} WARNING: Vault synchronization failed: {ex.Message}");
                Debug.WriteLine($"{LOG_PREFIX} Transitioning to OFFLINE MODE fallback.");

                return GetFallbackLocalPath(fileName, settings);
            }
            finally
            {
                // Ngắt kết nối an toàn sau khi hoàn thành phiên làm việc với Vault
                if (connection != null && connection.IsConnected)
                {
                    VDF.Vault.Library.ConnectionManager.LogOut(connection);
                    Debug.WriteLine($"{LOG_PREFIX} Logged out from Vault successfully.");
                }
            }
        }
        #endregion

        #region Helpers
        /// <summary>
        /// Tìm đối tượng tệp tin trong hệ thống Vault Server dựa trên đường dẫn ảo.
        /// Sử dụng chỉ định namespace đầy đủ để triệt tiêu hoàn toàn sự xung đột với System.IO.File
        /// </summary>
        private Autodesk.Connectivity.WebServices.File FindVaultFile(Connection connection, string vaultFilePath)
        {
            try
            {
                // Phân giải đường dẫn và tìm kiếm tệp tin thông qua Web Service Document Service
                Autodesk.Connectivity.WebServices.File[] files = connection.WebServiceManager.DocumentService.FindLatestFilesByPaths(new string[] { vaultFilePath });
                if (files != null && files.Length > 0 && files[0] != null && files[0].Id > 0)
                {
                    return files[0];
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{LOG_PREFIX} Error querying Vault file metadata: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Tạo đường dẫn tệp tin cục bộ dự phòng trong trường hợp ngoại tuyến
        /// </summary>
        private string GetFallbackLocalPath(string fileName, ChecklistSettings settings)
        {
            string fallbackFolder = settings != null && !string.IsNullOrEmpty(settings.LastExcelFolder)
                ? settings.LastExcelFolder
                : @"C:\MacGregor_CAS_WF\Designs\90 Users\truonph";

            string fallbackPath = Path.Combine(fallbackFolder, fileName);
            Debug.WriteLine($"{LOG_PREFIX} Fallback path resolved: {fallbackPath}");

            return fallbackPath;
        }

        /// <summary>
        /// Kết hợp an toàn đường dẫn Vault Server ảo
        /// </summary>
        private string CombineVaultPath(string folderPath, string fileName)
        {
            if (string.IsNullOrEmpty(folderPath)) return "$/" + fileName;
            
            string normalizedFolder = folderPath.Replace('\\', '/');
            if (!normalizedFolder.EndsWith("/"))
            {
                normalizedFolder += "/";
            }
            return normalizedFolder + fileName;
        }
        #endregion
    }
}