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
using BIMPills.UI.Export;
using BIMPills.UI.Gestion;
using BIMPills.UI.Licensing;
using BIMPills.UI.MCPIntegration;
using BIMPills.UI.ModelAudit;
using BIMPills.Core.Updates;
using BIMPills.UI.Ordering;
using BIMPills.Core.ParameterExtractor;
using BIMPills.Infrastructure.Persistence;
using System.Linq;
using BIMPills.UI.LegendFromExcel;
using BIMPills.UI.Support;
using BIMPills.UI.Updates;
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
                        new ElementInfo(100123, "Importación DWG planta baja",  null, "Importación CAD", canDelete: true,
                            description: "Importación CAD embebida en el modelo. Si ya no la necesitas, puede eliminarse para reducir el peso del archivo."),
                        new ElementInfo(100456, "Generic Model [100456]",        null, "Importación CAD", canDelete: true,
                            description: "Importación CAD embebida en el modelo. Si ya no la necesitas, puede eliminarse para reducir el peso del archivo."),
                        new ElementInfo(100789, "Grupo_Baños_Tipo_A",            null, "Grupo",           canDelete: false,
                            description: "Grupo anclado en Revit — no se puede eliminar hasta desanclarlo manualmente."),
                        new ElementInfo(100890, "DWG_Planta_Vinculado",          null, "Importación CAD", canDelete: false,
                            description: "Enlace CAD (DWG/DXF) vinculado externamente. Para eliminarlo, desvincúlalo primero desde Administrar → Vínculos."),
                    },
                    PurgeableItems = new List<PurgeableItem>
                    {
                        new PurgeableItem(200001, "Familia_Sin_Usar_01",      "Mobiliario",       "Familia",        2_097_152),
                        new PurgeableItem(200002, "Familia_Sin_Usar_02",      "Puertas",          "Familia",        1_048_576),
                        new PurgeableItem(200003, "Vista_Sin_Colocar_Borrador","FloorPlan",       "Vista",          0        ),
                        new PurgeableItem(200004, "Sección Antigua TEST",     "Section",          "Vista",          0        ),
                        new PurgeableItem(200005, "Arial 2.5mm",              "Estilos de texto", "Estilo texto",   0        ),
                        new PurgeableItem(200006, "Nota 3.5 Negrita",         "Estilos de texto", "Estilo texto",   0        ),
                        new PurgeableItem(200007, "Lineal 1:100 BIM-CA",      "Tipos de cota",    "Tipo cota",      0        ),
                        new PurgeableItem(200008, "Angular Grados",           "Tipos de cota",    "Tipo cota",      0        ),
                        new PurgeableItem(200009, "Patrón Hormigón Rayado",   "Regiones rellenas","Patron relleno", 0        ),
                        new PurgeableItem(200010, "Material_Obsoleto",        "General",          "Material",       524_288  ),
                    }
                };

                // En sandbox, mock callback que simula la purga (solo muestra qué IDs se purgarían)
                Action<IReadOnlyList<long>> mockPurge = ids =>
                    MessageBox.Show($"[Sandbox] Purgar/eliminar {ids.Count} elementos:\n{string.Join(", ", ids)}",
                        "Sandbox — Mock purge");

                var win = new ModelAuditWindow(result, purgeCallback: mockPurge);
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

                // Unified exportable views: sheets + individual views (S6-B)
                var views = new List<ExportableViewInfo>
                {
                    // Sheets
                    new ExportableViewInfo(2001, "uid-001", "Planta General Nivel 1",    ExportableItemType.Sheet,    "A-001", "Rev 2", "Arquitectura"),
                    new ExportableViewInfo(2002, "uid-002", "Planta General Nivel 2",    ExportableItemType.Sheet,    "A-002", "Rev 2", "Arquitectura"),
                    new ExportableViewInfo(2003, "uid-003", "Corte Longitudinal A-A",    ExportableItemType.Sheet,    "A-101", "Rev 1", "Arquitectura"),
                    new ExportableViewInfo(2004, "uid-004", "Planta Estructural Nivel 1",ExportableItemType.Sheet,    "E-001", "Rev 1", "Estructura"),
                    new ExportableViewInfo(2005, "uid-005", "Planta Mec\u00e1nica Nivel 1",   ExportableItemType.Sheet,    "M-001", "Rev 0", "MEP"),
                    // Individual views
                    new ExportableViewInfo(3001, "uid-101", "Nivel 1",                   ExportableItemType.FloorPlan, "", "", "Arquitectura"),
                    new ExportableViewInfo(3002, "uid-102", "Nivel 2",                   ExportableItemType.FloorPlan, "", "", "Arquitectura"),
                    new ExportableViewInfo(3003, "uid-103", "Techo Nivel 1",             ExportableItemType.CeilingPlan, "", "", "Arquitectura"),
                    new ExportableViewInfo(3004, "uid-104", "Alzado Norte",              ExportableItemType.Elevation, "", "", "Arquitectura"),
                    new ExportableViewInfo(3005, "uid-105", "Alzado Sur",                ExportableItemType.Elevation, "", "", "Arquitectura"),
                    new ExportableViewInfo(3006, "uid-106", "Secci\u00f3n A-A",                ExportableItemType.Section,   "", "", "Arquitectura"),
                    new ExportableViewInfo(3007, "uid-107", "Vista 3D General",          ExportableItemType.ThreeDView, "", "", ""),
                    new ExportableViewInfo(3008, "uid-108", "Leyenda de Materiales",     ExportableItemType.Legend,    "", "", ""),
                    new ExportableViewInfo(3009, "uid-109", "Detalle Tipo Muro",         ExportableItemType.DraftingView, "", "", ""),
                };

                var win = new ExportarWindow();
                win.SetDocumentName("Proyecto_Sandbox_Demo.rvt");
                win.InitializeExportFamilies(families, documentTitle: "Proyecto_Sandbox_Demo");
                win.InitializeExportViews(views, projectName: "Proyecto Sandbox Demo");
                // Parámetros tab — mismos mocks que OpenExtractor_Click
                var tempDirSandbox = System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(), "BIMPillsSandbox_Extractor_" + Guid.NewGuid().ToString("N"));
                System.IO.Directory.CreateDirectory(tempDirSandbox);
                win.InitializeExtractor(
                    selectedElementCount: 12,
                    applyCallback: config =>
                    {
                        var rules = string.Join("\n", config.Rules.Select((r, i) =>
                            $"{i + 1}. {r.Source} → {r.Target.ParameterName}"));
                        BIMPills.UI.Shared.BimPillsDialog.Info(
                            header:  "Extractor de Parámetros",
                            message: $"{config.Rules.Count} reglas aplicadas a 12 elementos (mock)",
                            detail:  $"Unidades: {config.LengthUnits} · Decimales: {config.Decimals}\n\n{rules}",
                            owner:   win);
                        return true;
                    },
                    presetRepository: new JsonExtractionPresetRepository(tempDirSandbox),
                    availableCategories: new[] { "Muros", "Puertas", "Ventanas", "Habitaciones", "Mobiliario" });

                win.InitializeExportModel(
                    "Proyecto_Sandbox_Demo.rvt",
                    activeViewName: "Planta Nivel 1",
                    availableParameters: new List<string> { "N\u00famero de proyecto", "Nombre del proyecto", "Cliente", "Fase" },
                    parameterValues: new Dictionary<string, string>
                    {
                        ["N\u00famero de proyecto"] = "2024-001",
                        ["Nombre del proyecto"]    = "Torre_Habitacional",
                        ["Cliente"]                = "Constructora_XYZ",
                        ["Fase"]                   = "SD"
                    },
                    availableViews: new List<BIMPills.Core.Models.NwcViewInfo>
                    {
                        new BIMPills.Core.Models.NwcViewInfo { ElementId = 101, Name = "{3D} - Vista general" },
                        new BIMPills.Core.Models.NwcViewInfo { ElementId = 102, Name = "{3D} - Coordinaci\u00f3n MEP" },
                        new BIMPills.Core.Models.NwcViewInfo { ElementId = 103, Name = "{3D} - Estructura" },
                        new BIMPills.Core.Models.NwcViewInfo { ElementId = 104, Name = "{3D} - S\u00f3tano" },
                        new BIMPills.Core.Models.NwcViewInfo { ElementId = 105, Name = "{3D} - Fachada Norte" },
                        new BIMPills.Core.Models.NwcViewInfo { ElementId = 106, Name = "{3D} - Revisi\u00f3n cliente" },
                    },
                    presets: new List<BIMPills.Core.Models.NwcExportPreset>
                    {
                        new BIMPills.Core.Models.NwcExportPreset
                        {
                            Name = "Config NWC est\u00e1ndar",
                            Config = new BIMPills.Core.Models.NwcExportConfig
                            {
                                Scope = BIMPills.Core.Models.NwcExportScope.Model,
                                ExportLinks = true,
                                FileNameTemplate = "{N\u00famero de proyecto}_{Nombre del proyecto}"
                            }
                        }
                    });
                win.Show();
            }
            catch (Exception ex)
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("Error abriendo ExportarWindow:");
                sb.AppendLine();
                var cur = ex;
                int level = 0;
                while (cur != null)
                {
                    sb.AppendLine($"[{level}] {cur.GetType().FullName}");
                    sb.AppendLine($"    Message: {cur.Message}");
                    if (!string.IsNullOrEmpty(cur.StackTrace))
                    {
                        var firstLines = cur.StackTrace.Split('\n');
                        for (int i = 0; i < firstLines.Length && i < 6; i++)
                            sb.AppendLine("    " + firstLines[i].Trim());
                    }
                    sb.AppendLine();
                    cur = cur.InnerException;
                    level++;
                }
                try { System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "bimpills-sandbox-exportar-error.log"), sb.ToString()); } catch { }
                MessageBox.Show(sb.ToString(), "Sandbox — Error");
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
                // Keynotes: no real file in sandbox — panel loads mock data automatically
                win.InitializeKeynotes();
                win.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error abriendo GestionarWindow:\n{ex.Message}", "Sandbox \u2014 Error");
            }
        }

        // ── Transferir ───────────────────────────────────────────────────────────

        private void OpenTransferir_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var openDocs = new List<OpenDocumentInfo>
                {
                    new OpenDocumentInfo { Title = "Proyecto_Sandbox_Demo.rvt", PathName = @"C:\Projects\Demo.rvt", IsCurrent = true },
                    new OpenDocumentInfo { Title = "Plantilla_Corporativa.rvt", PathName = @"C:\Templates\Corp.rvt", IsCurrent = false },
                    new OpenDocumentInfo { Title = "Proyecto_Referencia.rvt",   PathName = @"C:\Projects\Ref.rvt",   IsCurrent = false },
                };

                var mockTemplates = new Dictionary<string, List<ViewTemplateInfo>>
                {
                    ["Plantilla_Corporativa.rvt"] = new List<ViewTemplateInfo>
                    {
                        new ViewTemplateInfo { Id = 5001, Name = "ARQ - Planta Presentaci\u00f3n",  ViewType = "FloorPlan",   FilterCount = 3 },
                        new ViewTemplateInfo { Id = 5002, Name = "ARQ - Planta Trabajo",       ViewType = "FloorPlan",   FilterCount = 2 },
                        new ViewTemplateInfo { Id = 5003, Name = "ARQ - Alzado Presentaci\u00f3n",  ViewType = "Elevation",   FilterCount = 4 },
                        new ViewTemplateInfo { Id = 5004, Name = "ARQ - Secci\u00f3n Detalle",      ViewType = "Section",     FilterCount = 1 },
                        new ViewTemplateInfo { Id = 5005, Name = "EST - Planta Estructura",    ViewType = "FloorPlan",   FilterCount = 5 },
                        new ViewTemplateInfo { Id = 5006, Name = "MEP - Planta Mec\u00e1nica",      ViewType = "FloorPlan",   FilterCount = 6 },
                        new ViewTemplateInfo { Id = 5007, Name = "3D - Vista Isom\u00e9trica",      ViewType = "ThreeDView",  FilterCount = 0 },
                    },
                    ["Proyecto_Referencia.rvt"] = new List<ViewTemplateInfo>
                    {
                        new ViewTemplateInfo { Id = 6001, Name = "Planta B\u00e1sica",   ViewType = "FloorPlan",  FilterCount = 1 },
                        new ViewTemplateInfo { Id = 6002, Name = "Corte Simple",    ViewType = "Section",    FilterCount = 0 },
                    }
                };

                var mockDetails = new Dictionary<long, ViewTemplateDetail>
                {
                    [5001] = new ViewTemplateDetail
                    {
                        Name = "ARQ - Planta Presentaci\u00f3n", ViewType = "FloorPlan", AssignedViewCount = 4,
                        Parameters = new List<ViewTemplateParameter>
                        {
                            new ViewTemplateParameter { Name = "Escala de vista",          Value = "1 : 100",          IsComplex = false, Include = false },
                            new ViewTemplateParameter { Name = "Nivel de detalle",          Value = "Fino",             IsComplex = false, Include = true  },
                            new ViewTemplateParameter { Name = "Modelo (V/G)",              Value = "",                 IsComplex = true,  Include = true  },
                            new ViewTemplateParameter { Name = "Anotaci\u00f3n (V/G)",      Value = "",                 IsComplex = true,  Include = true  },
                            new ViewTemplateParameter { Name = "Filtros (V/G)",             Value = "",                 IsComplex = true,  Include = false },
                            new ViewTemplateParameter { Name = "Orientaci\u00f3n",          Value = "Norte de proyecto",IsComplex = false, Include = true  },
                            new ViewTemplateParameter { Name = "Disciplina",                Value = "Arquitectura",     IsComplex = false, Include = true  },
                        }
                    },
                    [5005] = new ViewTemplateDetail
                    {
                        Name = "EST - Planta Estructura", ViewType = "FloorPlan", AssignedViewCount = 7,
                        Parameters = new List<ViewTemplateParameter>
                        {
                            new ViewTemplateParameter { Name = "Escala de vista",           Value = "1 : 50",           IsComplex = false, Include = false },
                            new ViewTemplateParameter { Name = "Nivel de detalle",           Value = "Medio",            IsComplex = false, Include = true  },
                            new ViewTemplateParameter { Name = "Modelo (V/G)",               Value = "",                 IsComplex = true,  Include = true  },
                            new ViewTemplateParameter { Name = "Filtros (V/G)",              Value = "",                 IsComplex = true,  Include = true  },
                            new ViewTemplateParameter { Name = "Disciplina",                 Value = "Estructura",       IsComplex = false, Include = true  },
                        }
                    }
                };

                var win = new BIMPills.UI.Transfer.TransferWindow();
                win.SetModelName("Proyecto_Sandbox_Demo.rvt");
                win.InitializeViewTemplates(
                    openDocs,
                    getTemplatesCallback: docTitle =>
                        mockTemplates.TryGetValue(docTitle, out var list) ? list : new List<ViewTemplateInfo>(),
                    getDetailCallback: (docTitle, templateId) =>
                        mockDetails.TryGetValue(templateId, out var detail) ? detail : null);
                // Filters and Standards use built-in mock data when no callbacks are passed
                win.InitializeViewFilters(openDocs);
                win.InitializeProjectStandards(openDocs);
                win.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error abriendo TransferWindow:\n{ex.Message}", "Sandbox \u2014 Error");
            }
        }

        // ── Dibujar (Leyenda desde Excel) ───────────────────────────────────────

        private void OpenDibujar_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var textStyles = new List<BIMPills.Core.LegendFromExcel.RevitStyleInfo>
                {
                    new BIMPills.Core.LegendFromExcel.RevitStyleInfo { Id = 1, Name = "Arial 2.5mm" },
                    new BIMPills.Core.LegendFromExcel.RevitStyleInfo { Id = 2, Name = "Arial 3.5mm" },
                    new BIMPills.Core.LegendFromExcel.RevitStyleInfo { Id = 3, Name = "Nota 2.5mm Negrita" },
                };
                var lineStyles = new List<BIMPills.Core.LegendFromExcel.RevitStyleInfo>
                {
                    new BIMPills.Core.LegendFromExcel.RevitStyleInfo { Id = 10, Name = "<Hidden Lines>" },
                    new BIMPills.Core.LegendFromExcel.RevitStyleInfo { Id = 11, Name = "Líneas de techo" },
                    new BIMPills.Core.LegendFromExcel.RevitStyleInfo { Id = 12, Name = "Thin Lines" },
                };
                var fillTypes = new List<BIMPills.Core.LegendFromExcel.RevitStyleInfo>
                {
                    new BIMPills.Core.LegendFromExcel.RevitStyleInfo { Id = 20, Name = "Diagonal arriba" },
                    new BIMPills.Core.LegendFromExcel.RevitStyleInfo { Id = 21, Name = "Sólido" },
                };

                var win = new BIMPills.UI.Documentacion.DocumentacionWindow();
                win.SetDocumentName("Proyecto_Sandbox_Demo.rvt");
                win.InitializeDibujar(textStyles, lineStyles, fillTypes,
                    drawCallback: (filePath, options) =>
                    {
                        MessageBox.Show(
                            $"[Sandbox] Dibujar leyenda\n" +
                            $"Archivo: {filePath}\n" +
                            $"Vista: {options.ViewName}\n" +
                            $"Tamaño celda: {options.CellWidthMm}×{options.CellHeightMm} mm",
                            "Sandbox — Dibujar mock", MessageBoxButton.OK, MessageBoxImage.Information);
                        return true;
                    });
                win.NavigateToTab("leyenda");
                win.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error abriendo Dibujar:\n{ex.Message}\n\n{ex.StackTrace}", "Sandbox — Error");
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
                win.InitializeDibujar(
                    new List<BIMPills.Core.LegendFromExcel.RevitStyleInfo>
                    {
                        new BIMPills.Core.LegendFromExcel.RevitStyleInfo { Id = 1, Name = "Arial 2.5mm" },
                        new BIMPills.Core.LegendFromExcel.RevitStyleInfo { Id = 2, Name = "Arial 3.5mm" },
                    },
                    new List<BIMPills.Core.LegendFromExcel.RevitStyleInfo>
                    {
                        new BIMPills.Core.LegendFromExcel.RevitStyleInfo { Id = 10, Name = "<Hidden Lines>" },
                        new BIMPills.Core.LegendFromExcel.RevitStyleInfo { Id = 11, Name = "Thin Lines" },
                    },
                    new List<BIMPills.Core.LegendFromExcel.RevitStyleInfo>
                    {
                        new BIMPills.Core.LegendFromExcel.RevitStyleInfo { Id = 20, Name = "Sólido" },
                    });
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

        // ── BimPillsDialog showcase ──────────────────────────────────────────────

        private void OpenBimPillsDialogs_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var win = new BimPillsDialogShowcase { Owner = this };
                win.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error abriendo BimPillsDialogShowcase:\n{ex.Message}", "Sandbox \u2014 Error");
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

        // ── Licencia vencida ─────────────────────────────────────────────────────

        private void OpenLicenseExpired_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var win = new BIMPills.UI.Licensing.LicenseExpiredWindow { Owner = this, HolderName = "Rodrigo Flores" };
                var result = win.ShowDialog();
                if (result == true)
                    MessageBox.Show("El usuario eligió renovar → se abriría LicenseActivationWindow.", "Sandbox — Info");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error abriendo LicenseExpiredWindow:\n{ex.Message}", "Sandbox — Error");
            }
        }

        // ── Diagnóstico: detección de impresoras PDF ─────────────────────────

        private void OpenUpdateAvailable_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var fakeUpdate = new UpdateInfo
                {
                    TagName      = "beta-1.1",
                    ReleaseNotes =
                        "## Novedades en beta 1.1\n\n" +
                        "- **Auditoría:** Corrección del peso del archivo en modelos colaborativos (BIM 360/ACC/Revit Server)\n" +
                        "- **Auditoría:** Barras de metodología ahora se llenan según el puntaje real\n" +
                        "- **UI:** Selección múltiple con Shift+Click en pestaña Purgables\n" +
                        "- **Auto-update:** Sistema de actualización automática desde GitHub Releases\n" +
                        "- **Diálogos:** Reemplazo completo de MessageBox por BimPillsDialog",
                    InstallerDownloadUrl = "https://github.com/BIM-CA/bim-pills/releases/download/beta-1.1/BIMPills-setup.exe",
                };

                // Simular descarga: espera 2 segundos y retorna ruta ficticia
                async Task<string?> FakeDownload(UpdateInfo u)
                {
                    await Task.Delay(2000);
                    return @"C:\Temp\BIMPills_update_setup.exe";
                }

                var win = new UpdateAvailableWindow(fakeUpdate, "beta 1.0", FakeDownload)
                {
                    Owner = this
                };
                win.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Sandbox — Update");
            }
        }

        private void OpenPrinterDiag_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var printers = BIMPills.Infrastructure.Services.PdfPrinterService.GetInstalledPdfPrinters();
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"Impresoras PDF detectadas: {printers.Count}");
                sb.AppendLine();
                foreach (var p in printers)
                {
                    sb.AppendLine($"  [{p.Rank}] {p.DisplayName}");
                    sb.AppendLine($"      Sistema: {p.SystemName}");
                    sb.AppendLine($"      Silencioso: {p.SupportsSilent}");
                }

                if (printers.Count == 0)
                    sb.AppendLine("  (ninguna impresora PDF detectada)");

                // Show diag log path
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var logPath = System.IO.Path.Combine(appData, "Autodesk", "Revit", "Addins", "BIMPills", "pdf-printer-diag.log");
                sb.AppendLine();
                sb.AppendLine($"Log de diagnóstico: {logPath}");

                MessageBox.Show(sb.ToString(), "Sandbox — Impresoras PDF", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error detectando impresoras:\n{ex.Message}\n\n{ex.StackTrace}", "Sandbox — Error");
            }
        }

        // ── Soporte (chat flotante) ──────────────────────────────────────────────

        private BIMPills.UI.Support.SupportWindow? _supportWindow;

        private void OpenSupport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_supportWindow == null || !_supportWindow.IsLoaded)
                {
                    _supportWindow = new BIMPills.UI.Support.SupportWindow();
                    _supportWindow.ShowAnimated();
                }
                else if (_supportWindow.IsVisible)
                {
                    _supportWindow.Hide();
                }
                else
                {
                    _supportWindow.ShowAnimated();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error abriendo soporte:\n{ex.Message}\n\n{ex.StackTrace}",
                                "Sandbox — Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

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

        public Task<LicenseInfo?> ValidateAsync(string licenseKey, bool forceRefresh = false)
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

        public IReadOnlyList<ExportableViewInfo> GetExportableViews() => new List<ExportableViewInfo>
        {
            new ExportableViewInfo(2001, "uid-2001", "Planta General Nivel 1", ExportableItemType.Sheet, "A-001", "Rev 2", "Arquitectura"),
            new ExportableViewInfo(2002, "uid-2002", "Planta General Nivel 2", ExportableItemType.Sheet, "A-002", "Rev 2", "Arquitectura"),
            new ExportableViewInfo(3001, "uid-3001", "Planta Nivel 1", ExportableItemType.FloorPlan),
            new ExportableViewInfo(3002, "uid-3002", "Vista 3D Coordinación", ExportableItemType.ThreeDView),
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
