using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Events;

namespace BIMPills.Revit.Commands.DataManager
{
    /// <summary>
    /// Silently dismisses non-critical Revit warnings during transactions.
    /// Prevents blocking dialogs when transferring project standards (e.g. dimension types,
    /// text styles) that trigger sketch-constraint or family-constraint warnings.
    /// </summary>
    internal sealed class SilentWarningsPreprocessor : IFailuresPreprocessor
    {
        public FailureProcessingResult PreprocessFailures(FailuresAccessor a)
        {
            foreach (var failure in a.GetFailureMessages())
            {
                if (failure.GetSeverity() == FailureSeverity.Warning)
                    a.DeleteWarning(failure);
            }
            return FailureProcessingResult.Continue;
        }
    }
}
