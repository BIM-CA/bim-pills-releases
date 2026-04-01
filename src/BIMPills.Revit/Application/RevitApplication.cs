using Autodesk.Revit.UI;
using BIMPills.Commands.About;
using BIMPills.Commands.CustomDimensionSchemes;
using BIMPills.Commands.DataManager;
using BIMPills.Commands.Documentacion;
using BIMPills.Commands.ExportFamilies;
using BIMPills.Commands.Gestion;
using BIMPills.Commands.MCPIntegration;
using BIMPills.Commands.ModelAudit;
using BIMPills.Commands.Ordering;
using BIMPills.Core.Licensing;
using BIMPills.Core.Modules;
using BIMPills.Core.Services;
using BIMPills.Infrastructure.DI;
using BIMPills.Infrastructure.Licensing;
using BIMPills.Infrastructure.Logging;
using BIMPills.Infrastructure.Persistence;
using BIMPills.Infrastructure.Services;
using BIMPills.Revit.Compatibility;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace BIMPills.Revit.Application
{
    public class RevitApplication : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication app)
        {
            try
            {
                // 0. Assembly resolver — en .NET 8 el CLR no sondea el directorio del add-in
                //    automáticamente; sin esto las DLLs de BIMPills no se encuentran al instanciar
                //    ventanas WPF la primera vez.
                RegisterAssemblyResolver();

                // 1. Logger
                var logDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Autodesk", "Revit", "Addins", "BIMPills", "Logs");
                ServiceLocator.Register<ILogger>(new FileLogger(logDir));

                var logger = ServiceLocator.Get<ILogger>();
                logger.Info("═══════════════════════════════════════════════════");
                logger.Info("BIMPills — Iniciando plugin");
                logger.Info($"Directorio de logs: {logDir}");
                logger.Info($"Ensamblado: {Assembly.GetExecutingAssembly().Location}");

                // 2. Version adapter (compiled per-version via #if REVIT20XX)
                ServiceLocator.Register<IRevitVersionAdapter>(new RevitVersionAdapterImpl());
                var versionLabel = new RevitVersionAdapterImpl().VersionLabel;
                logger.Info($"Versión de Revit: {versionLabel}");

                // 3. Sprint 2 services
                ServiceLocator.Register<IDimensionSchemeService>(new DimensionSchemeService(new JsonSchemeRepository()));
                ServiceLocator.Register<IMCPDiscoveryService>(new MCPDiscoveryService(new JsonMCPConnectionRepository()));
                logger.Info("Servicios Sprint 2 registrados: IDimensionSchemeService, IMCPDiscoveryService");

                // 4. License service — validates against Airtable, caches locally (DPAPI)
                RegisterLicenseService(logger);

                // 5. Global exception handlers — prevent any BIMPills exception from crashing Revit
                RegisterGlobalExceptionHandlers(logger);

                // 6. Ribbon builder
                var ribbon = new RibbonBuilder(app);

                // 7. Load modules — add new modules here as the plugin grows
                foreach (var module in GetModules())
                {
                    logger.Info($"Cargando módulo: {module.GetType().Name} (Tab: {module.TabName}, Panel: {module.PanelName})");
                    ribbon.EnsureTab(module.TabName);
                    ribbon.EnsurePanel(module.TabName, module.PanelName);
                    module.BuildRibbon(ribbon);
                }

                logger.Info($"BIMPills iniciado correctamente ({versionLabel})");
                logger.Info("═══════════════════════════════════════════════════");

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                // Intentar escribir al log incluso si hay error
                if (ServiceLocator.IsRegistered<ILogger>())
                    ServiceLocator.Get<ILogger>().Error("Error fatal al iniciar BIMPills", ex);

                TaskDialog.Show("BIMPills — Error de inicio", ex.ToString());
                return Result.Failed;
            }
        }

        /// <summary>
        /// Registers global unhandled-exception handlers so that no BIMPills exception
        /// can propagate unhandled and crash Revit.
        /// </summary>
        private static void RegisterGlobalExceptionHandlers(ILogger logger)
        {
            // Catch all unhandled WPF UI-thread exceptions — prevents Revit crash
            System.Windows.Threading.Dispatcher.CurrentDispatcher.UnhandledException += (s, e) =>
            {
                try
                {
                    logger?.Error("Excepción no controlada en hilo UI de BIMPills", e.Exception);

                    System.Windows.MessageBox.Show(
                        $"Ocurrió un error inesperado en BIMPills:\n\n{e.Exception?.Message}\n\nEl error ha sido registrado en el log.",
                        "BIMPills — Error inesperado",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                }
                finally
                {
                    // CRITICAL: prevents crash propagation to Revit
                    e.Handled = true;
                }
            };

            // Catch unhandled non-UI thread exceptions (best-effort logging; CLR may still terminate)
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                try
                {
                    logger?.Error("Excepción fatal en AppDomain de BIMPills", e.ExceptionObject as Exception);
                }
                catch
                {
                    // Swallow — we cannot let the handler itself throw
                }
            };
        }

        /// <summary>
        /// Registers the license service and triggers an async background validation.
        /// Commands check the cached result synchronously via ILicenseService.IsValid.
        /// </summary>
        private static void RegisterLicenseService(ILogger logger)
        {
            var cache = new LicenseCache();
            var service = new AirtableLicenseService(cache);
            ServiceLocator.Register<ILicenseService>(service);

            var cached = cache.Load();
            if (cached != null && cache.IsCacheFresh())
            {
                logger.Info($"Licencia en cache válida — Plan: {cached.Plan}, Estado: {cached.Status}");
            }
            else if (cached != null)
            {
                logger.Info("Cache de licencia expirado — revalidando en background...");
                Task.Run(async () =>
                {
                    try
                    {
                        await service.ValidateAsync(cached.LicenseKey);
                        logger.Info("Licencia revalidada contra Airtable.");
                    }
                    catch (Exception ex)
                    {
                        logger.Warning($"No se pudo revalidar licencia: {ex.Message}");
                    }
                });
            }
            else
            {
                logger.Info("Sin licencia en cache — se requerirá activación al primer uso.");
            }
        }

        public Result OnShutdown(UIControlledApplication app)
        {
            if (ServiceLocator.IsRegistered<ILogger>())
                ServiceLocator.Get<ILogger>().Info("BIMPills cerrado.");
            return Result.Succeeded;
        }

        // ── Assembly resolver (solo Revit 2026 / .NET 8) ───────────────────────
        // En .NET 8, Revit no agrega el directorio del add-in al probing path.
        // Sin esto, cualquier DLL de BIMPills (BIMPills.UI, etc.) lanza
        // FileNotFoundException al instanciarse por primera vez.
        private static void RegisterAssemblyResolver()
        {
#if !REVIT2024          // Solo necesario en .NET 8 (2025/2026); .NET Framework lo resuelve solo
            var addinDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";

            AppDomain.CurrentDomain.AssemblyResolve += (_, args) =>
            {
                try
                {
                    var name = new AssemblyName(args.Name).Name;
                    if (string.IsNullOrEmpty(name)) return null;

                    var path = Path.Combine(addinDir, name + ".dll");
                    return File.Exists(path) ? Assembly.LoadFrom(path) : null;
                }
                catch
                {
                    return null;
                }
            };
#endif
        }

        // ── Registro de módulos ─────────────────────────────────────────────────
        // Para agregar una nueva funcionalidad, añade una línea aquí.
        private static IEnumerable<IPluginModule> GetModules()
        {
            // Panel: Datos
            yield return new ModelAuditModule();
            yield return new ExportFamiliesModule();
            // CustomDimensionSchemesModule — se accede desde Documentar → Acotar
            // ExportAudit — se accede desde la ventana de Auditoría (sin módulo propio)
            yield return new MCPIntegrationModule();
            yield return new OrderingModule();
            yield return new DataManagerModule();
            // Panel: Procesos
            yield return new DocumentacionModule();
            yield return new GestionModule();
            // Panel: Información
            yield return new AboutModule();
        }
    }
}
