using Content.Server.GameTicking;
using Content.Shared._Nibiru.Factions;
using Robust.Shared.Random;
using Robust.Shared.Network;
using System.Linq;
using Content.Shared.Preferences;
using Content.Shared.GameTicking;
using Content.Server.GameTicking.Events;
using Content.Server.Chat.Managers;

namespace Content.Server._Nibiru.Factions;

public sealed class NibiruFactionLeaderSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly FactionSystem _factionSystem = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly ILogManager _logManager = default!;

    private ISawmill _sawmill = default!;

    // Список победителей лотереи, ожидающих спавна
    private readonly Dictionary<NetUserId, NibiruFactionLeaderPrefsMessage> _pendingLeaders = new();

    public override void Initialize()
    {
        base.Initialize();
        _sawmill = _logManager.GetSawmill("nibiru.factions");
        SubscribeLocalEvent<RulePlayerSpawningEvent>(OnPlayerSpawning);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawned);
    }

    private void OnPlayerSpawning(RulePlayerSpawningEvent ev)
    {
        _pendingLeaders.Clear();
        var pendingPrefs = _factionSystem.PendingFactionLeaderPrefs;
        
        _sawmill.Info($"Starting faction leader lottery. Total pending prefs: {pendingPrefs.Count}");

        if (pendingPrefs.Count == 0)
            return;

        // Определяем количество фракций
        int factionCount = 2;
        int totalPlayers = ev.PlayerPool.Count;
        if (totalPlayers >= 10)
        {
            factionCount = 2 + (totalPlayers / 10);
        }

        // Регистрируем ВСЕ фракции из лобби в реестре (даже те, кто не станет лидером сразу)
        foreach (var pref in pendingPrefs.Values)
        {
            _factionSystem.RegisterLobbyPref(pref);
        }

        // Выбираем кандидатов (только те, у кого заполнено название и кто в пуле спавна)
        var poolIds = ev.PlayerPool.Select(p => p.UserId).ToHashSet();
        var candidates = pendingPrefs
            .Where(p => !string.IsNullOrWhiteSpace(p.Value.FactionName) && poolIds.Contains(p.Key))
            .Select(p => p.Key)
            .ToList();
            
        _sawmill.Info($"Candidates in pool: {candidates.Count}. Target faction count: {factionCount}");

        if (candidates.Count == 0)
            return;

        _random.Shuffle(candidates);

        var winners = candidates.Take(Math.Min(factionCount, candidates.Count)).ToList();
        var winnersSet = winners.ToHashSet();

        foreach (var userId in candidates)
        {
            if (winnersSet.Contains(userId))
            {
                _pendingLeaders[userId] = pendingPrefs[userId];
                _sawmill.Info($"Player {userId} won the lottery for faction: {pendingPrefs[userId].FactionName}");
            }
            else
            {
                // Проиграл в лотерее - оставляем в лобби
                var playerSession = ev.PlayerPool.FirstOrDefault(p => p.UserId == userId);
                if (playerSession != null)
                {
                    _chatManager.DispatchServerMessage(playerSession, Loc.GetString("nibiru-faction-lottery-lost"));
                    ev.PlayerPool.Remove(playerSession);
                }
                
                _sawmill.Info($"Player {userId} lost the lottery and stays in lobby.");
            }
        }
    }

    private void OnPlayerSpawned(PlayerSpawnCompleteEvent ev)
    {
        var userId = ev.Player.UserId;
        if (!_pendingLeaders.TryGetValue(userId, out var prefs))
        {
            return;
        }

        _sawmill.Info($"Winner {userId} spawned. Applying faction: {prefs.FactionName}");

        // Игрок заспавнился и он победитель лотереи
        var mob = ev.Mob;
        
        // Добавляем компонент фракции
        var factionComp = EnsureComp<FactionComponent>(mob);
        factionComp.FactionName = prefs.FactionName;
        factionComp.Description = prefs.Description;
        factionComp.FactionColor = prefs.Color;
        factionComp.IconPath = prefs.IconPath;
        factionComp.IsRecruiting = prefs.IsRecruiting;
        factionComp.IsCreator = true;
        factionComp.Rank = "Лидер";
        factionComp.Leader = mob;
        
        // Добавляем Dirty для синхронизации компонента с клиентом
        Dirty(mob, factionComp);

        // Регистрируем фракцию в реестре
        _factionSystem.RegisterFaction(factionComp);
        
        _pendingLeaders.Remove(userId);
        _sawmill.Info($"Faction {prefs.FactionName} registered for leader {mob}");
    }
}
