using BIMPills.Core.Commands;
using BIMPills.Core.Gestion;
using System.Collections.Generic;

namespace BIMPills.Commands.Gestion
{
    public sealed class GestionCommand : IPluginCommand
    {
        public CommandResult Execute(ICommandContext context)
        {
            var doc = context.Document;
            context.Logger.Info($"Recopilando subproyectos de: {doc.Title}");

            if (!doc.IsWorkshared)
            {
                LastResult = new GestionResult
                {
                    DocumentTitle = doc.Title,
                    IsWorkshared = false,
                    Worksets = new List<WorksetInfo>()
                };
                return CommandResult.Ok("Modelo sin worksharing habilitado.");
            }

            var worksets = doc.GetWorksets();
            context.Logger.Info($"Encontrados {worksets.Count} subproyectos.");

            LastResult = new GestionResult
            {
                DocumentTitle = doc.Title,
                IsWorkshared = true,
                Worksets = worksets
            };

            return CommandResult.Ok($"{worksets.Count} subproyectos encontrados.");
        }

        public static GestionResult? LastResult { get; private set; }
    }

    public sealed class GestionResult
    {
        public string DocumentTitle { get; set; } = string.Empty;
        public bool IsWorkshared { get; set; }
        public IReadOnlyList<WorksetInfo> Worksets { get; set; } = new List<WorksetInfo>();
    }
}
