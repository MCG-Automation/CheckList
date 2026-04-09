using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Newtonsoft.Json;
using MCGCadPlugin.Models.FittingManagement;
using MCGCadPlugin.Utilities.FittingManagement;

namespace MCGCadPlugin.Services.FittingManagement
{
    /// <summary>
    /// Đọc file JSON metadata (đã extract từ Inventor) kết hợp file DWG tương ứng,
    /// tạo block definitions với attributes trong bản vẽ AutoCAD hiện tại,
    /// và đăng ký vào MasterCatalog.
    /// </summary>
    public partial class FittingManagementService
    {
        // Cấu hình layer cho từng loại BOM
        private const string LAYER_FITTING_PANEL = "MCG_Fitting_Panel";
        private const short COLOR_INDEX_PANEL = 5;   // Blue
        private const string LAYER_FITTING_DETAIL = "MCG_Fitting_Detail";
        private const short COLOR_INDEX_DETAIL = 1;  // Red

        /// <summary>
        /// Import hàng loạt file JSON, tạo block + attributes trong bản vẽ, đăng ký catalog.
        /// </summary>
        /// <param name="jsonPaths">Danh sách đường dẫn file .json</param>
        /// <param name="bomType">Loại BOM: "PANEL" hoặc "DETAIL"</param>
        /// <returns>Tuple (số file thành công, số file thất bại)</returns>
        public Tuple<int, int> ImportJsonAndCreateBlocks(string[] jsonPaths, string bomType)
        {
            Debug.WriteLine($"{LOG_PREFIX} Bắt đầu ImportJsonAndCreateBlocks — {jsonPaths.Length} file(s), BomType={bomType}...");

            int successCount = 0;
            int failCount = 0;
            List<CatalogItem> newCatalogItems = new List<CatalogItem>();

            // Xác định layer và màu dựa trên BomType
            string layerName = (bomType == "PANEL") ? LAYER_FITTING_PANEL : LAYER_FITTING_DETAIL;
            short colorIndex = (bomType == "PANEL") ? COLOR_INDEX_PANEL : COLOR_INDEX_DETAIL;

            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;

            try
            {
                using (DocumentLock loc = doc.LockDocument())
                {
                    foreach (string jsonPath in jsonPaths)
                    {
                        try
                        {
                            CatalogItem catalogItem = ProcessSingleJsonFile(
                                db, jsonPath, bomType, layerName, colorIndex);

                            if (catalogItem != null)
                            {
                                newCatalogItems.Add(catalogItem);
                                successCount++;
                                Debug.WriteLine($"{LOG_PREFIX} Import JSON THÀNH CÔNG: {Path.GetFileName(jsonPath)}");
                            }
                            else
                            {
                                failCount++;
                            }
                        }
                        catch (System.Exception ex)
                        {
                            failCount++;
                            Debug.WriteLine($"{LOG_PREFIX} LỖI import JSON '{Path.GetFileName(jsonPath)}': {ex.Message}");
                        }
                    }
                }

                // Đăng ký tất cả items vào MasterCatalog
                if (newCatalogItems.Count > 0)
                {
                    string catalogPath = Path.Combine(_libraryFolderPath, "MasterCatalog.json");
                    if (!Directory.Exists(_libraryFolderPath))
                        Directory.CreateDirectory(_libraryFolderPath);

                    var mergeResult = MergeItemsToJson(catalogPath, newCatalogItems);
                    Debug.WriteLine($"{LOG_PREFIX} Đã cập nhật MasterCatalog: Mới={mergeResult.Item1}, Cập nhật={mergeResult.Item2}.");
                }
            }
            catch (System.Exception ex)
            {
                Debug.WriteLine($"{LOG_PREFIX} LỖI ImportJsonAndCreateBlocks: {ex.Message}");
                Debug.WriteLine($"{LOG_PREFIX} Stack trace:\n{ex.StackTrace}");
                throw;
            }

            Debug.WriteLine($"{LOG_PREFIX} ImportJsonAndCreateBlocks HOÀN TẤT — Thành công: {successCount}, Thất bại: {failCount}.");
            return new Tuple<int, int>(successCount, failCount);
        }

        #region JSON Import — Private Helpers

        /// <summary>
        /// Xử lý 1 file JSON: đọc metadata, import DWG, tạo block, inject attributes.
        /// </summary>
        /// <returns>CatalogItem nếu thành công, null nếu skip/thất bại</returns>
        private CatalogItem ProcessSingleJsonFile(
            Database db, string jsonPath, string bomType, string layerName, short colorIndex)
        {
            Debug.WriteLine($"{LOG_PREFIX} Đang xử lý JSON: {Path.GetFileName(jsonPath)}...");

            // 1. Đọc và parse JSON
            string jsonContent = File.ReadAllText(jsonPath);
            FittingMetadata metadata = JsonConvert.DeserializeObject<FittingMetadata>(jsonContent);
            if (metadata == null)
            {
                Debug.WriteLine($"{LOG_PREFIX} CẢNH BÁO: JSON rỗng hoặc không hợp lệ — bỏ qua.");
                return null;
            }

            // 2. Xác định tên block
            string blockName = !string.IsNullOrWhiteSpace(metadata.PartNumber)
                ? metadata.PartNumber.Trim()
                : Path.GetFileNameWithoutExtension(jsonPath);

            // 3. Tìm file DWG tương ứng (cùng tên, cùng folder)
            string dwgPath = Path.ChangeExtension(jsonPath, ".dwg");
            if (!File.Exists(dwgPath))
            {
                Debug.WriteLine($"{LOG_PREFIX} CẢNH BÁO: Không tìm thấy file DWG tương ứng: {dwgPath} — bỏ qua.");
                return null;
            }

            // 4. Tạo block definition và inject attributes
            using (var tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);

                    // Kiểm tra block đã tồn tại chưa
                    if (bt.Has(blockName))
                    {
                        Debug.WriteLine($"{LOG_PREFIX} Block '{blockName}' đã tồn tại — bỏ qua.");
                        tr.Commit();
                        return null;
                    }

                    // Tạo layer nếu chưa có
                    FittingBlockUtility.CheckAndCreateLayer(db, tr, layerName, colorIndex);

                    // Import DWG thành block definition
                    ObjectId btrId;
                    using (Database sideDb = new Database(false, true))
                    {
                        sideDb.ReadDwgFile(dwgPath, FileShare.Read, true, "");
                        sideDb.Insunits = db.Insunits;
                        btrId = db.Insert(blockName, sideDb, true);
                    }

                    // Mở block definition để chỉnh sửa
                    BlockTableRecord btr = (BlockTableRecord)tr.GetObject(btrId, OpenMode.ForWrite);

                    // Gán layer cho tất cả entity trong block
                    foreach (ObjectId entId in btr)
                    {
                        Entity ent = tr.GetObject(entId, OpenMode.ForWrite) as Entity;
                        if (ent != null)
                            ent.Layer = layerName;
                    }

                    // Inject attributes
                    FittingBlockUtility.AddAttributeDef(btr, tr,
                        "PART_ID", metadata.PartNumber ?? "", "Part Number", false);
                    FittingBlockUtility.AddAttributeDef(btr, tr,
                        "DESC", metadata.Description ?? "", "Description", false);
                    FittingBlockUtility.AddAttributeDef(btr, tr,
                        "MASS", metadata.Mass ?? "", "Mass", true);
                    FittingBlockUtility.AddAttributeDef(btr, tr,
                        "MATERIAL", metadata.Material ?? "", "Material", true);
                    FittingBlockUtility.AddAttributeDef(btr, tr,
                        "REVISION", metadata.Revision ?? "", "Revision", true);
                    FittingBlockUtility.AddAttributeDef(btr, tr,
                        "BOM_TYPE", bomType, "BOM Type", true);
                    FittingBlockUtility.AddAttributeDef(btr, tr,
                        "POS_NUM", "", "Position Number", true);

                    tr.Commit();
                    Debug.WriteLine($"{LOG_PREFIX} Block '{blockName}' đã tạo với 7 attributes, layer={layerName}.");
                }
                catch (System.Exception ex)
                {
                    Debug.WriteLine($"{LOG_PREFIX} Transaction ABORT: {ex.Message}");
                    throw;
                }
            }

            // 5. Tạo CatalogItem để đăng ký vào MasterCatalog
            return new CatalogItem
            {
                BlockName = blockName,
                PartNumber = metadata.PartNumber ?? "",
                Description = metadata.Description ?? "",
                Material = metadata.Material ?? "",
                Mass = metadata.Mass ?? "",
                Revision = metadata.Revision ?? "",
                Designer = metadata.Designer ?? "",
                Title = metadata.Title ?? "",
                BomType = bomType,
                FilePath = dwgPath,
                EntityType = "Block",
                TriggerLayer = layerName,
                UoM = "pcs"
            };
        }

        #endregion
    }
}
