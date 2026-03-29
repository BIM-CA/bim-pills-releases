using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPills.Core.Models;
using System;

namespace BIMPills.Revit.Commands.Ordering
{
    /// <summary>
    /// IExternalEventHandler that reverts the last assigned value in the session history.
    /// </summary>
    public class OrderingUndoHandler : IExternalEventHandler
    {
        private readonly OrderingSessionState _session;

        /// <summary>Called after a successful undo.</summary>
        public Action<int>? OnUndone { get; set; }

        public OrderingUndoHandler(OrderingSessionState session)
        {
            _session = session;
        }

        public void Execute(UIApplication app)
        {
            if (_session.History.Count == 0) return;

            var doc   = app.ActiveUIDocument.Document;
            var entry = _session.History[_session.History.Count - 1];

            try
            {
                var element = doc.GetElement(new ElementId((int)entry.ElementId));
                var param   = element?.LookupParameter(_session.Config.ParameterName);

                if (param != null && !param.IsReadOnly)
                {
                    using var tx = new Transaction(doc, "BIMPills: Ordenar — Deshacer");
                    tx.Start();
                    param.Set(entry.PreviousValue);
                    tx.Commit();
                }

                _session.History.RemoveAt(_session.History.Count - 1);
                _session.CurrentValue -= _session.Config.Step;
                OnUndone?.Invoke(_session.CurrentValue);
            }
            catch { /* Ignore — element may have been deleted */ }
        }

        public string GetName() => "BIMPills: OrderingUndoHandler";
    }
}
