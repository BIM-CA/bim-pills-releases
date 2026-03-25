namespace BIMPills.Core.Commands
{
    /// <summary>
    /// Contract for every BIM Pills command.
    /// Implementations live in BIMPills.Commands and have zero dependency on RevitAPI.dll.
    /// </summary>
    public interface IPluginCommand
    {
        CommandResult Execute(ICommandContext context);
    }
}
