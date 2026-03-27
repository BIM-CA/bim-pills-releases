using Autodesk.Revit.UI;
using BIMPills.Commands.About;
using BIMPills.Commands.Documentacion;
using BIMPills.Commands.ExportFamilies;
using BIMPills.Commands.Gestion;
using BIMPills.Commands.ModelAudit;
using BIMPills.Core.Modules;
using BIMPills.Core.Services;
using BIMPills.Infrastructure.DI;
using BIMPills.Infrastructure.Logging;
using BIMPills.Revit.Compatibility;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace BIMPills.Revit.Application
{
    public class RevitApplication : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication app)
        {
            try
            {
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

                // 3. Ribbon builder
                var ribbon = new RibbonBuilder(app);

                // 4. Load modules — add new modules here as the plugin grows
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

        public Result OnShutdown(UIControlledApplication app)
        {
            if (ServiceLocator.IsRegistered<ILogger>())
                ServiceLocator.Get<ILogger>().Info("BIMPills cerrado.");
            return Result.Succeeded;
        }

        // ── Registro de módulos ─────────────────────────────────────────────────
        // Para agregar una nueva funcionalidad, añade una línea aquí.
        private static IEnumerable<IPluginModule> GetModules()
        {
            // Panel: Datos
            yield return new ModelAuditModule();
            yield return new ExportFamiliesModule();
            // Panel: Procesos
            yield return new DocumentacionModule();
            yield return new GestionModule();
            // Panel: Información
            yield return new AboutModule();
        }
    }
}
