using Autodesk.Revit.UI;
using BIMPills.Core.Commands;
using BIMPills.Core.Services;
using BIMPills.Infrastructure.DI;
using System;

namespace BIMPills.Revit.Context
{
    internal sealed class RevitCommandContext : ICommandContext
    {
        public IDocumentServices Document { get; }
        public ILogger Logger { get; }

        public RevitCommandContext(ExternalCommandData commandData, Action<int, int, string>? onProgress = null)
        {
            Document = new RevitDocumentServices(
                commandData.Application.ActiveUIDocument.Document,
                onProgress);
            Logger   = ServiceLocator.Get<ILogger>();
        }
    }
}
