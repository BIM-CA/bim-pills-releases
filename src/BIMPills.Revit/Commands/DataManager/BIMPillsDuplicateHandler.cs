using Autodesk.Revit.DB;
using BIMPills.Core.Models;

namespace BIMPills.Revit.Commands.DataManager
{
    /// <summary>
    /// Handles duplicate type names when copying view templates between documents.
    /// Skip mode keeps the destination's existing template.
    /// Replace mode is handled by pre-deleting conflicts before the copy operation.
    /// </summary>
    internal sealed class BIMPillsDuplicateHandler : IDuplicateTypeNamesHandler
    {
        private readonly ConflictResolution _resolution;

        public BIMPillsDuplicateHandler(ConflictResolution resolution)
        {
            _resolution = resolution;
        }

        public DuplicateTypeAction OnDuplicateTypeNamesFound(DuplicateTypeNamesHandlerArgs args)
        {
            // For Skip: keep the destination type (don't import the source version).
            // For Replace: conflicts are pre-deleted before copy — this handler is a safety fallback.
            // For Rename: not natively supported by CopyElements; treated as Skip here,
            //             with the caller responsible for any post-copy rename logic.
            return DuplicateTypeAction.UseDestinationTypes;
        }
    }
}
