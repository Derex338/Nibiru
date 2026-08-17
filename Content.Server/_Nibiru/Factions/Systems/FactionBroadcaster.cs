using Content.Server.GameTicking;
using Content.Shared._Nibiru.Factions;
using Content.Shared.CCVar;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using System.Linq;

namespace Content.Server._Nibiru.Factions;

/// <summary>
/// Send update faction list to all players
/// </summary>
public sealed partial class FactionBroadcaster : EntitySystem
{
    [Dependency] private FactionSystem _factionSystem = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private IConfigurationManager _configurationManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerJoinedLobbyEvent>(OnLobbyJoin);
        Subs.CVar(_configurationManager, CCVars.GameDisallowLateJoins, _ => BroadcastFactionsList(), true);
    }

    /// <summary>
    /// Send actual faction list to all players
    /// </summary>
    public void BroadcastFactionsList()
    {
        if (_gameTicker.DisallowLateJoin)
            return;

        var factions = _factionSystem.AvailableFactions;

        var msg = new FactionsAvailableMessage
        {
            Factions = factions.ToList()
        };

        foreach (var session in _playerManager.Sessions)
        {
            RaiseNetworkEvent(msg, session);
        }
    }

    /// <summary>
    /// Force update faction list
    /// </summary>
    public void ForceUpdate()
    {
        BroadcastFactionsList();
    }

    private void OnLobbyJoin(PlayerJoinedLobbyEvent args)
    {
        BroadcastFactionsList();
    }
}
