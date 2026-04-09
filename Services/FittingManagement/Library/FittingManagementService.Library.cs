using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Newtonsoft.Json;
using MCGCadPlugin.Models.FittingManagement;
using MCGCadPlugin.Utilities.FittingManagement;

namespace MCGCadPlugin.Services.FittingManagement
{
    /// <summary>
    /// Chuyên xử lý Đọc/Ghi dữ liệu JSON và chèn Block từ Master/Project Library.
    /// </summary>
    public partial class FittingManagementService : IFittingManagementService
    {
        private const string LOG_PREFIX = "[FittingManagementService]";
        private readonly string _libraryFolderPath = @"C:\Temp_BIM_Library";

        public FittingManagementService()
        {
            Debug.WriteLine($"{LOG_PREFIX} Khởi tạo service.");
        }

        public List<CatalogItem> GetMasterCatalogItems()
        {
            Debug.WriteLine($"{LOG_PREFIX} Bắt đầu lấy dữ liệu Master Catalog...");
            try
            {
                string catalogPath = Path.Combine(_libraryFolderPath, "MasterCatalog.json");
                if (!File.Exists(catalogPath))
                {
                    Debug.WriteLine($"{LOG_PREFIX} CẢNH BÁO: Không tìm thấy file MasterCatalog.json.");
                    return new List<CatalogItem>();
                }

                string json = File.ReadAllText(catalogPath);
                var items = JsonConvert.DeserializeObject<List<CatalogItem>>(json) ?? new List<CatalogItem>();
                Debug.WriteLine($"{LOG_PREFIX} Đọc THÀNH CÔNG {items.Count} items từ Master Catalog.");
                return items;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{LOG_PREFIX} LỖI GetMasterCatalogItems: {ex.Message}");
                throw;
            }
        }

        public Tuple<int, int> AddItemsToProjectCatalog(string projectJsonPath, List<CatalogItem> itemsToAdd)
        {
            Debug.WriteLine($"{LOG_PREFIX} Bắt đầu lưu item vào Project Catalog...");
            try
            {
                var result = MergeItemsToJson(projectJsonPath, itemsToAdd);
                Debug.WriteLine($"{LOG_PREFIX} Lưu Project Catalog THÀNH CÔNG (Mới: {result.Item1}, Sửa: {result.Item2}).");
                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{LOG_PREFIX} LỖI AddItemsToProjectCatalog: {ex.Message}");
                throw;
            }
        }

        public Tuple<int, int> PublishToCentralLibrary(List<Tuple<ObjectId, CatalogItem>> itemsToPublish)
        {
            Debug.WriteLine($"{LOG_PREFIX} Bắt đầu Publish lên Central Library...");
            try
            {
                if (!Directory.Exists(_libraryFolderPath)) Directory.CreateDirectory(_libraryFolderPath);

                Database db = HostApplicationServices.WorkingDatabase;
                List<CatalogItem> successItems = new List<CatalogItem>();

                foreach (var item in itemsToPublish)
                {
                    ObjectId blockId = item.Item1;
                    CatalogItem info = item.Item2;

                    try
                    {
                        using (Database exportDb = db.Wblock(blockId))
                        {
                            exportDb.Insunits = UnitsValue.Millimeters;
                            if (File.Exists(info.FilePath)) File.Delete(info.FilePath);
                            exportDb.SaveAs(info.FilePath, DwgVersion.Current);
                        }
                        successItems.Add(info);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"{LOG_PREFIX} LỖI khi export {info.BlockName}: {ex.Message}");
                    }
                }

                if (successItems.Count > 0)
                {
                    string catalogPath = Path.Combine(_libraryFolderPath, "MasterCatalog.json");
                    var result = MergeItemsToJson(catalogPath, successItems);
                    Debug.WriteLine($"{LOG_PREFIX} Publish THÀNH CÔNG {successItems.Count} items.");
                    return result;
                }

                return new Tuple<int, int>(0, 0);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{LOG_PREFIX} LỖI PublishToCentralLibrary: {ex.Message}");
                throw;
            }
        }

        private Tuple<int, int> MergeItemsToJson(string jsonPath, List<CatalogItem> newItems)
        {
            List<CatalogItem> catalog = new List<CatalogItem>();
            int newCount = 0;
            int updatedCount = 0;

            if (File.Exists(jsonPath))
            {
                try
                {
                    string oldJson = File.ReadAllText(jsonPath);
                    catalog = JsonConvert.DeserializeObject<List<CatalogItem>>(oldJson) ?? new List<CatalogItem>();
                }
                catch { catalog = new List<CatalogItem>(); }
            }

            foreach (var newItem in newItems)
            {
                var existingItem = catalog.FirstOrDefault(x => x.BlockName == newItem.BlockName);
                if (existingItem == null)
                {
                    newCount++;
                    catalog.Add(newItem);
                }
                else
                {
                    if (existingItem.Revision != newItem.Revision) updatedCount++;
                    catalog.Remove(existingItem);
                    catalog.Add(newItem);
                }
            }

            File.WriteAllText(jsonPath, JsonConvert.SerializeObject(catalog, Formatting.Indented));
            return new Tuple<int, int>(newCount, updatedCount);
        }

        public void InsertBlockFromLibrary(string dwgPath, string blockName)
        {
            Debug.WriteLine($"{LOG_PREFIX} Bắt đầu lệnh InsertBlockFromLibrary: {blockName}...");
            try
            {
                Document doc = Application.DocumentManager.MdiActiveDocument;
                Database db = doc.Database;
                Editor ed = doc.Editor;

                if (!File.Exists(dwgPath)) throw new FileNotFoundException($"Không tìm thấy file: {dwgPath}");

                using (DocumentLock loc = doc.LockDocument())
                {
                    ObjectId btrId = ObjectId.Null;

                    using (var tr = db.TransactionManager.StartTransaction())
                    {
                        try
                        {
                            BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                            if (!bt.Has(blockName))
                            {
                                using (Database sideDb = new Database(false, true))
                                {
                                    sideDb.ReadDwgFile(dwgPath, FileShare.Read, true, "");
                                    sideDb.Insunits = db.Insunits;
                                    btrId = db.Insert(blockName, sideDb, true);
                                }
                            }
                            else
                            {
                                btrId = bt[blockName];
                            }

                            BlockTableRecord targetBtr = (BlockTableRecord)tr.GetObject(btrId, OpenMode.ForWrite);
                            targetBtr.Units = db.Insunits;

                            bool hasPosNum = false;
                            foreach (ObjectId childId in targetBtr)
                            {
                                DBObject obj = tr.GetObject(childId, OpenMode.ForRead);
                                if (obj is AttributeDefinition attDef && attDef.Tag.Equals("POS_NUM", StringComparison.OrdinalIgnoreCase))
                                {
                                    hasPosNum = true;
                                    break;
                                }
                            }

                            if (!hasPosNum) FittingBlockUtility.AddAttributeDef(targetBtr, tr, "POS_NUM", "", "Position Number", true);

                            tr.Commit();
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"{LOG_PREFIX} Transaction ABORT (Nạp định nghĩa Block): {ex.Message}");
                            throw;
                        }
                    }

                    PromptPointOptions ppo = new PromptPointOptions($"\nSelect insertion point for '{blockName}' (or press ESC to skip): ");
                    PromptPointResult ppr = ed.GetPoint(ppo);

                    if (ppr.Status == PromptStatus.OK)
                    {
                        using (var tr = db.TransactionManager.StartTransaction())
                        {
                            try
                            {
                                FittingBlockUtility.InsertBlockReference(db, tr, btrId, ppr.Value);
                                tr.Commit();
                                Debug.WriteLine($"{LOG_PREFIX} InsertBlockFromLibrary THÀNH CÔNG.");
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"{LOG_PREFIX} Transaction ABORT (Chèn Block Reference): {ex.Message}");
                                throw;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{LOG_PREFIX} LỖI InsertBlockFromLibrary: {ex.Message}");
                throw;
            }
        }
    }
}