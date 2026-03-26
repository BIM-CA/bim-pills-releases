using Autodesk.Revit.UI;
using BIMPills.Commands.About;
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

                // 2. Version adapter (compiled per-version via #if REVIT20XX)
                ServiceLocator.Register<IRevitVersionAdapter>(new RevitVersionAdapterImpl());

                // 3. Ribbon builder
                var ribbon = new RibbonBuilder(app);

                // 4. Load modules — add new modules here as the plugin grows
                foreach (var module in GetModules())
                {
                    ribbon.EnsureTab(module.TabName);
                    ribbon.EnsurePanel(module.TabName, module.PanelName);
                    module.BuildRibbon(ribbon);
                }

                ServiceLocator.Get<ILogger>().Info(
                    $"BIM Pills iniciado ({new RevitVersionAdapterImpl().VersionLabel})");

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("BIM Pills — Error de inicio", ex.ToString());
                return Result.Failed;
            }
        }

        public Result OnShutdown(UIControlledApplication app)
        {
            if (ServiceLocator.IsRegistered<ILogger>())
                ServiceLocator.Get<ILogger>().Info("BIM Pills cerrado.");
            return Result.Succeeded;
        }

        // ── Registro de módulos ─────────────────────────────────────────────────
        // Para agregar una nueva funcionalidad, añade una línea aquí.
        private static IEnumerable<IPluginModule> GetModules()
        {
            yield return new ModelAuditModule();
            yield return new AboutModule();
            // yield return new ExportSchedulesModule();
            // yield return new MaintenanceModule();
        }
    }
}
