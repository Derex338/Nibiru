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
    // Список победителей лотереи, ожидающих спавна
    private readonly Dictionary<NetUserId, NibiruFactionLeaderPrefsMessage> _pendingLeaders = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RulePlayerSpawningEvent>(OnPlayerSpawning);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawned);
    }

    private void OnPlayerSpawning(RulePlayerSpawningEvent ev)
    {
        _pendingLeaders.Clear();
        var pendingPrefs = _factionSystem.PendingFactionLeaderPrefs;
        
        if (pendingPrefs.Count == 0)
            return;

        // Регистрируем ВСЕ фракции из лобби в реестре
        foreach (var pref in pendingPrefs.Values)
        {
            _factionSystem.RegisterLobbyPref(pref);
        }

        // Все, у кого заполнено название и кто в пуле спавна, становятся лидерами
        var poolIds = ev.PlayerPool.Select(p => p.UserId).ToHashSet();
        foreach (var (userId, pref) in pendingPrefs)
        {
            if (string.IsNullOrWhiteSpace(pref.FactionName) || !poolIds.Contains(userId))
                continue;

            _pendingLeaders[userId] = pref;
        }
    }

    private void OnPlayerSpawned(PlayerSpawnCompleteEvent ev)
    {
        var userId = ev.Player.UserId;
        if (!_pendingLeaders.TryGetValue(userId, out var prefs))
        {
            return;
        }

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
    }
}
