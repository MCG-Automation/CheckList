using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using MCGCadPlugin.Models.FittingManagement;

namespace MCGCadPlugin.Services.FittingManagement
{
    public interface IFittingManagementService
    {
        // --- Giai đoạn 1 & 2: IDW Extraction & JSON Import ---
        ImportResult BatchImportIdwFiles(string[] idwPaths);
        ImportResult ImportJsonAndCreateBlocks(string[] jsonPaths, string bomType);

        // --- Giai đoạn 3.1: Library & Virtual Items ---
        List<CatalogItem> GetMasterCatalogItems();
        Tuple<int, int> AddItemsToProjectCatalog(string projectJsonPath, List<CatalogItem> itemsToAdd);
        Tuple<int, int> PublishToCentralLibrary(List<Tuple<ObjectId, CatalogItem>> itemsToPublish);
        void InsertBlockFromLibrary(string dwgPath, string blockName);
        CatalogItem PickGeometricFeatureFromCad();

        // --- Giai đoạn 3.2: BOM Harvester & Ballooning ---
        List<BomHarvestRecord> HarvestStructureBom();
        List<BomHarvestRecord> HarvestInterfaceBom();
        void MassAutoBalloon();
        void InteractivePlaceBalloon();

        // --- Giai đoạn 3.3: Block Utilities ---
        void InteractiveBlockRenameClone();
        void SmartReplaceBlocks();
        void RedefineBlocksFromLibrary();
        void ExtractEntitiesFromBlock();
        void ChangeBlockBasePoint();
        void AddEntitiesToBlock();
    }
}