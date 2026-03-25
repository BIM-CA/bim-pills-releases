using BIMPills.Commands.ModelAudit;
using BIMPills.Core.Audit;
using BIMPills.Core.Commands;
using BIMPills.Core.Services;
using System.Collections.Generic;
using Xunit;

namespace BIMPills.Core.Tests.ModelAudit
{
    public class ModelAuditCommandTests
    {
        [Fact]
        public void Execute_WithWarnings_ReturnsSuccessAndPopulatesLastResult()
        {
            var context = new FakeCommandContext(new FakeDocumentServices
            {
                Warnings = new List<ModelWarningInfo>
                {
                    new ModelWarningInfo("Elementos superpuestos", "Error", 4),
                    new ModelWarningInfo("Advertencia de habitabilidad", "Warning", 1)
                }
            });

            var command = new ModelAuditCommand();
            var result  = command.Execute(context);

            Assert.True(result.Success);
            Assert.NotNull(ModelAuditCommand.LastResult);
            Assert.Equal(2, ModelAuditCommand.LastResult!.Warnings.Count);
        }

        [Fact]
        public void Execute_EmptyModel_ReturnsSuccessWithZeroCounts()
        {
            var context = new FakeCommandContext(new FakeDocumentServices());
            var result  = new ModelAuditCommand().Execute(context);

            Assert.True(result.Success);
            Assert.Equal(0, ModelAuditCommand.LastResult!.Warnings.Count);
            Assert.Equal(0, ModelAuditCommand.LastResult!.UnplacedViews.Count);
        }
    }

    // ── Test doubles ────────────────────────────────────────────────────────────

    internal sealed class FakeCommandContext : ICommandContext
    {
        public IDocumentServices Document { get; }
        public ILogger Logger { get; } = new NullLogger();

        public FakeCommandContext(IDocumentServices doc) { Document = doc; }
    }

    internal sealed class FakeDocumentServices : IDocumentServices
    {
        public string Title { get; set; } = "TestModel.rvt";
        public bool IsWorkshared { get; set; } = false;

        public IReadOnlyList<ModelWarningInfo> Warnings    { get; set; } = new List<ModelWarningInfo>();
        public IReadOnlyList<FamilyInfo>       FamilyList  { get; set; } = new List<FamilyInfo>();
        public IReadOnlyList<ViewInfo>         Views       { get; set; } = new List<ViewInfo>();
        public IReadOnlyList<ElementInfo>      Orphans     { get; set; } = new List<ElementInfo>();

        public IReadOnlyList<ModelWarningInfo> GetWarnings()             => Warnings;
        public IReadOnlyList<FamilyInfo>       GetFamilySizes()          => FamilyList;
        public IReadOnlyList<ViewInfo>         GetUnplacedViews()        => Views;
        public IReadOnlyList<ElementInfo>      GetElementsWithoutCategory() => Orphans;
    }

    internal sealed class NullLogger : ILogger
    {
        public void Info(string message)    { }
        public void Warning(string message) { }
        public void Error(string message, System.Exception? exception = null) { }
    }
}
