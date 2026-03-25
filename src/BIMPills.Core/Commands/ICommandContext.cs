using BIMPills.Core.Services;

namespace BIMPills.Core.Commands
{
    /// <summary>
    /// Provides commands with access to the active document and services
    /// without exposing Revit API types directly.
    /// </summary>
    public interface ICommandContext
    {
        IDocumentServices Document { get; }
        ILogger Logger { get; }
    }
}
