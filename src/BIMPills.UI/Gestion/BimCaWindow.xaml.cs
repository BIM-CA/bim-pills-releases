using BIMPills.Core.Gestion;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace BIMPills.UI.Gestion
{
    public partial class BimCaWindow : Window
    {
        private readonly Func<string, bool>? _createCallback;
        private readonly HashSet<string>     _existingNames;

        private List<BimCaItem>              _templateItems   = new();
        private Dictionary<string, BimCaGroup> _groups        = new();

        public List<WorksetInfo> CreatedWorksets { get; } = new();

        private static readonly Dictionary<string, (string Name, string Color)> DisciplineInfo = new()
        {
            { "00-GEN", ("General",                           "#78909C") },
            { "01-ARQ", ("Arquitectura",                      "#EF6337") },
            { "02-OCV", ("Obra Civil",                        "#8D6E63") },
            { "03-EST", ("Estructura",                        "#5C6BC0") },
            { "04-IHS", ("Instalaciones Hidrosanitarias",     "#26A69A") },
            { "04-HID", ("Hidráulica",                        "#00ACC1") },
            { "04-SAN", ("Sanitaria",                         "#26A69A") },
            { "05-ELE", ("Electricidad",                      "#FFA726") },
            { "06-CLI", ("Climatización",                     "#42A5F5") },
            { "06-VEN", ("Ventilación",                       "#66BB6A") },
            { "07-PCI", ("Protección Contra Incendio",        "#EF5350") },
            { "08-IND", ("Industrial",                        "#AB47BC") },
            { "09-EQU", ("Equipos",                           "#7E57C2") },
        };

        public BimCaWindow(HashSet<string> existingWorksetNames, Func<string, bool>? createCallback)
        {
            InitializeComponent();
            BIMPills.UI.Shared.ThemeHelper.Apply(this);
            _existingNames  = existingWorksetNames;
            _createCallback = createCallback;
            LoadBuiltIn();
        }

        private void LoadBuiltIn() => LoadLines(GetBuiltInLines());

        private void LoadLines(IEnumerable<string> lines)
        {
            _templateItems.Clear();
            _groups.Clear();
            TemplateGroupsPanel.Children.Clear();

            foreach (var line in lines)
            {
                var name = line.Trim();
                if (string.IsNullOrWhiteSpace(name)) continue;

                var parts = name.Split('-');
                var key   = parts.Length >= 2 ? $"{parts[0]}-{parts[1]}" : "00-GEN";

                if (!_groups.ContainsKey(key)) _groups[key] = new BimCaGroup(key);

                var item = new BimCaItem
                {
                    Name          = name,
                    DisciplineKey = key,
                    IsSelected    = false,
                    AlreadyExists = _existingNames.Contains(name)
                };

                _templateItems.Add(item);
                _groups[key].Items.Add(item);
            }

            foreach (var kvp in _groups.OrderBy(k => k.Key))
                BuildGroupUI(kvp.Value);

            UpdateCount();
        }

        private void BuildGroupUI(BimCaGroup group)
        {
            var hasDi   = DisciplineInfo.TryGetValue(group.Key, out var di);
            var disName = hasDi ? di.Name : group.Key;
            var color   = (Color)ColorConverter.ConvertFromString(hasDi ? di.Color : "#78909C");

            // ── Header ──
            var headerBorder = new Border
            {
                Background      = new SolidColorBrush(Color.FromArgb(20, color.R, color.G, color.B)),
                CornerRadius    = new CornerRadius(6),
                Padding         = new Thickness(10, 7, 10, 7),
                Margin          = new Thickness(0, 10, 0, 4),
                Cursor          = Cursors.Hand,
                BorderBrush     = new SolidColorBrush(Color.FromArgb(40, color.R, color.G, color.B)),
                BorderThickness = new Thickness(1)
            };

            var hGrid = new Grid();
            hGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            hGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            hGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            hGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var groupCb = new CheckBox { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0), Tag = group.Key };
            groupCb.Click += GroupCheckBox_Click;
            group.GroupCheckBox = groupCb;
            Grid.SetColumn(groupCb, 0);

            var dot = new Border { Width = 10, Height = 10, CornerRadius = new CornerRadius(5), Background = new SolidColorBrush(color), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
            var nameRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            nameRow.Children.Add(dot);
            nameRow.Children.Add(new TextBlock { Text = disName, FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x2E)), FontFamily = new FontFamily("Segoe UI") });
            nameRow.Children.Add(new TextBlock { Text = $"  ({group.Key})", FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(0x86, 0x86, 0x8B)), FontFamily = new FontFamily("Segoe UI") });
            Grid.SetColumn(nameRow, 1);

            var badge = new Border { Background = new SolidColorBrush(Color.FromArgb(30, color.R, color.G, color.B)), CornerRadius = new CornerRadius(10), Padding = new Thickness(8, 2, 8, 2), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
            badge.Child = new TextBlock { Text = $"{group.Items.Count}", FontSize = 10, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(color), FontFamily = new FontFamily("Segoe UI") };
            Grid.SetColumn(badge, 2);

            var chevron = new TextBlock { Text = "\u25B6", FontSize = 9, Foreground = new SolidColorBrush(Color.FromRgb(0x86, 0x86, 0x8B)), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
            Grid.SetColumn(chevron, 3);

            hGrid.Children.Add(groupCb);
            hGrid.Children.Add(nameRow);
            hGrid.Children.Add(badge);
            hGrid.Children.Add(chevron);
            headerBorder.Child = hGrid;

            // ── Items ──
            var itemsPanel = new StackPanel { Margin = new Thickness(32, 0, 0, 0), Visibility = Visibility.Collapsed };
            group.ItemsPanel = itemsPanel;

            foreach (var item in group.Items)
            {
                var parts = item.Name.Split('-');
                var code  = parts.Length >= 3 ? parts[2] : "";
                var desc  = parts.Length >= 4 ? parts[3] : item.Name;
                desc = System.Text.RegularExpressions.Regex.Replace(desc, "(?<=[a-z])(?=[A-Z])", " ");

                var disabled   = new SolidColorBrush(Color.FromRgb(0xAE, 0xAE, 0xB2));
                var normalCol  = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x2E));
                var codeCol    = new SolidColorBrush(Color.FromRgb(0x86, 0x86, 0x8B));

                var itemBorder = new Border { Padding = new Thickness(8, 5, 8, 5), BorderBrush = new SolidColorBrush(Color.FromRgb(0xF2, 0xF2, 0xF7)), BorderThickness = new Thickness(0, 0, 0, 1) };
                var iGrid = new Grid();
                iGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                iGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                iGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var cb = new CheckBox { IsChecked = item.IsSelected, IsEnabled = !item.AlreadyExists, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0), Tag = item };
                cb.Click += ItemCheckBox_Click;
                item.CheckBox = cb;
                Grid.SetColumn(cb, 0);

                var nameRow2 = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                nameRow2.Children.Add(new TextBlock { Text = code, FontSize = 10, FontWeight = FontWeights.Bold, FontFamily = new FontFamily("Consolas"), Foreground = item.AlreadyExists ? disabled : codeCol, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0), MinWidth = 30 });
                nameRow2.Children.Add(new TextBlock { Text = desc, FontSize = 11.5, FontFamily = new FontFamily("Segoe UI"), Foreground = item.AlreadyExists ? disabled : normalCol, VerticalAlignment = VerticalAlignment.Center, ToolTip = item.Name });
                Grid.SetColumn(nameRow2, 1);

                iGrid.Children.Add(cb);
                iGrid.Children.Add(nameRow2);

                if (item.AlreadyExists)
                {
                    var existsBadge = new Border { Background = new SolidColorBrush(Color.FromRgb(0xE8, 0xF5, 0xE9)), CornerRadius = new CornerRadius(4), Padding = new Thickness(6, 1, 6, 1), VerticalAlignment = VerticalAlignment.Center };
                    existsBadge.Child = new TextBlock { Text = "Ya existe", FontSize = 9, Foreground = new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32)), FontFamily = new FontFamily("Segoe UI") };
                    Grid.SetColumn(existsBadge, 2);
                    iGrid.Children.Add(existsBadge);
                }

                itemBorder.Child = iGrid;
                itemsPanel.Children.Add(itemBorder);
            }

            headerBorder.MouseLeftButtonDown += (s, ev) =>
            {
                if (ev.Source is CheckBox) return;
                itemsPanel.Visibility = itemsPanel.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
                chevron.Text = itemsPanel.Visibility == Visibility.Visible ? "\u25BC" : "\u25B6";
            };

            TemplateGroupsPanel.Children.Add(headerBorder);
            TemplateGroupsPanel.Children.Add(itemsPanel);
        }

        private void GroupCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not CheckBox cb || cb.Tag is not string key) return;
            if (!_groups.TryGetValue(key, out var group)) return;
            bool check = cb.IsChecked == true;
            foreach (var item in group.Items.Where(i => !i.AlreadyExists))
            {
                item.IsSelected = check;
                if (item.CheckBox != null) item.CheckBox.IsChecked = check;
            }
            UpdateCount();
        }

        private void ItemCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb && cb.Tag is BimCaItem item)
            {
                item.IsSelected = cb.IsChecked == true;
                if (_groups.TryGetValue(item.DisciplineKey, out var group) && group.GroupCheckBox != null)
                {
                    var sel = group.Items.Where(i => !i.AlreadyExists).ToList();
                    group.GroupCheckBox.IsChecked = sel.All(i => i.IsSelected) ? true :
                                                   sel.Any(i => i.IsSelected) ? (bool?)null : false;
                }
            }
            UpdateCount();
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in _templateItems.Where(i => !i.AlreadyExists)) { item.IsSelected = true; if (item.CheckBox != null) item.CheckBox.IsChecked = true; }
            foreach (var g in _groups.Values) if (g.GroupCheckBox != null) g.GroupCheckBox.IsChecked = true;
            UpdateCount();
        }

        private void DeselectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in _templateItems) { item.IsSelected = false; if (item.CheckBox != null) item.CheckBox.IsChecked = false; }
            foreach (var g in _groups.Values) if (g.GroupCheckBox != null) g.GroupCheckBox.IsChecked = false;
            UpdateCount();
        }

        private void UpdateCount()
        {
            int count = _templateItems.Count(i => i.IsSelected);
            int total = _templateItems.Count(i => !i.AlreadyExists);
            SelectionCountText.Text = count > 0 ? $"{count} de {total} seleccionados" : "";
            CreateBtnLabel.Text     = count > 0 ? $"Crear {count} subproyectos" : "Crear seleccionados";
            BtnCreate.IsEnabled     = count > 0;
        }

        private void CreateSelectedWorksets_Click(object sender, RoutedEventArgs e)
        {
            var selected = _templateItems.Where(i => i.IsSelected && !i.AlreadyExists).ToList();
            if (selected.Count == 0) return;

            var confirm = MessageBox.Show(
                $"¿Crear {selected.Count} subproyectos en el modelo?",
                "BIM Pills — Estandarizar", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;

            int created = 0, failed = 0;
            foreach (var item in selected)
            {
                if (_createCallback != null && _createCallback(item.Name))
                {
                    CreatedWorksets.Add(new WorksetInfo { Name = item.Name, IsOpen = true, IsEditable = true, Owner = "", ElementCount = 0 });
                    item.AlreadyExists = true;
                    item.IsSelected    = false;
                    if (item.CheckBox != null) { item.CheckBox.IsChecked = false; item.CheckBox.IsEnabled = false; }
                    created++;
                }
                else failed++;
            }

            UpdateCount();
            var msg = $"Se crearon {created} subproyectos.";
            if (failed > 0) msg += $"\n{failed} no se pudieron crear.";
            MessageBox.Show(msg, "BIM Pills — Estandarizar", MessageBoxButton.OK, failed > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
        }

        private void LoadCustomTemplate_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "Texto (*.txt)|*.txt|Todos (*.*)|*.*", Title = "Cargar plantilla" };
            if (dlg.ShowDialog() != true) return;
            try { LoadLines(File.ReadAllLines(dlg.FileName)); }
            catch (Exception ex) { MessageBox.Show($"Error: {ex.Message}", "BIM Pills — Error"); }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private static IEnumerable<string> GetBuiltInLines()
        {
            return new[]
            {
                "01-ARQ-MUE-Mobiliario","01-ARQ-FIN-AcabadosInteriores","01-ARQ-MAM-MurosMamposteria",
                "01-ARQ-SUE-AcabadosSuelosExteriores","01-ARQ-SUI-AcabadosSuelosInteriores","01-ARQ-TYS-TabiquesTablayeso",
                "01-ARQ-FEX-AcabadosExteriores","01-ARQ-INT-Interiorismo","01-ARQ-RUT-RutasEvacuacion",
                "01-ARQ-CIE-CielosExteriores","01-ARQ-CII-CielosInteriores","01-ARQ-ENT-Entorno",
                "01-ARQ-BAS-ExtraccionBasura","01-ARQ-URB-MobiliarioUrbano","01-ARQ-CAR-Carpinteria",
                "01-ARQ-PAV-Pavimentos","01-ARQ-BCL-BasuraResiduosClinicos","01-ARQ-PAI-PaisajismoVegetacion",
                "01-ARQ-ACU-Acustica","01-ARQ-HER-Herreria","01-ARQ-PUE-Puertas","01-ARQ-VEN-Ventanas",
                "01-ARQ-MCO-MurosCortina","01-ARQ-SEV-SenaleticaVial","01-ARQ-SEI-SenaleticaInstitucional",
                "01-ARQ-SES-SenaleticaSeguridad",
                "05-ELE-ACO-AcometidaElectricidad","05-ELE-SAC-SubAcometidaElectricidad","05-ELE-CEL-CanalizacionElectrica",
                "05-ELE-TIE-TierraFisica","05-ELE-PRY-Pararrayos","05-ELE-ALU-Alumbrado","05-ELE-DEL-DispositivosElectricidad",
                "05-ELE-REG-FuerzaRegulada","05-ELE-UPS-FuerzaRespaldo","05-ELE-ATL-AcometidaTelecomunicaciones",
                "05-ELE-SAT-SubAcometidaTelecomunicaciones","05-ELE-CTL-CanalizacionTelecomunicaciones",
                "05-ELE-TEL-DispositivosTelecomunicaciones","05-ELE-SEG-Seguridad","05-ELE-CTV-CircuitoCerradoTV",
                "05-ELE-VOD-VozDatos","05-ELE-AUD-Audio","05-ELE-COD-CorrientesDebiles","05-ELE-RAD-Radiocomunicacion",
                "05-ELE-DOM-Domotica",
                "00-GEN-OPE-OperacionMantenimiento","00-GEN-PEL-MaterialesPeligrososNocivos","00-GEN-PRO-Procesos",
                "00-GEN-NIV-NivelesYEjesCompartidos","00-GEN-SCP-CajasYPlanosReferencia","00-GEN-ARE-Areas",
                "00-GEN-ESP-Espacios","00-GEN-HAB-Habitaciones","00-GEN-ARQ-VinculoArquitectura",
                "00-GEN-EST-VinculoEstructura","00-GEN-ELE-VinculoElectricidad","00-GEN-IHS-VinculoHidrosanitarias",
                "00-GEN-MEC-VinculoMecanicas","00-GEN-ESP-VinculoEspeciales","00-GEN-EMP-VinculoEmplazamiento",
                "02-OCV-TOP-Topografia","02-OCV-ACT-LevantamientoEstadoActual","02-OCV-GEO-Geotecnia",
                "02-OCV-MOV-PlataformasMovimientoTierra",
                "03-EST-CIM-Cimentacion","03-EST-MCC-MurosColumnasInSitu","03-EST-MCP-MurosColumnasPrefabricados",
                "03-EST-MCA-MurosColumnasAcero","03-EST-FYC-FachadasCubiertas","03-EST-VEC-VigasEntrepisosInSitu",
                "03-EST-VEP-VigasEntrepisosPrefabricados","03-EST-VEA-VigasEntrepisosAcero","03-EST-EDS-EstabilizacionSuelo",
                "03-EST-RBR-Armadura",
                "06-CLI-INY-InyeccionAireClimatizado","06-CLI-RET-RetornoAireClimatizado","06-CLI-EXA-ExtraccionAireViciadoClimatizado",
                "06-CLI-FRE-RenovacionAireFrescoClimatizacion","06-CLI-SRF-SuministroAguaHeladaRefrigeracion",
                "06-CLI-RRF-RetornoAguaHeladaRefrigeracion","06-CLI-SCL-SuministroAguaCalienteCalefaccion",
                "06-CLI-RCL-RetornoAguaCalienteCalefaccion",
                "06-VEN-FPA-SuministroVentilacionParqueos","06-VEN-EPA-ExtraccionForzadaParqueos",
                "06-VEN-SVV-SuministroVentilacionVivienda","06-VEN-EVV-ExtraccionForzadaVentilacionVivienda",
                "06-VEN-RVV-RetornoVentilacionVivienda","06-VEN-FVV-AportacionVentilacionVivienda",
                "06-VEN-ECO-ExtraccionCocinas","06-VEN-EEG-ExtraccionEscapeGases","06-VEN-FSA-SuministroAireFresco",
                "06-VEN-EAV-ExtraccionAireViciado","06-VEN-SSP-SistemaSobrepresion",
                "04-IHS-ASA-AparatosSanitarios","04-HID-AFA-AcometidaAguaFria","04-HID-AFR-SuministroAguaFria",
                "04-HID-ACS-SuministroAguaCaliente","04-HID-ACR-RetornoAguaCaliente","04-HID-RIE-Riego",
                "04-HID-APO-AguaPotable","04-HID-ANP-AguaNoPotable","04-SAN-VES-VentilacionSanitaria",
                "04-SAN-EXS-ExtraccionSecadoras","04-SAN-DAN-DrenajeAguasNegras","04-SAN-DAG-DrenajeAguasGrises",
                "04-SAN-DPL-DrenajePluvial","04-SAN-DIN-DrenajeInundacion","04-SAN-DCN-DrenajeCondensacion",
                "04-SAN-BIO-DesechosBiologicos","04-SAN-ATR-AguasTratadas","04-SAN-DPR-DrenajePresion",
                "04-SAN-DRD-DrenajeDescarga",
                "07-PCI-DET-DeteccionIncendio","07-PCI-RHU-RedHumedaBIES","07-PCI-RHS-RedHumedaSprinklers",
                "07-PCI-RSE-RedSeca","07-PCI-EXH-ExtincionIncendioHalon","07-PCI-EXG-ExtincionIncendioGasInerte",
                "07-PCI-EXT-ExtincionIncendioCO2",
                "08-IND-RFI-TuberiaDesconocida","08-IND-GLP-GasPropano","08-IND-VCB-VentilacionCombustible",
                "08-IND-CCB-CargaCombustible","08-IND-GVP-GasolinaExtraSuperior","08-IND-GSP-GasolinaSuperior",
                "08-IND-GRG-GasolinaRegular","08-IND-DSP-DieselSuperior","08-IND-DRG-DieselRegular",
                "08-IND-ACE-Aceite","08-IND-AIR-AireComprimido","08-IND-NIT-Nitrogeno","08-IND-VAP-VaporAltaPresion",
                "08-IND-VMP-VaporMediaPresion","08-IND-VBP-VaporBajaPresion","08-IND-VAC-Vacio",
                "08-IND-GLB-GasLaboratorio","08-IND-GMD-GasMedico","08-IND-OXI-Oxigeno",
                "09-EQU-EQU-Equipos"
            };
        }
    }

    internal class BimCaItem
    {
        public string Name          { get; set; } = "";
        public string DisciplineKey { get; set; } = "";
        public bool   IsSelected    { get; set; }
        public bool   AlreadyExists { get; set; }
        public CheckBox? CheckBox   { get; set; }
    }

    internal class BimCaGroup
    {
        public string       Key          { get; }
        public List<BimCaItem> Items     { get; } = new();
        public CheckBox?    GroupCheckBox { get; set; }
        public StackPanel?  ItemsPanel    { get; set; }
        public BimCaGroup(string key) { Key = key; }
    }
}
