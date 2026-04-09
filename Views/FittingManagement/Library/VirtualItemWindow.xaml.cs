using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using MCGCadPlugin.Models.FittingManagement;
using MCGCadPlugin.Services.FittingManagement;

namespace MCGCadPlugin.Views.FittingManagement
{
    public partial class VirtualItemWindow : Window
    {
        private readonly IFittingManagementService _service;
        private CatalogItem _draftItem;
        private readonly string _masterCatalogPath = @"C:\Temp_BIM_Library\MasterCatalog.json";

        public VirtualItemWindow(IFittingManagementService service, CatalogItem draftItem)
        {
            InitializeComponent();
            _service = service;
            _draftItem = draftItem;
            LoadDraftDataToUI();
        }

        private void LoadDraftDataToUI()
        {
            if (_draftItem == null) return;

            TxtEntityType.Text = _draftItem.EntityType == "Block" ? $"Block: {_draftItem.BlockName}" : _draftItem.EntityType;
            TxtLayer.Text = _draftItem.TriggerLayer;
            TxtColor.Text = _draftItem.TriggerColor;
            TxtUoM.Text = _draftItem.UoM;

            TxtPartID.Text = _draftItem.PartNumber; 
            TxtDesc.Text = _draftItem.Description; 
            TxtMass.Text = _draftItem.Mass ?? "0";

            if (!string.IsNullOrEmpty(_draftItem.BomType))
            {
                foreach (ComboBoxItem item in CboBomType.Items)
                {
                    if (item.Content.ToString().Equals(_draftItem.BomType, StringComparison.OrdinalIgnoreCase))
                    {
                        CboBomType.SelectedItem = item; break;
                    }
                }
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtPartID.Text))
            {
                MessageBox.Show("Part ID is required!", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtPartID.Focus(); return;
            }

            _draftItem.PartNumber = TxtPartID.Text.Trim();
            _draftItem.Title = TxtTitle.Text.Trim();
            _draftItem.Description = TxtDesc.Text.Trim();
            _draftItem.Mass = TxtMass.Text.Trim();
            _draftItem.BomType = (CboBomType.SelectedItem is ComboBoxItem cb) ? cb.Content.ToString() : "DETAIL";
            
            if (_draftItem.EntityType != "Block") _draftItem.BlockName = ""; 
            _draftItem.FilePath = ""; 

            try
            {
                var result = _service.AddItemsToProjectCatalog(_masterCatalogPath, new List<CatalogItem> { _draftItem });
                MessageBox.Show("Saved to Master Library successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
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