using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using MCGCadPlugin.Models.FittingManagement;
using MCGCadPlugin.Services.FittingManagement;

namespace MCGCadPlugin.Views.FittingManagement
{
    public partial class NewAccessoryWindow : Window
    {
        private readonly IFittingManagementService _service;
        private readonly string _masterCatalogPath = @"C:\Temp_BIM_Library\MasterCatalog.json";
        public string CreatedPartId { get; private set; } 

        public NewAccessoryWindow(IFittingManagementService service)
        {
            InitializeComponent();
            _service = service;
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtPartID.Text))
            {
                MessageBox.Show("Part ID is required!", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtPartID.Focus(); return;
            }

            CreatedPartId = TxtPartID.Text.Trim();
            string bomType = (CboBomType.SelectedItem is ComboBoxItem cb) ? cb.Content.ToString() : "DETAIL";

            var newItem = new CatalogItem
            {
                PartNumber = CreatedPartId,
                Description = TxtDesc.Text.Trim(),
                Title = string.IsNullOrWhiteSpace(TxtXClass.Text) ? "Accessory" : TxtXClass.Text.Trim(), 
                BomType = bomType,
                EntityType = "Accessory", 
                UoM = "pcs",
                BlockName = ""
            };

            try
            {
                _service.AddItemsToProjectCatalog(_masterCatalogPath, new List<CatalogItem> { newItem });
                this.DialogResult = true; 
                this.Close();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false; this.Close();
        }
    }
}