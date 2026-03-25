using Autodesk.Revit.UI;
using BIMPills.Core.Commands;
using BIMPills.Core.Services;
using BIMPills.Infrastructure.DI;

namespace BIMPills.Revit.Context
{
    internal sealed class RevitCommandContext : ICommandContext
    {
        public IDocumentServices Document { get; }
        public ILogger Logger { get; }

        public RevitCommandContext(ExternalCommandData commandData)
        {
            Document = new RevitDocumentServices(commandData.Application.ActiveUIDocument.Document);
            Logger   = ServiceLocator.Get<ILogger>();
        }
    }
}
