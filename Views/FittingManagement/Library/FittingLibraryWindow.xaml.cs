using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Newtonsoft.Json;
using Microsoft.Win32;
using MCGCadPlugin.Models.FittingManagement;
using MCGCadPlugin.Services.FittingManagement;

namespace MCGCadPlugin.Views.FittingManagement
{
    public class CategoryNode
    {
        public string CategoryName { get; set; }
        public string CountLabel { get; set; }
        public List<CategoryNode> Children { get; set; } = new List<CategoryNode>();
        public List<CatalogItem> Items { get; set; } = new List<CatalogItem>();
    }

    public partial class FittingLibraryWindow : Window
    {
        private readonly IFittingManagementService _service;
        private List<CatalogItem> _fullCatalog;
        private string _libraryPath = @"C:\Temp_BIM_Library";
        private string _currentProjectFile = "";

        public FittingLibraryWindow(IFittingManagementService service)
        {
            InitializeComponent();
            _service = service;
            LoadCatalog(Path.Combine(_libraryPath, "MasterCatalog.json"));
            ColProjectPos.IsReadOnly = true; 
            BtnAutoAssignPos.IsEnabled = false; 
        }

        private void LoadCatalog(string catalogFilePath)
        {
            _fullCatalog = new List<CatalogItem>();
            if (File.Exists(catalogFilePath))
            {
                try { _fullCatalog = JsonConvert.DeserializeObject<List<CatalogItem>>(File.ReadAllText(catalogFilePath)) ?? new List<CatalogItem>(); }
                catch (Exception ex) { MessageBox.Show("Cannot load library: " + ex.Message); }
            }
            BuildCategoryTree(); ApplyFilters();
        }

        private void BuildCategoryTree()
        {
            var rootNodes = new List<CategoryNode>();
            rootNodes.Add(new CategoryNode { CategoryName = "All Fittings", CountLabel = $"({_fullCatalog.Count})", Items = _fullCatalog });

            var bomGroups = _fullCatalog.GroupBy(x => {
                if (string.IsNullOrWhiteSpace(x.BomType)) return "Uncategorized (Legacy)";
                string type = x.BomType.ToUpper();
                if (type == "PANEL") return "Fitting In Panel";
                if (type == "DETAIL" || type == "HULL") return "Fitting In Detail";
                return "Uncategorized (Legacy)";
            }).OrderBy(g => g.Key);

            foreach (var bg in bomGroups)
            {
                var bomNode = new CategoryNode { CategoryName = bg.Key, CountLabel = $"({bg.Count()})", Items = bg.ToList() };
                var catGroups = bg.GroupBy(x => string.IsNullOrWhiteSpace(x.Title) ? "Uncategorized" : x.Title.Trim()).OrderBy(g => g.Key);
                foreach (var cg in catGroups)
                {
                    bomNode.Children.Add(new CategoryNode { CategoryName = cg.Key, CountLabel = $"({cg.Count()})", Items = cg.ToList() });
                }
                rootNodes.Add(bomNode);
            }
            TreeCategories.ItemsSource = rootNodes;
        }

        private void TreeCategories_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e) { ApplyFilters(); }
        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e) { ApplyFilters(); }

        private void ApplyFilters()
        {
            if (_fullCatalog == null) return;
            IEnumerable<CatalogItem> sourceList = _fullCatalog;
            if (TreeCategories.SelectedItem is CategoryNode selectedNode && selectedNode.CategoryName != "All Fittings") sourceList = selectedNode.Items;

            string searchText = TxtSearch.Text?.ToLower() ?? "";
            if (string.IsNullOrWhiteSpace(searchText)) { GridCatalog.ItemsSource = sourceList.ToList(); return; }

            string[] keywords = searchText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var filtered = sourceList.Where(i => keywords.All(kw => 
                (i.PartNumber != null && i.PartNumber.ToLower().Contains(kw)) || 
                (i.BlockName != null && i.BlockName.ToLower().Contains(kw)) ||
                (i.Description != null && i.Description.ToLower().Contains(kw)) ||
                (i.Title != null && i.Title.ToLower().Contains(kw)) ||
                (i.Designer != null && i.Designer.ToLower().Contains(kw)) ||
                (i.EntityType != null && i.EntityType.ToLower().Contains(kw)))).ToList();

            GridCatalog.ItemsSource = filtered;
        }

        private void BtnAddFromCad_Click(object sender, RoutedEventArgs e)
        {
            if (RadioProjectMode.IsChecked == true) { MessageBox.Show("Please switch to 'Master Library' mode.", "Notice", MessageBoxButton.OK, MessageBoxImage.Information); return; }
            this.Hide();
            Autodesk.AutoCAD.Internal.Utils.SetFocusToDwgView();
            Autodesk.AutoCAD.ApplicationServices.Application.Idle += OnAutoCadIdle;
        }

        private void OnAutoCadIdle(object sender, EventArgs e)
        {
            Autodesk.AutoCAD.ApplicationServices.Application.Idle -= OnAutoCadIdle;
            try
            {
                var draftItem = _service.PickGeometricFeatureFromCad();
                if (draftItem != null)
                {
                    VirtualItemWindow virtualWin = new VirtualItemWindow(_service, draftItem) { Owner = this };
                    if (virtualWin.ShowDialog() == true) LoadCatalog(Path.Combine(_libraryPath, "MasterCatalog.json"));
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
            finally { this.Show(); }
        }

        private void BtnManageAccessory_Click(object sender, RoutedEventArgs e)
        {
            if (RadioProjectMode.IsChecked == true) return;
            var selectedItems = GridCatalog.SelectedItems.Cast<CatalogItem>().ToList();
            if (selectedItems.Count != 1) return;

            try
            {
                AccessoryManagerWindow accWin = new AccessoryManagerWindow(_service, selectedItems[0]);
                if (accWin.ShowDialog() == true) LoadCatalog(Path.Combine(_libraryPath, "MasterCatalog.json"));
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void BtnInsert_Click(object sender, RoutedEventArgs e) { InsertSelected(); }
        private void GridCatalog_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e) { InsertSelected(); }

        private void InsertSelected()
        {
            var selectedItems = GridCatalog.SelectedItems.Cast<CatalogItem>().ToList();
            if (selectedItems == null || selectedItems.Count == 0) return;

            Autodesk.AutoCAD.Internal.Utils.SetFocusToDwgView();
            try
            {
                foreach (var item in selectedItems)
                {
                    if (item.EntityType != "Block") { MessageBox.Show($"Cannot insert {item.EntityType} as Block."); continue; }
                    _service.InsertBlockFromLibrary(item.FilePath, item.BlockName);
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            LoadCatalog((RadioProjectMode.IsChecked == true && !string.IsNullOrEmpty(_currentProjectFile)) ? _currentProjectFile : Path.Combine(_libraryPath, "MasterCatalog.json"));
        }

        private void RadioMode_Checked(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return; 
            if (RadioMasterMode.IsChecked == true)
            {
                if (BtnAddToProject != null) BtnAddToProject.IsEnabled = true; 
                if (ColProjectPos != null) ColProjectPos.IsReadOnly = true; 
                if (BtnAutoAssignPos != null) BtnAutoAssignPos.IsEnabled = false; 
                if (BtnAddFromCad != null) BtnAddFromCad.IsEnabled = true;
                if (BtnManageAccessory != null) BtnManageAccessory.IsEnabled = true;
                LoadCatalog(Path.Combine(_libraryPath, "MasterCatalog.json"));
            }
            else if (RadioProjectMode.IsChecked == true)
            {
                if (BtnAddToProject != null) BtnAddToProject.IsEnabled = false; 
                if (BtnAddFromCad != null) BtnAddFromCad.IsEnabled = false;
                if (BtnManageAccessory != null) BtnManageAccessory.IsEnabled = false;

                if (string.IsNullOrEmpty(_currentProjectFile)) { RadioMasterMode.IsChecked = true; }
                else
                {
                    if (ColProjectPos != null) ColProjectPos.IsReadOnly = false; 
                    if (BtnAutoAssignPos != null) BtnAutoAssignPos.IsEnabled = true; 
                    LoadCatalog(_currentProjectFile);
                }
            }
        }

        private void BtnLoadProject_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog { Title = "Select Project Library", Filter = "JSON Files (*.json)|*.json" };
            if (ofd.ShowDialog() == true)
            {
                _currentProjectFile = ofd.FileName;
                TxtCurrentProject.Text = Path.GetFileNameWithoutExtension(_currentProjectFile);
                RadioProjectMode.IsChecked = true; LoadCatalog(_currentProjectFile);
            }
        }

        private void BtnCreateProject_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog sfd = new SaveFileDialog { Title = "Create New Project Library", Filter = "JSON Files (*.json)|*.json", FileName = "New_Project_Catalog.json" };
            if (sfd.ShowDialog() == true)
            {
                _currentProjectFile = sfd.FileName;
                TxtCurrentProject.Text = Path.GetFileNameWithoutExtension(_currentProjectFile);
                File.WriteAllText(_currentProjectFile, "[]");
                RadioProjectMode.IsChecked = true; LoadCatalog(_currentProjectFile);
            }
        }

        private void BtnAddToProject_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentProjectFile)) return;
            var selectedItems = GridCatalog.SelectedItems.Cast<CatalogItem>().ToList();
            if (selectedItems.Count == 0) return;

            try
            {
                _service.AddItemsToProjectCatalog(_currentProjectFile, selectedItems);
                MessageBox.Show("Items added to project.");
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void GridCatalog_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.Column == ColProjectPos && RadioProjectMode.IsChecked == true && !string.IsNullOrEmpty(_currentProjectFile))
            {
                var editedItem = e.Row.Item as CatalogItem;
                if (editedItem != null)
                {
                    string newValue = ((TextBox)e.EditingElement).Text;
                    if (editedItem.ProjectPosNum == newValue) return;

                    editedItem.ProjectPosNum = newValue;
                    try { File.WriteAllText(_currentProjectFile, JsonConvert.SerializeObject(_fullCatalog, Formatting.Indented)); }
                    catch (Exception ex) { MessageBox.Show(ex.Message); }
                }
            }
        }

        private void BtnRemoveSelected_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = GridCatalog.SelectedItems.Cast<CatalogItem>().ToList();
            if (selectedItems.Count == 0) return;

            if (MessageBox.Show("Remove item(s)?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                foreach (var item in selectedItems) _fullCatalog.Remove(item);
                string targetFile = (RadioProjectMode.IsChecked == true) ? _currentProjectFile : Path.Combine(_libraryPath, "MasterCatalog.json");
                File.WriteAllText(targetFile, JsonConvert.SerializeObject(_fullCatalog, Formatting.Indented));
                BuildCategoryTree(); ApplyFilters();
            }
        }

        private void BtnAutoAssignPos_Click(object sender, RoutedEventArgs e)
        {
            if (RadioProjectMode.IsChecked != true || string.IsNullOrEmpty(_currentProjectFile)) return;

            var detailFittings = _fullCatalog.Where(x => !string.IsNullOrWhiteSpace(x.BomType) && (x.BomType.ToUpper() == "DETAIL" || x.BomType.ToUpper() == "HULL")).ToList();
            if (detailFittings.Count == 0) return;

            var groupedByPartId = detailFittings.Where(x => !string.IsNullOrEmpty(x.PartNumber)).GroupBy(x => x.PartNumber).OrderBy(g => g.Key).ToList();
            int posCounter = 1;

            foreach (var group in groupedByPartId)
            {
                string posString = posCounter.ToString("D3");
                foreach (var item in group) item.ProjectPosNum = posString;
                posCounter++;
            }

            try
            {
                File.WriteAllText(_currentProjectFile, JsonConvert.SerializeObject(_fullCatalog, Formatting.Indented));
                GridCatalog.Items.Refresh(); 
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }
        
        // Chức năng Push Update cho Thư viện đã được giữ tại Service `RedefineBlocksFromLibrary` để nhất quán. Nút này trên UI có thể ẩn đi nếu bạn muốn.
        private void BtnUpdateLibrary_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("This feature has been upgraded to 'Redefine Blocks' in the main panel for better stability.", "Upgraded", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}