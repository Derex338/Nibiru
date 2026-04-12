using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._Nibiru.SaveLoad.Commands;

[AdminCommand(AdminFlags.Server)]
public sealed class SaveRoundCommand : IConsoleCommand
{
    public string Command => "saveround";
    public string Description => "Saves the current round, including maps and players.";
    public string Help => "Usage: saveround <savename>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteLine($"Invalid number of arguments. {Help}");
            return;
        }

        var saveSys = IoCManager.Resolve<IEntityManager>().System<NibiruRoundSaveSystem>();
        saveSys.SaveRound(args[0]);
        shell.WriteLine($"Saved round to {args[0]}");
    }
}

[AdminCommand(AdminFlags.Server)]
public sealed class LoadRoundCommand : IConsoleCommand
{
    public string Command => "loadround";
    public string Description => "Sets the next round to load from the specified save, and restarts the round.";
    public string Help => "Usage: loadround <savename>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteLine($"Invalid number of arguments. {Help}");
            return;
        }

        var saveSys = IoCManager.Resolve<IEntityManager>().System<NibiruRoundSaveSystem>();
        saveSys.RequestLoad(args[0]);
        shell.WriteLine($"Requested round restart with save {args[0]}.");
    }
}
