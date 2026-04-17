using Autodesk.Revit.UI;
using BIMPills.Commands.About;
using BIMPills.Commands.CustomDimensionSchemes;
using BIMPills.Commands.DataManager;
using BIMPills.Commands.Transfer;
using BIMPills.Commands.Documentacion;
using BIMPills.Commands.ExportFamilies;
using BIMPills.Commands.Gestion;
using BIMPills.Commands.MCPIntegration;
using BIMPills.Commands.ModelAudit;
using BIMPills.Commands.Ordering;
using BIMPills.Core.About;
using BIMPills.Core.Licensing;
using BIMPills.Core.Modules;
using BIMPills.Core.Services;
using BIMPills.Core.Updates;
using BIMPills.Infrastructure.DI;
using BIMPills.Infrastructure.Licensing;
using BIMPills.Infrastructure.Logging;
using BIMPills.Infrastructure.Persistence;
using BIMPills.Infrastructure.Services;
using BIMPills.Infrastructure.Updates;
using BIMPills.Revit.Compatibility;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

                // 2. Theme — detect Revit dark mode and initialize ThemeHelper
                try
                {
#if REVIT2024 || REVIT2025 || REVIT2026 || REVIT2027
                    // Dark theme disabled until fully polished — always use light
                    // bool isDark = Autodesk.Revit.UI.UIThemeManager.CurrentTheme == Autodesk.Revit.UI.UITheme.Dark;
                    BIMPills.UI.Shared.ThemeHelper.Initialize(false);
                    logger.Info("Tema forzado: Claro (tema oscuro deshabilitado temporalmente)");
#endif
                }
                catch { /* Theme detection is non-critical */ }

                // 3. Version adapter (compiled per-version via #if REVIT20XX)
                ServiceLocator.Register<IRevitVersionAdapter>(new RevitVersionAdapterImpl());
                var versionLabel = new RevitVersionAdapterImpl().VersionLabel;
                logger.Info($"Versión de Revit: {versionLabel}");

                // 3. Sprint 2 services
                ServiceLocator.Register<IDimensionSchemeService>(new DimensionSchemeService(new JsonSchemeRepository()));
                ServiceLocator.Register<IMCPDiscoveryService>(new MCPDiscoveryService(new JsonMCPConnectionRepository()));
                logger.Info("Servicios Sprint 2 registrados: IDimensionSchemeService, IMCPDiscoveryService");

                // 4. License service — validates against Airtable, caches locally (DPAPI)
                RegisterLicenseService(logger, app);

                // 4b. Update checker — comprueba GitHub Releases una vez cada 24 h
                RegisterUpdateChecker(logger, app);

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

        // ── Update checker state ────────────────────────────────────────────────
        private static readonly GitHubUpdateChecker _updateChecker = new GitHubUpdateChecker();
        private static DateTime _lastUpdateCheck = DateTime.MinValue;
        private static readonly TimeSpan _updateCheckCooldown = TimeSpan.FromHours(24);
        private static string? _pendingInstallerPath = null;

        // Last time we revalidated against Airtable — used to throttle DocumentOpened calls
        private static DateTime _lastRevalidation = DateTime.MinValue;
        private static readonly TimeSpan _revalidationCooldown = TimeSpan.FromMinutes(30);

        /// <summary>
        /// Registers the license service, triggers an async background validation at startup,
        /// and subscribes to DocumentOpened for periodic revalidation against Airtable.
        /// </summary>
        private static void RegisterLicenseService(ILogger logger, UIControlledApplication app)
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
                RevalidateInBackground(service, cache, logger, "startup");
            }
            else
            {
                logger.Info("Sin licencia en cache — se requerirá activación al primer uso.");
            }

            // Revalidate against Airtable each time a document is opened (max once per 30 min)
            app.ControlledApplication.DocumentOpened += (s, e) =>
            {
                if (DateTime.UtcNow - _lastRevalidation < _revalidationCooldown) return;
                var current = cache.Load();
                if (string.IsNullOrEmpty(current?.LicenseKey)) return;
                RevalidateInBackground(service, cache, logger, "DocumentOpened");
            };
        }

        private static void RevalidateInBackground(
            AirtableLicenseService service, LicenseCache cache, ILogger logger, string trigger)
        {
            _lastRevalidation = DateTime.UtcNow;
            Task.Run(async () =>
            {
                try
                {
                    var key = cache.Load()?.LicenseKey;
                    if (string.IsNullOrEmpty(key)) return;
                    await service.ValidateAsync(key, forceRefresh: true);
                    logger.Info($"Licencia revalidada contra Airtable ({trigger}).");
                }
                catch (Exception ex)
                {
                    logger.Warning($"No se pudo revalidar licencia ({trigger}): {ex.Message}");
                }
            });
        }

        public Result OnShutdown(UIControlledApplication app)
        {
            if (ServiceLocator.IsRegistered<ILogger>())
                ServiceLocator.Get<ILogger>().Info("BIMPills cerrado.");

            // Si hay un instalador pendiente descargado, lanzarlo ahora que Revit se cierra.
            LaunchPendingInstallerIfAny();

            return Result.Succeeded;
        }

        private static void LaunchPendingInstallerIfAny()
        {
            try
            {
                if (string.IsNullOrEmpty(_pendingInstallerPath)) return;
                if (!File.Exists(_pendingInstallerPath)) return;
                Process.Start(new ProcessStartInfo
                {
                    FileName        = _pendingInstallerPath,
                    UseShellExecute = true,
                });
            }
            catch { /* No bloquear el cierre de Revit si falla el lanzamiento */ }
        }

        /// <summary>
        /// Registra el chequeo de actualizaciones en el evento DocumentOpened
        /// (máximo una vez cada 24 horas) y suscribe la lógica de notificación.
        /// </summary>
        private static void RegisterUpdateChecker(ILogger logger, UIControlledApplication app)
        {
            app.ControlledApplication.DocumentOpened += (s, e) =>
            {
                if (DateTime.UtcNow - _lastUpdateCheck < _updateCheckCooldown) return;
                _lastUpdateCheck = DateTime.UtcNow;

                var currentVersion = new AboutInfo().Version; // "beta 1.0"

                Task.Run(async () =>
                {
                    try
                    {
                        var update = await _updateChecker.CheckAsync(currentVersion);
                        if (update == null) return;

                        logger.Info($"Actualización disponible: {update.DisplayVersion}");

                        // Volver al hilo UI de WPF para mostrar el diálogo
                        System.Windows.Application.Current?.Dispatcher.BeginInvoke(
                            System.Windows.Threading.DispatcherPriority.Background,
                            new Action(() => ShowUpdateDialog(update, logger)));
                    }
                    catch (Exception ex)
                    {
                        logger.Warning($"Error al verificar actualizaciones: {ex.Message}");
                    }
                });
            };
        }

        private static void ShowUpdateDialog(UpdateInfo update, ILogger logger)
        {
            try
            {
                var notes = string.IsNullOrWhiteSpace(update.ReleaseNotes)
                    ? string.Empty
                    : $"\n\n{update.ReleaseNotes}";

                var td = new TaskDialog("BIM Pills — Actualización disponible")
                {
                    MainInstruction = $"Nueva versión disponible: {update.DisplayVersion}",
                    MainContent     = $"Tienes instalada la versión actual. Hay una nueva versión " +
                                      $"de BIM Pills lista para descargar.{notes}",
                    CommonButtons   = TaskDialogCommonButtons.Close,
                    DefaultButton   = TaskDialogResult.Close,
                };

                if (!string.IsNullOrEmpty(update.InstallerDownloadUrl))
                {
                    td.AddCommandLink(TaskDialogCommandLinkId.CommandLink1,
                        "Descargar e instalar al cerrar Revit",
                        "El instalador se descargará ahora. Se ejecutará automáticamente cuando cierres Revit.");
                }

                var result = td.Show();

                if (result == TaskDialogResult.CommandLink1 &&
                    !string.IsNullOrEmpty(update.InstallerDownloadUrl))
                {
                    DownloadUpdateInBackground(update, logger);
                }
            }
            catch (Exception ex)
            {
                logger.Warning($"Error al mostrar diálogo de actualización: {ex.Message}");
            }
        }

        private static void DownloadUpdateInBackground(UpdateInfo update, ILogger logger)
        {
            Task.Run(async () =>
            {
                try
                {
                    logger.Info($"Descargando actualización {update.DisplayVersion}...");
                    var path = await _updateChecker.DownloadInstallerAsync(update);
                    if (!string.IsNullOrEmpty(path))
                    {
                        _pendingInstallerPath = path;
                        logger.Info($"Instalador descargado: {path}");

                        System.Windows.Application.Current?.Dispatcher.BeginInvoke(
                            System.Windows.Threading.DispatcherPriority.Background,
                            new Action(() =>
                            {
                                new TaskDialog("BIM Pills — Descarga completada")
                                {
                                    MainInstruction = "Descarga completada",
                                    MainContent     = "El instalador está listo. " +
                                                      "Se ejecutará automáticamente al cerrar Revit.",
                                    CommonButtons   = TaskDialogCommonButtons.Ok,
                                }.Show();
                            }));
                    }
                }
                catch (Exception ex)
                {
                    logger.Warning($"Error al descargar actualización: {ex.Message}");
                }
            });
        }

        // ── Assembly resolver (solo Revit 2026 / .NET 8) ───────────────────────
        // En .NET 8, Revit no agrega el directorio del add-in al probing path.
        // Sin esto, cualquier DLL de BIMPills (BIMPills.UI, etc.) lanza
        // FileNotFoundException al instanciarse por primera vez.
        private static void RegisterAssemblyResolver()
        {
            // Necesario en todas las versiones: .NET Framework no sondea el directorio del
            // add-in automáticamente para DLLs NuGet que no están en el GAC
            // (p.ej. System.Security.Cryptography.ProtectedData en Revit 2024).
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
            yield return new TransferModule();
            // Panel: Procesos
            yield return new DocumentacionModule();
            yield return new GestionModule();
            // Panel: Información
            yield return new AboutModule();
        }
    }
}
