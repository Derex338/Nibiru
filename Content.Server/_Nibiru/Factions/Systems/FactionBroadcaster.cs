using Content.Server.GameTicking;
using Content.Shared._Nibiru.Factions;
using Content.Shared.CCVar;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using System.Linq;

namespace Content.Server._Nibiru.Factions;

/// <summary>
/// Система для отправки обновлений списка фракций всем клиентам
/// Работает аналогично обновлению списка работ в оригинальном GameTicker
/// </summary>
public sealed partial class FactionBroadcaster : EntitySystem
{
    [Dependency] private FactionSystem _factionSystem = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private IConfigurationManager _configurationManager = default!;

    private TimeSpan _nextBroadcast = TimeSpan.Zero;
    private const float BroadcastInterval = 2f; // Отправляем обновления каждые 2 секунды

    //public override void Update(float frameTime)
    //{
    //    base.Update(frameTime);

    //    //if (Timing.CurTime < _nextBroadcast)
    //    //    return;

    //    //_nextBroadcast = Timing.CurTime + TimeSpan.FromSeconds(BroadcastInterval);

    //    // Отправляем обновление всем подключённым клиентам
    //    //BroadcastFactionsList();
    //}

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerJoinedLobbyEvent>(OnLobbyJoin);
        Subs.CVar(_configurationManager, CCVars.GameDisallowLateJoins, _ => BroadcastFactionsList(), true);
    }

    /// <summary>
    /// Отправляет актуальный список фракций всем клиентам
    /// </summary>
    public void BroadcastFactionsList()
    {
        if (_gameTicker.DisallowLateJoin)
            return;

        var factions = _factionSystem.AvailableFactions;

        //if (factions.Count == 0)
        //    return;

        var msg = new FactionsAvailableMessage
        {
            Factions = factions.ToList()
        };

        // Отправляем всем подключённым игрокам
        foreach (var session in _playerManager.Sessions)
        {
            RaiseNetworkEvent(msg, session);
        }
    }

    /// <summary>
    /// Принудительно отправляет обновление списка фракций
    /// Вызывается при изменении фракций
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
