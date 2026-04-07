using BIMPills.Commands.Gestion;
using BIMPills.Commands.ModelAudit;
using BIMPills.Core.About;
using BIMPills.Core.Audit;
using BIMPills.Core.Documentacion;
using BIMPills.Core.Gestion;
using BIMPills.Core.Licensing;
using BIMPills.Core.Models;
using BIMPills.Core.Services;
using BIMPills.Infrastructure.DI;
using BIMPills.UI.About;
using BIMPills.UI.CustomDimensionSchemes;
using BIMPills.UI.DataManager;
using BIMPills.UI.Documentacion;
using BIMPills.UI.ExportFamilies;
using BIMPills.UI.Gestion;
using BIMPills.UI.Licensing;
using BIMPills.UI.MCPIntegration;
using BIMPills.UI.ModelAudit;
using BIMPills.UI.Ordering;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;

namespace BIMPills.UI.Sandbox
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        // ── Auditar ─────────────────────────────────────────────────────────────

        private void OpenAuditar_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = new ModelAuditResult
                {
                    DocumentTitle  = "Proyecto_Sandbox_Demo.rvt",
                    IsWorkshared   = true,
                    FileSizeBytes  = 52_428_800, // 50 MB
                    TotalElements  = 12_450,
                    HealthScore    = new ModelHealthScore(
                        warningsCount:     45,
                        fileSizeMB:        50.0,
                        largestFamilyMB:   8.5,
                        totalElements:     12_450,
                        unplacedViewsCount: 12,
                        purgeableCount:    28),
                    Warnings = new List<ModelWarningInfo>
                    {
                        new ModelWarningInfo("Elementos superpuestos detectados en nivel 1", "Medio", 14),
                        new ModelWarningInfo("Familias con geometría incorrecta",             "Alto",   3),
                        new ModelWarningInfo("Parámetros compartidos sin usar",               "Bajo",  28),
                    },
                    Families = new List<FamilyInfo>
                    {
                        new FamilyInfo("Puerta_Abatible_Simple",   "Puertas",    32,  8_912_896),
                        new FamilyInfo("Ventana_Corrediza_2H",     "Ventanas",   18,  5_242_880),
                        new FamilyInfo("Silla_Oficina_Ergonomica", "Mobiliario", 45, 12_582_912),
                        new FamilyInfo("Mesa_Reuniones_6P",        "Mobiliario",  8,  9_437_184),
                        new FamilyInfo("Columna_Redonda_D30",      "Columnas",   24,  3_145_728),
                    },
                    UnplacedViews = new List<ViewInfo>
                    {
                        new ViewInfo("Planta Nivel 1 - BORRADOR", "FloorPlan", false),
                        new ViewInfo("Sección A-A - ANTIGUA",     "Section",   false),
                        new ViewInfo("Elevación Norte - TEST",     "Elevation", false),
                    },
                    OrphanElements = new List<ElementInfo>
                    {
                        new ElementInfo(100123, "Generic Model [100123]", null),
                        new ElementInfo(100456, "Generic Model [100456]", null),
                    },
                    PurgeableItems = new List<PurgeableItem>
                    {
                        new PurgeableItem(200001, "Familia_Sin_Usar_01",  "Mobiliario", "Familia",  2_097_152),
                        new PurgeableItem(200002, "Vista_Sin_Colocar_02", "FloorPlan",  "Vista",    0        ),
                        new PurgeableItem(200003, "Material_Obsoleto_03", "General",    "Material", 524_288  ),
                    }
                };

                var win = new ModelAuditWindow(result, purgeCallback: null);
                win.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error abriendo ModelAuditWindow:\n{ex.Message}", "Sandbox — Error");
            }
        }

        // ── Exportar ─────────────────────────────────────────────────────────────

        private void OpenExportar_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var families = new List<FamilyExportInfo>
                {
                    new FamilyExportInfo(1001, "Puerta_Abatible_Simple",   "Puertas"),
                    new FamilyExportInfo(1002, "Ventana_Corrediza_2H",     "Ventanas"),
                    new FamilyExportInfo(1003, "Silla_Oficina_Ergonomica", "Mobiliario"),
                    new FamilyExportInfo(1004, "Mesa_Reuniones_6P",        "Mobiliario"),
                    new FamilyExportInfo(1005, "Columna_Redonda_D30",      "Columnas"),
                };

                var sheets = new List<SheetExportInfo>
                {
                    new SheetExportInfo(2001, "A-001", "Planta General Nivel 1",    "Rev 2", "Arquitectura"),
                    new SheetExportInfo(2002, "A-002", "Planta General Nivel 2",    "Rev 2", "Arquitectura"),
                    new SheetExportInfo(2003, "A-101", "Corte Longitudinal A-A",    "Rev 1", "Arquitectura"),
                    new SheetExportInfo(2004, "E-001", "Planta Estructural Nivel 1","Rev 1", "Estructura"),
                    new SheetExportInfo(2005, "M-001", "Planta Mecánica Nivel 1",   "Rev 0", "MEP"),
                };

                var win = new ExportarWindow();
                win.SetDocumentName("Proyecto_Sandbox_Demo.rvt");
                win.InitializeExportFamilies(families, documentTitle: "Proyecto_Sandbox_Demo");
                win.InitializeExportSheets(sheets, projectName: "Proyecto Sandbox Demo");
                win.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error abriendo ExportarWindow:\n{ex.Message}", "Sandbox — Error");
            }
        }

        // ── Conectar (MCP) ───────────────────────────────────────────────────────

        private void OpenConectar_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var win = new MCPConnectionWindow();
                win.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error abriendo MCPConnectionWindow:\n{ex.Message}", "Sandbox — Error");
            }
        }

        // ── Ordenar ──────────────────────────────────────────────────────────────

        private void OpenOrdenar_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Mock category provider by element type
                IReadOnlyList<string> GetCategoriesByType(string type) => type == "Modelo"
                    ? new List<string> { "Puertas", "Ventanas", "Muros", "Columnas", "Mobiliario", "Escaleras" }
                    : new List<string> { "Texto", "Cotas", "Etiquetas de puerta", "Etiquetas de habitación" };

                // Mock parameter provider by category
                IReadOnlyList<string> GetParametersByCategory(string category) => category switch
                {
                    "Puertas"   => new List<string> { "Número de puerta", "Marca", "Comentarios" },
                    "Ventanas"  => new List<string> { "Número de ventana", "Marca", "Comentarios" },
                    "Columnas"  => new List<string> { "Marca", "Comentarios", "Número de columna" },
                    "Mobiliario"=> new List<string> { "Marca", "Comentarios" },
                    _           => new List<string> { "Marca", "Comentarios" }
                };

                var win = new OrdenarWindow(GetCategoriesByType, GetParametersByCategory);
                win.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error abriendo OrdenarWindow:\n{ex.Message}", "Sandbox — Error");
            }
        }

        // ── Gestionar (Tablas) ───────────────────────────────────────────────────

        private void OpenGestionar_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var mockServices = new MockDocumentServices();
                var win = new GestionarWindow(mockServices, "Proyecto_Sandbox_Demo.rvt");
                win.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error abriendo GestionarWindow:\n{ex.Message}", "Sandbox — Error");
            }
        }

        // ── Documentar (Acotar) ──────────────────────────────────────────────────

        private void OpenDocumentar_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dimTypes = new List<DimensionTypeInfo>
                {
                    new DimensionTypeInfo(3001, "Cota lineal 2.5mm"),
                    new DimensionTypeInfo(3002, "Cota angular 2.5mm"),
                    new DimensionTypeInfo(3003, "Cota diámetro 2mm"),
                };

                var data = new AcotadoVanosData(
                    doorCount:      24,
                    dimensionTypes: dimTypes,
                    activeViewName: "Planta Nivel 1",
                    gridCount:      8,
                    wallCount:      56,
                    levelCount:     4);

                var win = new DocumentacionWindow();
                win.SetDocumentName("Proyecto_Sandbox_Demo.rvt");
                win.InitializeAcotado(data, executeCallback: null);
                win.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error abriendo DocumentacionWindow:\n{ex.Message}", "Sandbox — Error");
            }
        }

        // ── Estandarizar (Worksets / Gestion) ────────────────────────────────────

        private void OpenEstandarizar_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = new GestionResult
                {
                    DocumentTitle = "Proyecto_Sandbox_Demo.rvt",
                    IsWorkshared  = true,
                    Worksets      = new List<WorksetInfo>
                    {
                        new WorksetInfo { Id = 1, Name = "ARQ - Arquitectura",    IsOpen = true,  IsDefault = true,  IsEditable = true,  Owner = "rflores",  ElementCount = 4200 },
                        new WorksetInfo { Id = 2, Name = "EST - Estructura",      IsOpen = true,  IsDefault = false, IsEditable = false, Owner = "jperez",   ElementCount = 1850 },
                        new WorksetInfo { Id = 3, Name = "MEP - Instalaciones",   IsOpen = false, IsDefault = false, IsEditable = false, Owner = "",         ElementCount = 920  },
                        new WorksetInfo { Id = 4, Name = "EXT - Exteriores",      IsOpen = true,  IsDefault = false, IsEditable = true,  Owner = "rflores",  ElementCount = 640  },
                        new WorksetInfo { Id = 5, Name = "Workset compartido",    IsOpen = true,  IsDefault = false, IsEditable = true,  Owner = "",         ElementCount = 210  },
                    }
                };

                var win = new GestionWindow(result, createCallback: null, renameCallback: null);
                win.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error abriendo GestionWindow:\n{ex.Message}", "Sandbox — Error");
            }
        }

        // ── Esquemas Personalizados ──────────────────────────────────────────────

        private void OpenCustomSchemes_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var win = new CustomDimensionSchemesWindow();
                win.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error abriendo CustomDimensionSchemesWindow:\n{ex.Message}", "Sandbox — Error");
            }
        }

        // ── Activación de licencia ───────────────────────────────────────────────

        private void OpenActivacion_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var win = new LicenseActivationWindow();
                win.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error abriendo LicenseActivationWindow:\n{ex.Message}", "Sandbox — Error");
            }
        }

        // ── Acerca de (variantes de licencia) ────────────────────────────────────

        private void OpenAboutNoLicense_Click(object sender, RoutedEventArgs e)
            => OpenAboutWithLicense(null);

        private void OpenAboutActive_Click(object sender, RoutedEventArgs e)
            => OpenAboutWithLicense(new MockLicenseService
            {
                MockLicense = new LicenseInfo
                {
                    Plan       = "Pro Anual",
                    Status     = "Activo",
                    HolderName = "Rodrigo Flores",
                    ExpiresAt  = DateTime.Today.AddMonths(11),
                    ValidatedAt = DateTime.UtcNow
                }
            });

        private void OpenAboutGrace_Click(object sender, RoutedEventArgs e)
            => OpenAboutWithLicense(new MockLicenseService
            {
                MockLicense = new LicenseInfo
                {
                    Plan       = "Pro Mensual",
                    Status     = "Grace Period",
                    HolderName = "Rodrigo Flores",
                    ExpiresAt  = DateTime.Today.AddDays(-3),
                    ValidatedAt = DateTime.UtcNow
                }
            });

        private void OpenAboutWithLicense(MockLicenseService? licenseService)
        {
            try
            {
                if (licenseService != null)
                    ServiceLocator.Register<ILicenseService>(licenseService);
                else if (ServiceLocator.IsRegistered<ILicenseService>())
                    ServiceLocator.Register<ILicenseService>(new MockLicenseService());

                var info = new AboutInfo();
                var win  = new AboutWindow(info);
                win.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error abriendo AboutWindow:\n{ex.Message}", "Sandbox — Error");
            }
        }
    }

    // ── Mock ILicenseService ─────────────────────────────────────────────────────

    internal sealed class MockLicenseService : ILicenseService
    {
        public LicenseInfo? MockLicense { get; set; }

        public bool IsValid => MockLicense != null &&
            (MockLicense.Status == "Activo" || MockLicense.Status == "Grace Period");

        public bool IsActivated => MockLicense != null;

        public bool IsExpired => MockLicense != null &&
            MockLicense.Status != "Activo" && MockLicense.Status != "Grace Period";

        public bool IsGracePeriod => MockLicense?.Status == "Grace Period";

        public LicenseInfo? GetCachedLicense() => MockLicense;

        public Task<LicenseInfo?> ValidateAsync(string licenseKey)
            => Task.FromResult(MockLicense);

        public Task<bool> ActivateAsync(string licenseKey, string machineId)
            => Task.FromResult(false);

        public Task<bool> DeactivateAsync()
        {
            MockLicense = null;
            return Task.FromResult(true);
        }
    }

    // ── Mock IDocumentServices ───────────────────────────────────────────────────

    /// <summary>
    /// Minimal mock implementation of IDocumentServices for the UI Sandbox.
    /// Returns plausible fake data so windows render without a live Revit document.
    /// </summary>
    internal sealed class MockDocumentServices : IDocumentServices
    {
        public string Title        => "Proyecto_Sandbox_Demo.rvt";
        public bool   IsWorkshared => true;

        public long   GetModelFileSize()       => 52_428_800;
        public int    GetTotalElementCount()   => 12_450;
        public string GetActiveViewName()      => "Planta Nivel 1";
        public int    GetDoorCountInActiveView() => 24;
        public int    GetGridCountInActiveView() => 8;
        public int    GetWallCountInActiveView() => 56;
        public int    GetArqLevelCount()         => 4;
        public string GetProjectName()           => "Proyecto Sandbox Demo";

        public IReadOnlyList<ModelWarningInfo>  GetWarnings()              => new List<ModelWarningInfo>();
        public IReadOnlyList<FamilyInfo>        GetFamilySizes()           => new List<FamilyInfo>();
        public IReadOnlyList<ViewInfo>          GetUnplacedViews()         => new List<ViewInfo>();
        public IReadOnlyList<ElementInfo>       GetElementsWithoutCategory() => new List<ElementInfo>();
        public IReadOnlyList<PurgeableItem>     GetPurgeableElements()     => new List<PurgeableItem>();
        public IReadOnlyList<DimensionTypeInfo> GetDimensionTypes()        => new List<DimensionTypeInfo>
        {
            new DimensionTypeInfo(3001, "Cota lineal 2.5mm"),
            new DimensionTypeInfo(3002, "Cota angular 2.5mm"),
        };

        public IReadOnlyList<FamilyExportInfo> GetLoadedFamilies() => new List<FamilyExportInfo>
        {
            new FamilyExportInfo(1001, "Puerta_Abatible_Simple",   "Puertas"),
            new FamilyExportInfo(1002, "Ventana_Corrediza_2H",     "Ventanas"),
        };

        public IReadOnlyList<WorksetInfo> GetWorksets() => new List<WorksetInfo>
        {
            new WorksetInfo { Id = 1, Name = "ARQ - Arquitectura", IsOpen = true, IsDefault = true, IsEditable = true, Owner = "rflores", ElementCount = 4200 },
            new WorksetInfo { Id = 2, Name = "EST - Estructura",   IsOpen = true, IsDefault = false, IsEditable = false, Owner = "jperez",  ElementCount = 1850 },
        };

        public IReadOnlyList<SheetExportInfo> GetSheets() => new List<SheetExportInfo>
        {
            new SheetExportInfo(2001, "A-001", "Planta General Nivel 1", "Rev 2", "Arquitectura"),
            new SheetExportInfo(2002, "A-002", "Planta General Nivel 2", "Rev 2", "Arquitectura"),
        };

        public IReadOnlyList<ScheduleInfo> GetSchedules() => new List<ScheduleInfo>
        {
            new ScheduleInfo { Id = 5001, Name = "Planilla de Puertas",   CategoryName = "Puertas",    RowCount = 32,  ColumnCount = 5 },
            new ScheduleInfo { Id = 5002, Name = "Planilla de Ventanas",  CategoryName = "Ventanas",   RowCount = 18,  ColumnCount = 4 },
            new ScheduleInfo { Id = 5003, Name = "Planilla de Materiales",CategoryName = "Materiales", RowCount = 120, ColumnCount = 6 },
        };

        public ScheduleData GetScheduleData(long scheduleId)
        {
            return new ScheduleData
            {
                Schedule = new ScheduleInfo { Id = scheduleId, Name = "Planilla Mock", CategoryName = "Mock", RowCount = 3, ColumnCount = 3 },
                Columns  = new List<ScheduleColumnInfo>
                {
                    new ScheduleColumnInfo { Name = "Marca",      ParameterName = "Mark",      IsReadOnly = true,  StorageType = "String" },
                    new ScheduleColumnInfo { Name = "Comentarios",ParameterName = "Comments",  IsReadOnly = false, StorageType = "String" },
                    new ScheduleColumnInfo { Name = "Recuento",   ParameterName = "Count",     IsReadOnly = true,  StorageType = "Integer"},
                },
                ElementIds = new List<long> { 10001, 10002, 10003 },
                Rows = new List<List<string>>
                {
                    new List<string> { "P-001", "Puerta principal", "1" },
                    new List<string> { "P-002", "Puerta secundaria","2" },
                    new List<string> { "P-003", "",                 "1" },
                }
            };
        }

        public bool   ExportFamily(long familyId, string destinationPath) => true;
        public int    PurgeElements(IReadOnlyList<long> elementIds)       => elementIds.Count;
        public bool   CreateWorkset(string name)                          => true;
        public bool   RenameWorkset(long worksetId, string newName)       => true;

        public ParameterUpdateResult ApplyParameterUpdates(IReadOnlyList<ParameterUpdateRequest> updates)
            => new ParameterUpdateResult { Updated = updates.Count, Skipped = 0 };
    }
}
