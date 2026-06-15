using System;
using System.Diagnostics;
using System.IO;
using Autodesk.Connectivity.WebServices;
using Autodesk.DataManagement.Client.Framework.Vault.Currency.Connections;
using Autodesk.DataManagement.Client.Framework.Vault.Settings;
using VDF = Autodesk.DataManagement.Client.Framework;
using MCGCadPlugin.Models.CheckList;
using MCGCadPlugin.Utilities; // Nạp thêm bộ tiện ích ghi Log file công ty

namespace MCGCadPlugin.Services.CheckList
{
    /// <summary>
    /// Dịch vụ kết nối và đồng bộ hóa tệp tin tự động từ Autodesk Vault Server.
    /// </summary>
    public class VaultSyncService : IVaultSyncService
    {
        #region Fields
        private const string LOG_PREFIX = "[VaultSyncService]";
        #endregion

        #region Public Methods
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
                // 1. Kết nối tới Vault Server sử dụng Autodesk ID Authentication (SSO từ AutoCAD)
                VDF.Vault.Results.LogInResult logInResult = VDF.Vault.Library.ConnectionManager.LogIn(
                    settings.VaultServer,
                    settings.VaultName,
                    null, 
                    null,
                    AuthenticationFlags.AutodeskAuthentication,
                    null  
                );

                connection = logInResult?.Connection;

                if (logInResult == null || !logInResult.Success || connection == null)
                {
                    throw new Exception("Đăng nhập Vault thất bại. Phiên Token Autodesk ID hết hạn hoặc sai thông số Server.");
                }

                // 2. Truy vấn động đường dẫn thư mục làm việc cục bộ qua API
                string localWorkingFolder = string.Empty;
                try
                {
                    var workingFolderPath = connection.WorkingFoldersManager.GetWorkingFolder(settings.VaultExcelFolderPath);
                    localWorkingFolder = workingFolderPath?.ToString() ?? string.Empty;
                }
                catch
                {
                    localWorkingFolder = settings.LastExcelFolder;
                }

                if (string.IsNullOrEmpty(localWorkingFolder))
                {
                    localWorkingFolder = settings.LastExcelFolder;
                }

                if (!Directory.Exists(localWorkingFolder))
                {
                    Directory.CreateDirectory(localWorkingFolder);
                }

                string finalLocalFilePath = Path.Combine(localWorkingFolder, fileName);
                string vaultRelativeFilePath = CombineVaultPath(settings.VaultExcelFolderPath, fileName);

                // 3. Tìm kiếm tệp tin trong Vault Server
                Autodesk.Connectivity.WebServices.File vaultFile = FindVaultFile(connection, vaultRelativeFilePath);
                if (vaultFile == null)
                {
                    throw new FileNotFoundException($"Không tìm thấy tệp '{fileName}' tại đường dẫn Vault: {settings.VaultExcelFolderPath}");
                }

                // 4. Thực hiện Get Latest và cưỡng bức ghi đè (Force Overwrite)
                var acquireSettings = new AcquireFilesSettings(connection);
                acquireSettings.OptionsResolution.OverwriteOption = 
                    AcquireFilesSettings.AcquireFileResolutionOptions.OverwriteOptions.ForceOverwriteAll;

                var fileIteration = new VDF.Vault.Currency.Entities.FileIteration(connection, vaultFile);
                acquireSettings.AddFileToAcquire(fileIteration, AcquireFilesSettings.AcquisitionOption.Download);

                // Thực thi tải file từ Vault
                connection.FileManager.AcquireFiles(acquireSettings);
                
                settings.LastExcelFolder = localWorkingFolder;
                return finalLocalFilePath;
            }
            catch (Exception ex)
            {
                // CHUẨN HÓA KIẾN TRÚC: Ghi nhận lỗi minh bạch vào hệ thống File Log để debug theo Cách 1 công ty
                string errorDetail = $"SyncExcelFile failed for '{fileName}': {ex.Message}";
                Debug.WriteLine($"{LOG_PREFIX} WARNING: {errorDetail}");
                FileLogger.LogException(LOG_PREFIX, errorDetail, ex);

                // Tự động trả về vùng đệm ngoại tuyến để không làm sập ứng dụng của kỹ sư
                return GetFallbackLocalPath(fileName, settings);
            }
            finally
            {
                if (connection != null && connection.IsConnected)
                {
                    VDF.Vault.Library.ConnectionManager.LogOut(connection);
                }
            }
        }
        #endregion

        #region Helpers
        private Autodesk.Connectivity.WebServices.File FindVaultFile(Connection connection, string vaultFilePath)
        {
            try
            {
                Autodesk.Connectivity.WebServices.File[] files = connection.WebServiceManager.DocumentService.FindLatestFilesByPaths(new string[] { vaultFilePath });
                if (files != null && files.Length > 0 && files[0] != null && files[0].Id > 0)
                {
                    return files[0];
                }
            }
            catch {}
            return null;
        }

        private string GetFallbackLocalPath(string fileName, ChecklistSettings settings)
        {
            string fallbackFolder = ChecklistAppDataPaths.GetDefaultDesignPath();

            if (!Directory.Exists(fallbackFolder))
            {
                try { Directory.CreateDirectory(fallbackFolder); }
                catch {
                    fallbackFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MCGCadPlugin", "Cache");
                    Directory.CreateDirectory(fallbackFolder);
                }
            }
            return Path.Combine(fallbackFolder, fileName);
        }

        private string CombineVaultPath(string folderPath, string fileName)
        {
            if (string.IsNullOrEmpty(folderPath)) return "$/" + fileName;
            string normalizedFolder = folderPath.Replace('\\', '/');
            if (!normalizedFolder.EndsWith("/")) normalizedFolder += "/";
            return normalizedFolder + fileName;
        }
        #endregion
    }
}