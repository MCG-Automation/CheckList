using System;
using System.Diagnostics;
using System.IO;
using System.Xml;
using Autodesk.Connectivity.WebServices;
using Autodesk.DataManagement.Client.Framework.Vault.Currency.Connections;
using Autodesk.DataManagement.Client.Framework.Vault.Settings;
using VDF = Autodesk.DataManagement.Client.Framework;
using VltFormsSettings = Autodesk.DataManagement.Client.Framework.Vault.Forms.Settings;
using VltFormsLib = Autodesk.DataManagement.Client.Framework.Vault.Forms.Library;
using MCG_CheckList.Models.CheckList;
using MCG_CheckList.Utilities;

namespace MCG_CheckList.Services.CheckList
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
        public VaultSyncResult SyncExcelFile(string fileName, ChecklistSettings settings)
        {
            FileLogger.LogSessionStart("VaultSyncService.SyncExcelFile");
            FileLogger.Log(LOG_PREFIX, $"fileName={fileName}");

            if (settings == null)
            {
                FileLogger.Log(LOG_PREFIX, "ABORT: settings is null");
                return new VaultSyncResult
                {
                    LocalPath = GetFallbackLocalPath(fileName, settings),
                    SyncedFromVault = false,
                    ErrorMessage = "Cấu hình settings bị null."
                };
            }

            // Log toàn bộ settings để xác nhận giá trị thực tế đang dùng
            FileLogger.Log(LOG_PREFIX, $"settings.VaultServer (raw) = '{settings.VaultServer}'");
            FileLogger.Log(LOG_PREFIX, $"settings.VaultName         = '{settings.VaultName}'");
            FileLogger.Log(LOG_PREFIX, $"settings.VaultExcelFolderPath = '{settings.VaultExcelFolderPath}'");
            FileLogger.Log(LOG_PREFIX, $"settings.LastExcelFolder   = '{settings.LastExcelFolder}'");
            FileLogger.Log(LOG_PREFIX, $"settings.json path         = '{ChecklistAppDataPaths.SettingsFile}'");

            // 0. Ưu tiên đọc server/vault từ LoginHistory.xml (giống FittingManagement)
            //    Bypass settings.json có thể bị stale — lấy đúng phiên đang active trên máy
            string lhServer, lhVault;
            TryReadLoginHistory(out lhServer, out lhVault);

            string serverName;
            string vaultName;
            if (!string.IsNullOrEmpty(lhServer) && !string.IsNullOrEmpty(lhVault))
            {
                serverName = lhServer;
                vaultName  = lhVault;
                FileLogger.Log(LOG_PREFIX, $"LoginHistory.xml override → server='{serverName}', vault='{vaultName}'");
            }
            else
            {
                // Fallback về settings.json — strip URL prefix nếu có
                serverName = settings.VaultServer ?? string.Empty;
                if (serverName.ToLower().StartsWith("https://")) serverName = serverName.Substring(8);
                else if (serverName.ToLower().StartsWith("http://")) serverName = serverName.Substring(7);
                serverName = serverName.TrimEnd('/');
                vaultName  = settings.VaultName;
                FileLogger.Log(LOG_PREFIX, $"settings.json fallback → server='{serverName}', vault='{vaultName}'");
            }

            FileLogger.Log(LOG_PREFIX, $"Final serverName = '{serverName}'");
            FileLogger.Log(LOG_PREFIX, $"Final vaultName  = '{vaultName}'");
            Connection connection = null;
            try
            {
                // 1. Dùng VltFormsLib.Login với AutoLoginMode=RestoreAndExecute
                //    Giống FittingManagement/VaultDirectService: restore cached session từ Vault Client,
                //    không cần pass credentials — SDK tự lấy session đang active trên máy.
                FileLogger.Log(LOG_PREFIX, "Calling VltFormsLib.Login (AutoLoginMode=RestoreAndExecute) ...");
                var loginSettings = new VltFormsSettings.LoginSettings
                {
                    ServerName = serverName,
                    VaultName  = vaultName,
                    AutoLoginMode = VltFormsSettings.LoginSettings.AutoLoginModeValues.RestoreAndExecute
                };

                connection = VltFormsLib.Login(loginSettings);

                FileLogger.Log(LOG_PREFIX, $"VltFormsLib.Login → Connection={(connection == null ? "null" : "non-null")}");

                if (connection == null)
                    throw new Exception($"Vault login failed (Forms.Library.Login returned null). Server='{serverName}', Vault='{vaultName}'.");

                if (!connection.IsConnected)
                {
                    FileLogger.Log(LOG_PREFIX, $"Connection non-null nhưng IsConnected=false.");
                    throw new Exception($"Vault login failed (not connected). Server='{serverName}', Vault='{vaultName}'.");
                }

                FileLogger.Log(LOG_PREFIX, $"Login SUCCESS — {connection.Server}/{connection.Vault}, User={connection.UserName} (ID={connection.UserID}).");

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
                Debug.WriteLine($"{LOG_PREFIX} Get Latest THÀNH CÔNG: {finalLocalFilePath}");
                return new VaultSyncResult
                {
                    LocalPath = finalLocalFilePath,
                    SyncedFromVault = true
                };
            }
            catch (Exception ex)
            {
                string errorDetail = $"SyncExcelFile failed for '{fileName}': {ex.Message}";
                Debug.WriteLine($"{LOG_PREFIX} WARNING: {errorDetail}");
                FileLogger.Log(LOG_PREFIX, $"EXCEPTION caught: {ex.GetType().Name}: {ex.Message}");
                FileLogger.LogException(LOG_PREFIX, errorDetail, ex);

                string fallback = GetFallbackLocalPath(fileName, settings);
                FileLogger.Log(LOG_PREFIX, $"Using fallback path: '{fallback}'");
                return new VaultSyncResult
                {
                    LocalPath = fallback,
                    SyncedFromVault = false,
                    ErrorMessage = ex.Message
                };
            }
            finally
            {
                try
                {
                    if (connection != null && connection.IsConnected)
                        VDF.Vault.Library.ConnectionManager.LogOut(connection);
                }
                catch (Exception exLogout)
                {
                    Debug.WriteLine($"{LOG_PREFIX} LogOut warning (non-fatal): {exLogout.Message}");
                }
            }
        }
        #endregion

        #region Helpers
        /// <summary>
        /// Đọc LoginHistory.xml để lấy server/vault từ phiên Vault đang active trên máy.
        /// Cùng phương pháp FittingManagement dùng — tránh phụ thuộc settings.json có thể stale.
        /// </summary>
        private void TryReadLoginHistory(out string server, out string vault)
        {
            server = null;
            vault  = null;
            try
            {
                string historyPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Autodesk", "VaultCommon", "LoginHistory.xml");

                FileLogger.Log(LOG_PREFIX, $"Reading LoginHistory.xml: {historyPath}");

                if (!System.IO.File.Exists(historyPath))
                {
                    FileLogger.Log(LOG_PREFIX, "LoginHistory.xml not found.");
                    return;
                }

                var xdoc = new XmlDocument();
                xdoc.Load(historyPath);

                var serverNode = xdoc.SelectSingleNode("//LastServerName");
                var vaultNode  = xdoc.SelectSingleNode("//LastVault");

                string s = serverNode?.InnerText?.Trim();
                string v = vaultNode?.InnerText?.Trim();

                if (!string.IsNullOrEmpty(s) && !string.IsNullOrEmpty(v))
                {
                    server = s;
                    vault  = v;
                    FileLogger.Log(LOG_PREFIX, $"LoginHistory.xml → server='{server}', vault='{vault}'");
                }
                else
                {
                    FileLogger.Log(LOG_PREFIX, $"LoginHistory.xml parsed but values empty. serverNode='{s}', vaultNode='{v}'");
                }
            }
            catch (Exception ex)
            {
                FileLogger.Log(LOG_PREFIX, $"TryReadLoginHistory failed: {ex.Message}");
            }
        }

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
                    fallbackFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MCG_CheckList", "Cache");
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