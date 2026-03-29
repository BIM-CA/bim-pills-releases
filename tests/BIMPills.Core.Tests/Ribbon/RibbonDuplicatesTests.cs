using BIMPills.Commands.About;
using BIMPills.Commands.CustomDimensionSchemes;
using BIMPills.Commands.DataManager;
using BIMPills.Commands.Documentacion;
using BIMPills.Commands.ExportFamilies;
using BIMPills.Commands.Gestion;
using BIMPills.Commands.MCPIntegration;
using BIMPills.Commands.ModelAudit;
using BIMPills.Commands.Ordering;
using BIMPills.Core.Modules;
using System.Collections.Generic;
using Xunit;

namespace BIMPills.Core.Tests.Ribbon
{
    public class RibbonDuplicatesTests
    {
        /// <summary>
        /// Validates that no two modules register a button with the same name,
        /// which would throw ArgumentException on Revit startup.
        /// Run this before opening Revit to catch ribbon collisions early.
        /// </summary>
        [Fact]
        public void AllModules_NoDuplicateButtonNames()
        {
            var modules = new IPluginModule[]
            {
                new ModelAuditModule(),
                new ExportFamiliesModule(),
                new DocumentacionModule(),
                new GestionModule(),
                new MCPIntegrationModule(),
                new CustomDimensionSchemesModule(),
                new AboutModule(),
                new OrderingModule(),
                new DataManagerModule(),
            };

            var builder = new RecordingRibbonBuilder();

            foreach (var module in modules)
                module.BuildRibbon(builder);

            var duplicates = builder.FindDuplicates();
            Assert.True(
                duplicates.Count == 0,
                $"Duplicate ribbon button names found: {string.Join(", ", duplicates)}");
        }

        [Fact]
        public void AllModules_ButtonNamesAreNotEmpty()
        {
            var modules = new IPluginModule[]
            {
                new ModelAuditModule(),
                new ExportFamiliesModule(),
                new DocumentacionModule(),
                new GestionModule(),
                new MCPIntegrationModule(),
                new CustomDimensionSchemesModule(),
                new AboutModule(),
                new OrderingModule(),
                new DataManagerModule(),
            };

            var builder = new RecordingRibbonBuilder();

            foreach (var module in modules)
                module.BuildRibbon(builder);

            foreach (var name in builder.RegisteredNames)
                Assert.False(string.IsNullOrWhiteSpace(name),
                    "A module registered a button with an empty name.");
        }
    }

    // ── Test double ──────────────────────────────────────────────────────────

    internal sealed class RecordingRibbonBuilder : IRibbonBuilder
    {
        private readonly List<string> _names = new List<string>();

        public IReadOnlyList<string> RegisteredNames => _names;

        public void EnsureTab(string tabName) { }
        public void EnsurePanel(string tabName, string panelName) { }

        public void AddPushButton(
            string tabName, string panelName, string buttonName,
            string tooltip, string commandTypeFullName,
            string assemblyPath, string? iconKey = null)
        {
            _names.Add(buttonName);
        }

        /// <summary>Returns names that appear more than once.</summary>
        public List<string> FindDuplicates()
        {
            var seen = new HashSet<string>();
            var dupes = new List<string>();
            foreach (var name in _names)
                if (!seen.Add(name) && !dupes.Contains(name))
                    dupes.Add(name);
            return dupes;
        }
    }
}
