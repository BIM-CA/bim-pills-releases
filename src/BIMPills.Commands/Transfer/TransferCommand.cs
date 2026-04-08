using BIMPills.Core.Commands;

namespace BIMPills.Commands.Transfer
{
    public class TransferCommand : IPluginCommand
    {
        public CommandResult Execute(ICommandContext context)
        {
            return CommandResult.Ok("Transferir");
        }
    }
}
