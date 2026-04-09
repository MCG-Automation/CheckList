using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using MCGCadPlugin.Models.FittingManagement;

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
        /// <returns>Tuple (số file thành công, số file thất bại)</returns>
        public Tuple<int, int> BatchImportIdwFiles(string[] idwPaths)
        {
            Debug.WriteLine($"{LOG_PREFIX} Bắt đầu BatchImportIdwFiles — {idwPaths.Length} file(s)...");

            int successCount = 0;
            int failCount = 0;
            bool weStartedInventor = false;
            dynamic invApp = null;

            try
            {
                // 1. Khởi tạo Inventor COM — dùng instance đang chạy hoặc tạo mới
                invApp = AcquireInventorInstance(out weStartedInventor);

                // 2. Đảm bảo thư mục output tồn tại
                if (!Directory.Exists(_libraryFolderPath))
                    Directory.CreateDirectory(_libraryFolderPath);

                // 3. Xử lý từng file IDW
                foreach (string idwPath in idwPaths)
                {
                    try
                    {
                        ProcessSingleIdwFile(invApp, idwPath);
                        successCount++;
                        Debug.WriteLine($"{LOG_PREFIX} Import IDW THÀNH CÔNG: {Path.GetFileName(idwPath)}");
                    }
                    catch (System.Exception ex)
                    {
                        failCount++;
                        Debug.WriteLine($"{LOG_PREFIX} LỖI import IDW '{Path.GetFileName(idwPath)}': {ex.Message}");
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.WriteLine($"{LOG_PREFIX} LỖI BatchImportIdwFiles: {ex.Message}");
                Debug.WriteLine($"{LOG_PREFIX} Stack trace:\n{ex.StackTrace}");
                throw;
            }
            finally
            {
                // 4. Giải phóng COM — chỉ Quit nếu chính ta khởi tạo Inventor
                ReleaseInventorInstance(invApp, weStartedInventor);
            }

            Debug.WriteLine($"{LOG_PREFIX} BatchImportIdwFiles HOÀN TẤT — Thành công: {successCount}, Thất bại: {failCount}.");
            return new Tuple<int, int>(successCount, failCount);
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
                Debug.WriteLine($"{LOG_PREFIX} Đã kết nối Inventor instance đang chạy.");
            }
            catch
            {
                // Inventor chưa chạy — khởi tạo mới
                Type invType = Type.GetTypeFromProgID("Inventor.Application");
                if (invType == null)
                    throw new InvalidOperationException(
                        "Inventor chưa được cài đặt trên máy này. Vui lòng cài Inventor để sử dụng tính năng Import IDW.");

                invApp = Activator.CreateInstance(invType);
                invApp.Visible = false;
                weStarted = true;
                Debug.WriteLine($"{LOG_PREFIX} Đã khởi tạo Inventor instance mới (background).");
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
                    Debug.WriteLine($"{LOG_PREFIX} Đã tắt Inventor instance.");
                }
                Marshal.ReleaseComObject(invApp);
            }
            catch (System.Exception ex)
            {
                Debug.WriteLine($"{LOG_PREFIX} CẢNH BÁO khi giải phóng Inventor COM: {ex.Message}");
            }
        }

        /// <summary>
        /// Xử lý 1 file IDW: trích xuất metadata, export DWG, lưu JSON.
        /// </summary>
        private void ProcessSingleIdwFile(dynamic invApp, string idwPath)
        {
            Debug.WriteLine($"{LOG_PREFIX} Đang xử lý: {Path.GetFileName(idwPath)}...");

            string baseName = Path.GetFileNameWithoutExtension(idwPath);
            string dwgOutputPath = Path.Combine(_libraryFolderPath, baseName + ".dwg");
            string jsonOutputPath = Path.Combine(_libraryFolderPath, baseName + ".json");

            dynamic drawingDoc = null;
            try
            {
                // Mở file IDW (read-only)
                drawingDoc = invApp.Documents.Open(idwPath, false);

                // Trích xuất iProperties
                FittingMetadata metadata = ExtractIProperties(drawingDoc);

                // Trích xuất thông tin các Drawing Views
                metadata.Views = ExtractDrawingViews(drawingDoc);

                // Export DWG
                ExportIdwToDwg(drawingDoc, dwgOutputPath);

                // Lưu metadata ra JSON
                string json = JsonConvert.SerializeObject(metadata, Formatting.Indented);
                File.WriteAllText(jsonOutputPath, json);
                Debug.WriteLine($"{LOG_PREFIX} Đã lưu JSON: {jsonOutputPath}");
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
                        Debug.WriteLine($"{LOG_PREFIX} CẢNH BÁO khi đóng IDW: {ex.Message}");
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
                Debug.WriteLine($"{LOG_PREFIX} CẢNH BÁO khi đọc iProperties: {ex.Message}");
            }

            Debug.WriteLine($"{LOG_PREFIX} iProperties: PartNumber={metadata.PartNumber}, Title={metadata.Title}");
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

                // Mass trong Inventor trả về dạng số (double) — chuyển thành string
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
                            views.Add(new ViewMetadata
                            {
                                Name = (string)drawingView.Name,
                                CenterX = (double)drawingView.Center.X,
                                CenterY = (double)drawingView.Center.Y,
                                Width = (double)drawingView.Width,
                                Height = (double)drawingView.Height
                            });
                        }
                        catch (System.Exception ex)
                        {
                            Debug.WriteLine($"{LOG_PREFIX} CẢNH BÁO khi đọc DrawingView: {ex.Message}");
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.WriteLine($"{LOG_PREFIX} CẢNH BÁO khi duyệt Sheets: {ex.Message}");
            }

            Debug.WriteLine($"{LOG_PREFIX} Tìm thấy {views.Count} drawing view(s).");
            return views;
        }

        /// <summary>
        /// Export file IDW sang định dạng DWG qua Inventor COM.
        /// </summary>
        private void ExportIdwToDwg(dynamic drawingDoc, string dwgOutputPath)
        {
            Debug.WriteLine($"{LOG_PREFIX} Đang export DWG: {dwgOutputPath}...");

            // Lấy DWG translator add-in từ Inventor
            dynamic translatorAddin = null;
            foreach (dynamic addin in drawingDoc.Parent.ApplicationAddIns)
            {
                // GUID của Inventor DWG Translator: {C24E3AC2-122E-11D5-8E91-0010B541CD80}
                if (addin.ClassIdString == "{C24E3AC2-122E-11D5-8E91-0010B541CD80}")
                {
                    translatorAddin = addin;
                    break;
                }
            }

            if (translatorAddin == null)
                throw new InvalidOperationException("Không tìm thấy DWG Translator Add-In trong Inventor.");

            // Cấu hình export context
            dynamic transContext = translatorAddin.Parent.TransientObjects.CreateTranslationContext();
            transContext.Type = 1; // kSaveCopyAsTranslation

            // Tạo NameValueMap cho options
            dynamic options = translatorAddin.Parent.TransientObjects.CreateNameValueMap();

            // Kiểm tra có cần options không
            dynamic hasOptions = null;
            if (translatorAddin.HasSaveCopyAsOptions(drawingDoc, transContext, options))
            {
                // Sử dụng options mặc định từ Inventor
            }

            // Thực hiện export
            translatorAddin.SaveCopyAs(drawingDoc, transContext, options, dwgOutputPath);
            Debug.WriteLine($"{LOG_PREFIX} Export DWG THÀNH CÔNG: {dwgOutputPath}");
        }

        #endregion
    }
}
