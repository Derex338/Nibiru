using Content.Server.Administration;
using Content.Server._Nibiru.SaveLoad;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.IoC;

namespace Content.Server._Nibiru.Factions.Commands;

[AnyCommand]
public sealed class LateJoinLoadCommand : IConsoleCommand
{
    public string Command => "latejoin_load";
    public string Description => "Load saved character";
    public string Help => "latejoin_load";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player == null)
        {
            shell.WriteError("This command can only be used by players");
            return;
        }

        var entityManager = IoCManager.Resolve<IEntityManager>();
        var sys = entityManager.System<NibiruRoundSaveSystem>();

        string? targetCharacter = args.Length > 0 ? string.Join(" ", args) : null;
        sys.TryLoadSavedPlayer(shell.Player, targetCharacter);
        shell.WriteLine($"Attempting to load saved character{(targetCharacter != null ? " " + targetCharacter : "")}...");
    }
}
