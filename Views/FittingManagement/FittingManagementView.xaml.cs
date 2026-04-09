using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using MCGCadPlugin.Services.FittingManagement;

namespace MCGCadPlugin.Views.FittingManagement
{
    public partial class FittingManagementView : UserControl
    {
        // Khởi tạo Service qua Interface (Đúng chuẩn SOLID)
        private readonly IFittingManagementService _service;

        public FittingManagementView()
        {
            InitializeComponent();
            _service = new FittingManagementService(); 
        }

        // =========================================================
        // STEP 1: IDW EXTRACTION (Inventor COM Interop)
        // =========================================================
        private void BtnBatchImportInventor_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                OpenFileDialog ofd = new OpenFileDialog
                {
                    Title = "Select Inventor Drawing Files (.idw)",
                    Filter = "Inventor Drawing (*.idw)|*.idw",
                    Multiselect = true
                };

                if (ofd.ShowDialog() != true || ofd.FileNames.Length == 0) return;

                var result = _service.BatchImportIdwFiles(ofd.FileNames);
                MessageBox.Show(
                    $"Import IDW hoàn tất!\n\nThành công: {result.Item1}\nThất bại: {result.Item2}",
                    "Import IDW Result",
                    MessageBoxButton.OK,
                    result.Item2 > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Lỗi Import IDW", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // =========================================================
        // STEP 2: JSON IMPORT (Tạo Block + Attributes + Catalog)
        // =========================================================
        private void BtnImportJson_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                OpenFileDialog ofd = new OpenFileDialog
                {
                    Title = "Select Extracted JSON Files",
                    Filter = "JSON Files (*.json)|*.json",
                    Multiselect = true
                };

                if (ofd.ShowDialog() != true || ofd.FileNames.Length == 0) return;

                // Xác định BomType từ RadioButton trên UI
                string bomType = (RadioPanelFitting.IsChecked == true) ? "PANEL" : "DETAIL";

                var result = _service.ImportJsonAndCreateBlocks(ofd.FileNames, bomType);
                MessageBox.Show(
                    $"Import JSON hoàn tất!\n\nBlock đã tạo: {result.Item1}\nBỏ qua/Thất bại: {result.Item2}",
                    "Import JSON Result",
                    MessageBoxButton.OK,
                    result.Item2 > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Lỗi Import JSON", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // =========================================================
        // STEP 3: FITTING LIBRARY
        // =========================================================
        private void BtnOpenLibrary_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                FittingLibraryWindow libraryWindow = new FittingLibraryWindow(_service);
                Autodesk.AutoCAD.ApplicationServices.Application.ShowModelessWindow(libraryWindow);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        // =========================================================
        // STEP 4: BOM EXPORT & BALLOONING
        // =========================================================
        private void BtnOpenBomPreview_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                BomPreviewWindow bomWindow = new BomPreviewWindow(_service);
                Autodesk.AutoCAD.ApplicationServices.Application.ShowModelessWindow(bomWindow);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void BtnAddBalloon_Click(object sender, RoutedEventArgs e)
        {
            Autodesk.AutoCAD.Internal.Utils.SetFocusToDwgView();
            try { _service.InteractivePlaceBalloon(); }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void BtnMassBalloon_Click(object sender, RoutedEventArgs e)
        {
            Autodesk.AutoCAD.Internal.Utils.SetFocusToDwgView();
            try { _service.MassAutoBalloon(); }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        // =========================================================
        // BLOCK UTILITIES EVENTS
        // =========================================================
        private void BtnRenameCloneBlock_Click(object sender, RoutedEventArgs e)
        {
            Autodesk.AutoCAD.Internal.Utils.SetFocusToDwgView();
            try { _service.InteractiveBlockRenameClone(); } catch (Exception ex) { MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void BtnRedefineBlocks_Click(object sender, RoutedEventArgs e)
        {
            Autodesk.AutoCAD.Internal.Utils.SetFocusToDwgView();
            try { _service.RedefineBlocksFromLibrary(); } catch (Exception ex) { MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void BtnSmartReplace_Click(object sender, RoutedEventArgs e)
        {
            Autodesk.AutoCAD.Internal.Utils.SetFocusToDwgView();
            try { _service.SmartReplaceBlocks(); } catch (Exception ex) { MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void BtnChangeBasePoint_Click(object sender, RoutedEventArgs e)
        {
            Autodesk.AutoCAD.Internal.Utils.SetFocusToDwgView();
            try { _service.ChangeBlockBasePoint(); } catch (Exception ex) { MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void BtnAddToBlock_Click(object sender, RoutedEventArgs e)
        {
            Autodesk.AutoCAD.Internal.Utils.SetFocusToDwgView();
            try { _service.AddEntitiesToBlock(); } catch (Exception ex) { MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void BtnExtractFromBlock_Click(object sender, RoutedEventArgs e)
        {
            Autodesk.AutoCAD.Internal.Utils.SetFocusToDwgView();
            try { _service.ExtractEntitiesFromBlock(); } catch (Exception ex) { MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
        }
    }
}