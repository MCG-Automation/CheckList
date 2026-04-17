using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using MCGCadPlugin.Models.FittingManagement;
using MCGCadPlugin.Utilities;

namespace MCGCadPlugin.Services.FittingManagement
{
    /// <summary>
    /// Xử lý trích xuất geometry và metadata từ file IDW của Inventor qua COM Interop.
    /// Kết quả: file DWG + JSON được lưu vào thư mục thư viện.
    /// </summary>
    public partial class FittingManagementService
    {
        /// <summary>
        /// Import hàng loạt file .idw từ Inventor, trích xuất metadata và export DWG.
        /// Yêu cầu: Inventor phải được cài đặt trên máy.
        /// </summary>
        /// <param name="idwPaths">Danh sách đường dẫn file .idw</param>
        /// <returns>ImportResult chứa số liệu và chi tiết lỗi</returns>
        public ImportResult BatchImportIdwFiles(string[] idwPaths)
        {
            var result = new ImportResult();
            FileLogger.LogSessionStart($"BatchImportIdwFiles ({idwPaths.Length} files)");
            FileLogger.Log(LOG_PREFIX, $"Bắt đầu BatchImportIdwFiles — {idwPaths.Length} file(s)...");
            Debug.WriteLine($"{LOG_PREFIX} Bắt đầu BatchImportIdwFiles — {idwPaths.Length} file(s)...");

            bool weStartedInventor = false;
            dynamic invApp = null;

            try
            {
                // 1. Khởi tạo Inventor COM — dùng instance đang chạy hoặc tạo mới
                invApp = AcquireInventorInstance(out weStartedInventor);

                // 2. Đảm bảo thư mục output tồn tại
                if (!Directory.Exists(_libraryFolderPath))
                {
                    Directory.CreateDirectory(_libraryFolderPath);
                    FileLogger.Log(LOG_PREFIX, $"Đã tạo thư mục: {_libraryFolderPath}");
                }

                // 3. Xử lý từng file IDW
                foreach (string idwPath in idwPaths)
                {
                    string fileName = Path.GetFileName(idwPath);
                    try
                    {
                        ProcessSingleIdwFile(invApp, idwPath);
                        result.SuccessCount++;
                        FileLogger.Log(LOG_PREFIX, $"Import IDW THÀNH CÔNG: {fileName}");
                        Debug.WriteLine($"{LOG_PREFIX} Import IDW THÀNH CÔNG: {fileName}");
                    }
                    catch (System.Exception ex)
                    {
                        result.FailCount++;
                        FileLogger.LogException(LOG_PREFIX, $"import IDW '{fileName}'", ex);
                        Debug.WriteLine($"{LOG_PREFIX} LỖI import IDW '{fileName}': {ex.Message}");
                        result.AddError(fileName, $"{ex.GetType().Name}: {ex.Message}");
                    }
                }
            }
            catch (System.Exception ex)
            {
                FileLogger.LogException(LOG_PREFIX, "BatchImportIdwFiles (outer)", ex);
                Debug.WriteLine($"{LOG_PREFIX} LỖI BatchImportIdwFiles: {ex.Message}");
                throw;
            }
            finally
            {
                // 4. Giải phóng COM — chỉ Quit nếu chính ta khởi tạo Inventor
                ReleaseInventorInstance(invApp, weStartedInventor);
            }

            FileLogger.Log(LOG_PREFIX, $"HOÀN TẤT — Thành công: {result.SuccessCount}, Thất bại: {result.FailCount}.");
            Debug.WriteLine($"{LOG_PREFIX} BatchImportIdwFiles HOÀN TẤT — Thành công: {result.SuccessCount}, Thất bại: {result.FailCount}.");
            return result;
        }

        #region IDW Import — Private Helpers

        /// <summary>
        /// Kết nối hoặc khởi tạo Inventor COM Application.
        /// </summary>
        private dynamic AcquireInventorInstance(out bool weStarted)
        {
            weStarted = false;
            dynamic invApp;

            try
            {
                // Thử kết nối instance đang chạy
                invApp = Marshal.GetActiveObject("Inventor.Application");
                FileLogger.Log(LOG_PREFIX, "Đã kết nối Inventor instance đang chạy.");
            }
            catch
            {
                // Inventor chưa chạy — khởi tạo mới
                Type invType = Type.GetTypeFromProgID("Inventor.Application");
                if (invType == null)
                {
                    FileLogger.Log(LOG_PREFIX, "LỖI: Inventor chưa được cài đặt trên máy.");
                    throw new InvalidOperationException(
                        "Inventor chưa được cài đặt trên máy này. Vui lòng cài Inventor để sử dụng tính năng Import IDW.");
                }

                invApp = Activator.CreateInstance(invType);
                invApp.Visible = false;
                weStarted = true;
                FileLogger.Log(LOG_PREFIX, "Đã khởi tạo Inventor instance mới (background).");
            }

            return invApp;
        }

        /// <summary>
        /// Giải phóng Inventor COM objects an toàn.
        /// </summary>
        private void ReleaseInventorInstance(dynamic invApp, bool weStarted)
        {
            if (invApp == null) return;

            try
            {
                if (weStarted)
                {
                    invApp.Quit();
                    FileLogger.Log(LOG_PREFIX, "Đã tắt Inventor instance.");
                }
                Marshal.ReleaseComObject(invApp);
            }
            catch (System.Exception ex)
            {
                FileLogger.LogException(LOG_PREFIX, "giải phóng Inventor COM", ex);
            }
        }

        /// <summary>
        /// Xử lý 1 file IDW: trích xuất metadata, export DWG, lưu JSON.
        /// </summary>
        private void ProcessSingleIdwFile(dynamic invApp, string idwPath)
        {
            FileLogger.Log(LOG_PREFIX, $"Đang xử lý: {Path.GetFileName(idwPath)}...");

            string baseName = Path.GetFileNameWithoutExtension(idwPath);
            string dwgOutputPath = Path.Combine(_libraryFolderPath, baseName + ".dwg");
            string jsonOutputPath = Path.Combine(_libraryFolderPath, baseName + ".json");

            dynamic drawingDoc = null;
            try
            {
                // Mở file IDW (OpenVisible = TRUE — bắt buộc để SaveCopyAs có thể render views)
                // Inventor app đã được ẩn (invApp.Visible = false), nhưng document cần có window nội bộ
                FileLogger.Log(LOG_PREFIX, $"  Bước 1/4: Đang mở file IDW (OpenVisible=true)...");
                drawingDoc = invApp.Documents.Open(idwPath, true);

                // Trích xuất iProperties
                FileLogger.Log(LOG_PREFIX, $"  Bước 2/4: Đang trích xuất iProperties...");
                FittingMetadata metadata = ExtractIProperties(drawingDoc);

                // Trích xuất thông tin các Drawing Views
                FileLogger.Log(LOG_PREFIX, $"  Bước 3/4: Đang trích xuất Drawing Views...");
                metadata.Views = ExtractDrawingViews(drawingDoc);

                // Export DWG
                FileLogger.Log(LOG_PREFIX, $"  Bước 4/4: Đang export DWG tới {dwgOutputPath}...");
                ExportIdwToDwg(invApp, drawingDoc, dwgOutputPath);

                // Lưu metadata ra JSON
                string json = JsonConvert.SerializeObject(metadata, Formatting.Indented);
                File.WriteAllText(jsonOutputPath, json);
                FileLogger.Log(LOG_PREFIX, $"  Đã lưu JSON: {jsonOutputPath}");
            }
            finally
            {
                // Đóng document không lưu
                if (drawingDoc != null)
                {
                    try
                    {
                        drawingDoc.Close(true); // true = skip save
                        Marshal.ReleaseComObject(drawingDoc);
                    }
                    catch (System.Exception ex)
                    {
                        FileLogger.LogException(LOG_PREFIX, "đóng IDW", ex);
                    }
                }
            }
        }

        /// <summary>
        /// Trích xuất iProperties từ DrawingDocument của Inventor.
        /// </summary>
        private FittingMetadata ExtractIProperties(dynamic drawingDoc)
        {
            var metadata = new FittingMetadata();

            try
            {
                dynamic propSets = drawingDoc.PropertySets;

                // Design Tracking Properties — chứa Part Number, Description, Mass, Material, etc.
                dynamic designProps = propSets["Design Tracking Properties"];
                metadata.PartNumber = SafeGetProperty(designProps, "Part Number");
                metadata.Description = SafeGetProperty(designProps, "Description");
                metadata.Revision = SafeGetProperty(designProps, "Revision Number");
                metadata.Designer = SafeGetProperty(designProps, "Designer");
                metadata.Material = SafeGetProperty(designProps, "Material");
                metadata.Mass = SafeGetProperty(designProps, "Mass");

                // Inventor Summary Information — chứa Title
                dynamic summaryProps = propSets["Inventor Summary Information"];
                metadata.Title = SafeGetProperty(summaryProps, "Title");
            }
            catch (System.Exception ex)
            {
                FileLogger.LogException(LOG_PREFIX, "đọc iProperties", ex);
            }

            FileLogger.Log(LOG_PREFIX, $"  iProperties: PartNumber='{metadata.PartNumber}', Title='{metadata.Title}'");
            return metadata;
        }

        /// <summary>
        /// Đọc giá trị property an toàn — trả về chuỗi rỗng nếu không tồn tại.
        /// </summary>
        private string SafeGetProperty(dynamic propSet, string propName)
        {
            try
            {
                dynamic prop = propSet[propName];
                object val = prop.Value;
                if (val == null) return "";

                if (val is double dVal) return dVal.ToString("F3");
                return val.ToString();
            }
            catch
            {
                return "";
            }
        }

        /// <summary>
        /// Trích xuất thông tin các Drawing Views từ tất cả Sheets.
        /// </summary>
        private List<ViewMetadata> ExtractDrawingViews(dynamic drawingDoc)
        {
            var views = new List<ViewMetadata>();

            try
            {
                foreach (dynamic sheet in drawingDoc.Sheets)
                {
                    foreach (dynamic drawingView in sheet.DrawingViews)
                    {
                        try
                        {
                            // Inventor DrawingView.Position trả về Point2d (center của view)
                            dynamic position = drawingView.Position;
                            views.Add(new ViewMetadata
                            {
                                Name = (string)drawingView.Name,
                                CenterX = (double)position.X,
                                CenterY = (double)position.Y,
                                Width = (double)drawingView.Width,
                                Height = (double)drawingView.Height
                            });
                        }
                        catch (System.Exception ex)
                        {
                            FileLogger.LogException(LOG_PREFIX, "đọc DrawingView", ex);
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                FileLogger.LogException(LOG_PREFIX, "duyệt Sheets", ex);
            }

            FileLogger.Log(LOG_PREFIX, $"  Tìm thấy {views.Count} drawing view(s).");
            return views;
        }

        /// <summary>
        /// Export file IDW sang định dạng DWG.
        /// Chiến lược: Thử Document.SaveAs() trước (đơn giản, Inventor tự detect format).
        /// Nếu fail, fallback sang DWG Translator với INI (nếu tìm được).
        /// </summary>
        private void ExportIdwToDwg(dynamic invApp, dynamic drawingDoc, string dwgOutputPath)
        {
            // Xóa file cũ nếu tồn tại (tránh Inventor warning dialog)
            if (File.Exists(dwgOutputPath))
            {
                FileLogger.Log(LOG_PREFIX, "    Đang xóa file DWG cũ...");
                try { File.Delete(dwgOutputPath); }
                catch (System.Exception ex)
                {
                    FileLogger.LogException(LOG_PREFIX, "xóa file DWG cũ", ex);
                }
            }

            // === CHIẾN LƯỢC 1: Document.SaveAs — đơn giản nhất, không cần INI ===
            FileLogger.Log(LOG_PREFIX, "    [A1] Thử export qua drawingDoc.SaveAs(path, true)...");
            try
            {
                drawingDoc.SaveAs(dwgOutputPath, true); // true = SaveCopyAs, giữ IDW gốc
                if (File.Exists(dwgOutputPath))
                {
                    FileLogger.Log(LOG_PREFIX, $"    [A2] Export DWG THÀNH CÔNG qua SaveAs: {dwgOutputPath}");
                    return;
                }
                FileLogger.Log(LOG_PREFIX, "    [A3] SaveAs hoàn tất nhưng file không được tạo — fallback sang translator.");
            }
            catch (System.Exception exSaveAs)
            {
                FileLogger.LogException(LOG_PREFIX, "SaveAs (chiến lược 1)", exSaveAs);
                FileLogger.Log(LOG_PREFIX, "    [A3] SaveAs fail — fallback sang translator.");
            }

            // === CHIẾN LƯỢC 2: DWG Translator + INI ===
            ExportViaTranslator(invApp, drawingDoc, dwgOutputPath);
        }

        /// <summary>
        /// Fallback: Export qua DWG Translator Add-In với file INI.
        /// </summary>
        private void ExportViaTranslator(dynamic invApp, dynamic drawingDoc, string dwgOutputPath)
        {
            const string DWG_TRANSLATOR_GUID = "{C24E3AC2-122E-11D5-8E91-0010B541CD80}";

            FileLogger.Log(LOG_PREFIX, "    [B1] Đang lấy DWG Translator Add-In...");
            dynamic translatorAddin;
            try
            {
                translatorAddin = invApp.ApplicationAddIns.ItemById(DWG_TRANSLATOR_GUID);
            }
            catch (System.Exception ex)
            {
                FileLogger.LogException(LOG_PREFIX, "lấy DWG Translator Add-In", ex);
                throw new InvalidOperationException(
                    $"Không tìm thấy DWG Translator Add-In (GUID: {DWG_TRANSLATOR_GUID}).", ex);
            }
            if (translatorAddin == null)
                throw new InvalidOperationException("DWG Translator Add-In không tồn tại.");

            FileLogger.Log(LOG_PREFIX, "    [B2] Đang kích hoạt Add-In...");
            try { translatorAddin.Activate(); }
            catch (System.Exception ex)
            {
                FileLogger.Log(LOG_PREFIX, $"    Activate() warning: {ex.Message}");
            }

            dynamic transientObjects = invApp.TransientObjects;
            dynamic options = transientObjects.CreateNameValueMap();
            dynamic dataMedium = transientObjects.CreateDataMedium();
            dataMedium.FileName = dwgOutputPath;
            dynamic transContext = transientObjects.CreateTranslationContext();
            transContext.Type = 102657; // kFileBrowseIOMechanism

            // Tìm INI file — probe nhiều location kể cả INI của user tự tạo trong plugin folder
            string iniPath = FindInventorDwgIniPath();
            if (string.IsNullOrEmpty(iniPath))
            {
                // Không tìm được INI chuẩn — tạo INI tối thiểu trong %APPDATA%\MCGCadPlugin\
                iniPath = CreateMinimalDwgIni();
                FileLogger.Log(LOG_PREFIX, $"    [B3] Đã tạo INI tối thiểu: {iniPath}");
            }
            else
            {
                FileLogger.Log(LOG_PREFIX, $"    [B3] Tìm thấy DWG INI: {iniPath}");
            }

            if (!string.IsNullOrEmpty(iniPath))
            {
                try
                {
                    options.Add("Export_Acad_IniFile", iniPath);
                    FileLogger.Log(LOG_PREFIX, "    [B4] Đã set Export_Acad_IniFile.");
                }
                catch (System.Exception exAdd)
                {
                    FileLogger.Log(LOG_PREFIX, $"    [B4] Warning: {exAdd.Message}");
                }
            }

            FileLogger.Log(LOG_PREFIX, "    [B5] Đang gọi SaveCopyAs...");
            translatorAddin.SaveCopyAs(drawingDoc, transContext, options, dataMedium);
            FileLogger.Log(LOG_PREFIX, $"    [B6] Export DWG THÀNH CÔNG qua Translator: {dwgOutputPath}");
        }

        /// <summary>
        /// Tạo file INI tối thiểu cho DWG export khi không tìm thấy INI chuẩn của Inventor.
        /// Lưu tại %APPDATA%\MCGCadPlugin\DWG-AutoCAD Export.ini.
        /// </summary>
        private string CreateMinimalDwgIni()
        {
            try
            {
                string iniPath = Path.Combine(FileLogger.LogDirectory, "DWG-AutoCAD Export.ini");
                if (File.Exists(iniPath)) return iniPath;

                // Nội dung INI tối thiểu — dùng default DWG 2018 format
                string content =
                    "[System]\r\n" +
                    "Version=2\r\n" +
                    "Language=ENU\r\n" +
                    "\r\n" +
                    "[Export]\r\n" +
                    "Export_Acad_Version=27\r\n" +
                    "Export_Acad_Revision=0\r\n" +
                    "\r\n";

                File.WriteAllText(iniPath, content);
                return iniPath;
            }
            catch (System.Exception ex)
            {
                FileLogger.LogException(LOG_PREFIX, "tạo INI tối thiểu", ex);
                return null;
            }
        }

        /// <summary>
        /// Tìm đường dẫn file "DWG-AutoCAD Export.ini" trong các phiên bản Inventor đã cài.
        /// Ưu tiên phiên bản mới nhất trước.
        /// </summary>
        private string FindInventorDwgIniPath()
        {
            string publicDocs = Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments);

            // Duyệt từ phiên bản mới nhất xuống cũ nhất
            string[] versions = { "2026", "2025", "2024", "2023", "2022", "2021", "2020" };
            string[] iniNames = { "DWG-AutoCAD Export.ini", "DWG AutoCAD Export.ini" };

            foreach (string version in versions)
            {
                foreach (string iniName in iniNames)
                {
                    string path = Path.Combine(publicDocs,
                        "Autodesk", $"Inventor {version}", "Design Data", iniName);
                    if (File.Exists(path))
                        return path;
                }
            }

            // Thử luôn thư mục C:\ProgramData (một số bản Inventor để INI ở đây)
            string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            foreach (string version in versions)
            {
                foreach (string iniName in iniNames)
                {
                    string path = Path.Combine(programData,
                        "Autodesk", $"Inventor {version}", "Design Data", iniName);
                    if (File.Exists(path))
                        return path;
                }
            }

            return null;
        }

        #endregion
    }
}
