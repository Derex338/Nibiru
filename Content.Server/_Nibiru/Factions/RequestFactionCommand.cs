using Content.Server._Nibiru.GameTicking.Rules;
using Content.Server.Administration;
using Content.Server.GameTicking;
using Content.Server.Station.Components;
using Content.Shared._Nibiru.Factions;
using Content.Shared.Administration;
using Content.Shared.Singularity.Components;
using Content.Shared.Station;
using Content.Shared.Station.Components;
using Robust.Shared.Console;
using Robust.Shared.Player;

namespace Content.Server._Nibiru.Factions.Commands;

/// <summary>
/// Команда для запроса списка фракций
/// </summary>
[AdminCommand(AdminFlags.Admin)]
public sealed class RequestFactionsCommand : IConsoleCommand
{
    public string Command => "requestfactions";
    public string Description => "Request list of available factions";
    public string Help => "requestfactions";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player == null)
        {
            shell.WriteError("This command can only be used by players");
            return;
        }

        var entityManager = IoCManager.Resolve<IEntityManager>();
        var faction = entityManager.System<FactionSystem>();

        var list = faction.UpdateAvailableFactionsList();
        string str = string.Empty;
        foreach (var factionName in list)
        {
            str = str + "\n" + factionName.FactionName + "\n Leader:" + factionName.Leader + "\n Members count:" + factionName.MemberCount;
        }
        shell.WriteLine($"List of factions: {str}");

    }
}

/// <summary>
/// Команда для присоединения к фракции через поздний вход
/// </summary>
[AnyCommand]
public sealed class LateJoinFactionCommand : IConsoleCommand
{
    public string Command => "latejoin_faction";
    public string Description => "Join a faction during late join";
    public string Help => "latejoin_faction <factionName>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player == null)
        {
            shell.WriteError("This command can only be used by players");
            return;
        }

        var entityManager = IoCManager.Resolve<IEntityManager>();
        var rule = entityManager.System<NibiruSurvivalRuleSystem>();

        rule.OnLateJoinFactionChoice(shell.Player, args.Length > 0 ? args[0] : null);
    }
}

/// <summary>
/// Команда для одиночного спавна
/// </summary>
[AnyCommand]
public sealed class LateJoinSoloCommand : IConsoleCommand
{
    public string Command => "latejoin_solo";
    public string Description => "Spawn without joining any faction";
    public string Help => "latejoin_solo";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player == null)
        {
            shell.WriteError("This command can only be used by players");
            return;
        }

        var player = shell.Player;

        if (player == null)
        {
            return;
        }

        var entityManager = IoCManager.Resolve<IEntityManager>();
        var ticker = entityManager.System<GameTicker>();
        var query = entityManager.EntityQueryEnumerator<StationDataComponent>();

        while (query.MoveNext(out var uid, out var comp))
        {
            ticker.MakeJoinGame(player, uid);
            break;
        }

        // Отправляем сообщение с пустым factionName
        //var msg = new LateJoinFactionMessage { FactionName = null };
        //entityManager.RaisePredictiveEvent(msg);

        shell.WriteLine("Spawning solo...");
    }
}
