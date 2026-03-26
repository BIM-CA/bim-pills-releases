using BIMPills.Commands.About;
using BIMPills.Core.Commands;
using BIMPills.Core.Services;
using System;
using Xunit;

namespace BIMPills.Core.Tests.About
{
    public class AboutCommandTests
    {
        [Fact]
        public void Execute_ReturnsSuccess()
        {
            var context = new FakeAboutContext();
            var command = new AboutCommand();
            var result  = command.Execute(context);

            Assert.True(result.Success);
        }

        [Fact]
        public void Execute_PopulatesLastResult()
        {
            var context = new FakeAboutContext();
            var command = new AboutCommand();
            command.Execute(context);

            Assert.NotNull(AboutCommand.LastResult);
            Assert.Equal("BIM Pills", AboutCommand.LastResult!.PluginName);
            Assert.Equal("1.0.0", AboutCommand.LastResult.Version);
            Assert.Equal("Rodrigo Flores", AboutCommand.LastResult.Developer);
            Assert.Equal("BIM-CA", AboutCommand.LastResult.Company);
            Assert.Equal("https://bim-ca.com", AboutCommand.LastResult.Website);
        }

        [Fact]
        public void Execute_MessageContainsPluginNameAndVersion()
        {
            var context = new FakeAboutContext();
            var command = new AboutCommand();
            var result  = command.Execute(context);

            Assert.Contains("BIM Pills", result.Message);
            Assert.Contains("1.0.0", result.Message);
            Assert.Contains("BIM-CA", result.Message);
        }
    }

    // ── Test doubles ────────────────────────────────────────────────────────────

    internal sealed class FakeAboutContext : ICommandContext
    {
        public IDocumentServices Document { get; } = new FakeAboutDocumentServices();
        public ILogger Logger { get; } = new NullAboutLogger();
    }

    internal sealed class FakeAboutDocumentServices : IDocumentServices
    {
        public string Title => "TestModel.rvt";
        public bool IsWorkshared => false;

        public System.Collections.Generic.IReadOnlyList<Audit.ModelWarningInfo> GetWarnings()
            => new System.Collections.Generic.List<Audit.ModelWarningInfo>();
        public System.Collections.Generic.IReadOnlyList<Audit.FamilyInfo> GetFamilySizes()
            => new System.Collections.Generic.List<Audit.FamilyInfo>();
        public System.Collections.Generic.IReadOnlyList<Audit.ViewInfo> GetUnplacedViews()
            => new System.Collections.Generic.List<Audit.ViewInfo>();
        public System.Collections.Generic.IReadOnlyList<Audit.ElementInfo> GetElementsWithoutCategory()
            => new System.Collections.Generic.List<Audit.ElementInfo>();
    }

    internal sealed class NullAboutLogger : ILogger
    {
        public void Info(string message)    { }
        public void Warning(string message) { }
        public void Error(string message, Exception? exception = null) { }
    }
}
