using System;
using System.Windows;
using System.Windows.Controls;
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
        // STEP 1 & 2: EXTRACTION & JSON IMPORT (Tạm thời chặn bằng MessageBox)
        // =========================================================
        private void BtnBatchImportInventor_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Tính năng Import Inventor sẽ được cập nhật Service ở bước cuối cùng.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnImportJson_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Tính năng Import JSON sẽ được cập nhật Service ở bước cuối cùng.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
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