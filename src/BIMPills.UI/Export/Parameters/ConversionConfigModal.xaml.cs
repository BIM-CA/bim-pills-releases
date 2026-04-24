using System.Windows;
using System.Windows.Controls;
using BIMPills.Core.ParameterExtractor;
using BIMPills.UI.ParameterExtractor;

namespace BIMPills.UI.Export.Parameters
{
    public partial class ConversionConfigModal : Window
    {
        private bool _confirmed;

        private ConversionConfigModal()
        {
            InitializeComponent();
        }

        public static void Show(Window? owner, SourceMappingVM vm)
        {
            var modal = new ConversionConfigModal { Owner = owner };

            modal.OriginCombo.SelectedItem     = vm.ConversionOrigin;
            modal.GeoFormatCombo.SelectedItem  = vm.ConversionGeoFormat;
            modal.ConvMethodCombo.SelectedItem = vm.ConversionMethod;
            modal.UtmZoneBox.Text              = vm.ConversionUtmZone.ToString();
            modal.UtmNorthCheck.IsChecked      = vm.ConversionUtmIsNorth;
            modal.UpdateUtmVisibility(vm.ConversionMethod);

            modal.ShowDialog();
            if (!modal._confirmed) return;

            if (modal.OriginCombo.SelectedItem is CoordinateOrigin o)
                vm.ConversionOrigin = o;
            if (modal.GeoFormatCombo.SelectedItem is GeoFormat g)
                vm.ConversionGeoFormat = g;
            if (modal.ConvMethodCombo.SelectedItem is GeoConversionMethod m)
                vm.ConversionMethod = m;
            if (int.TryParse(modal.UtmZoneBox.Text, out int z) && z >= 1 && z <= 60)
                vm.ConversionUtmZone = z;
            vm.ConversionUtmIsNorth = modal.UtmNorthCheck.IsChecked == true;
        }

        private void UpdateUtmVisibility(GeoConversionMethod method)
        {
            UtmPanel.Visibility = method == GeoConversionMethod.UTM
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void ConvMethod_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (ConvMethodCombo.SelectedItem is GeoConversionMethod m)
                UpdateUtmVisibility(m);
        }

        private void Apply_Click(object sender, RoutedEventArgs e) { _confirmed = true; Close(); }
        private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
    }
}
