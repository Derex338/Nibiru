using Content.Shared._Nibiru.Factions;
using Robust.Shared.Prototypes;
using Content.Server.Popups;
using Content.Shared.Construction.Prototypes;
using System.Linq;
using Content.Server.Administration.Logs;
using Content.Server.Chat.Managers;
using Content.Shared.Database;
using Content.Shared.Mobs;
using Robust.Shared.Random;
using Content.Shared.Mobs.Components;
using Content.Server.Mind;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Content.Shared.GameTicking;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;


namespace Content.Server._Nibiru.Factions;

public sealed class FactionSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MindSystem _mindSystem = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly FactionBroadcaster _broadcast = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;

    private static readonly HashSet<Entity<FactionComponent>> ClientLookup = new();

    /// <summary>
    /// Кэш доступных фракций для отправки клиентам
    /// Обновляется периодически
    /// </summary>
    public IReadOnlyList<FactionInfo> AvailableFactions { get; private set; } = new List<FactionInfo>();

    private TimeSpan _nextUpdate = TimeSpan.Zero;
    private const float UpdateInterval = 2f; // Обновляем каждые 2 секунды

    /// <summary>
    /// Временное хранилище предпочтений для лидеров фракций, присланных из лобби
    /// </summary>
    private readonly Dictionary<NetUserId, NibiruFactionLeaderPrefsMessage> _pendingFactionLeaderPrefs = new();

    public IReadOnlyDictionary<NetUserId, NibiruFactionLeaderPrefsMessage> PendingFactionLeaderPrefs => _pendingFactionLeaderPrefs;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<FactionCreateRequestMessage>(OnFactionCreateRequest);
        SubscribeNetworkEvent<FactionStateRequestMessage>(OnFactionStateRequest);
        SubscribeNetworkEvent<NibiruFactionLeaderPrefsMessage>(OnFactionLeaderPrefs);

        SubscribeNetworkEvent<HeirChooseMessage>(OnHeirChoose);
        SubscribeNetworkEvent<FactionTitleTransferMessage>(OnTitleTransfer);
        SubscribeNetworkEvent<FactionLeaveMessage>(OnLeaveFaction);
        SubscribeNetworkEvent<FactionDeleteMessage>(OnDeleteFaction);
        SubscribeNetworkEvent<FactionKickMemberMessage>(OnKickMemberFaction);

        SubscribeNetworkEvent<FactionChangeStateMessage>(OnFactionStateChange);
        SubscribeNetworkEvent<FactionChangeMemberRankMessage>(OnChangeMemberRank);

        SubscribeLocalEvent<FactionComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<FactionComponent, ComponentStartup>(OnFactionStartup);
        SubscribeLocalEvent<FactionComponent, ComponentShutdown>(OnFactionShutdown);

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeLocalEvent<MapCreatedEvent>(OnMapCreated);
    }

    private void OnMapCreated(MapCreatedEvent ev)
    {
        // Когда создается новая карта, регистрируем на ней все существующие фракции
        var query = EntityQueryEnumerator<FactionComponent>();
        while (query.MoveNext(out var factionUid, out var faction))
        {
            if (faction.IsCreator)
                RegisterFaction(faction);
        }
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _pendingFactionLeaderPrefs.Clear();
    }

    /// <summary>
    /// Обновляет список доступных фракций для клиентов
    /// </summary>
    public List<FactionInfo> UpdateAvailableFactionsList()
    {
        var factions = new List<FactionInfo>();

        // Проходим по всем картам
        var query = EntityQueryEnumerator<FactionRegistryComponent, MapComponent>();
        while (query.MoveNext(out var mapUid, out var registry, out _))
        {
            foreach (var (factionName, data) in registry.Factions)
            {
                // Конвертируем NetEntity обратно в EntityUid
                var leaderUid = GetEntity(data.Leader);

                // Считаем живых участников (включая лидера)
                var aliveCount = 0;
                if (_entityManager.EntityExists(leaderUid) &&
                    TryComp<MobStateComponent>(leaderUid, out var leaderMs) &&
                    leaderMs.CurrentState == MobState.Alive)
                {
                    aliveCount++;
                }

                var deadNetMembers = new List<NetEntity>();
                foreach (var netMember in data.Members)
                {
                    var memberUid = GetEntity(netMember);
                    if (!_entityManager.EntityExists(memberUid))
                    {
                        deadNetMembers.Add(netMember);
                        continue;
                    }
                    if (TryComp<MobStateComponent>(memberUid, out var ms) && ms.CurrentState == MobState.Alive)
                        aliveCount++;
                }

                // Удаляем несуществующих членов
                foreach (var dead in deadNetMembers)
                    data.Members.Remove(dead);

                if (aliveCount == 0 || !data.IsRecruiting)
                    continue;

                // Не добавляем дубликаты
                if (factions.Any(f => f.FactionName == factionName))
                    continue;

                factions.Add(new FactionInfo
                {
                    FactionName = factionName,
                    MemberCount = aliveCount,
                    Color = data.Color,
                    Description = data.Description,
                    IconPath = data.IconPath,
                    Status = data.Status,
                    IsRecruiting = data.IsRecruiting,
                    Leader = data.Leader
                });
            }
        }

        AvailableFactions = factions;
        _broadcast.BroadcastFactionsList();

        return factions;
    }

    /// <summary>
    /// Регистрирует фракцию во всех реестрах карт
    /// </summary>
    public void RegisterFaction(FactionComponent faction)
    {
        if (string.IsNullOrWhiteSpace(faction.FactionName))
            return;

        var leaderNet = GetNetEntity(faction.Leader != default ? faction.Leader : faction.Owner);
        var membersNet = new List<NetEntity>();

        foreach (var member in faction.Members)
        {
            var netMember = GetNetEntity(member);
            if (netMember != leaderNet)
                membersNet.Add(netMember);
        }

        var mapQuery = EntityQueryEnumerator<MapComponent>();
        while (mapQuery.MoveNext(out var mapUid, out _))
        {
            var registry = EnsureComp<FactionRegistryComponent>(mapUid);
            registry.Factions[faction.FactionName] = new FactionRegistryData
            {
                Name = faction.FactionName,
                Leader = leaderNet,
                Members = membersNet,
                Color = faction.FactionColor,
                Description = faction.Description,
                IconPath = faction.IconPath,
                Status = faction.Status,
                IsRecruiting = faction.IsRecruiting,
                Created = _timing.CurTime
            };

            Dirty(mapUid, registry);
        }

        Dirty(faction.Owner, faction);

        // Сразу обновляем список
        UpdateAvailableFactionsList();
    }

    /// <summary>
    /// Регистрирует фракцию из лобби в реестре
    /// </summary>
    public void RegisterLobbyPref(NibiruFactionLeaderPrefsMessage pref)
    {
        if (string.IsNullOrWhiteSpace(pref.FactionName))
            return;

        var query = EntityQueryEnumerator<MapComponent>();
        while (query.MoveNext(out var mapUid, out _))
        {
            var registry = EnsureComp<FactionRegistryComponent>(mapUid);
            
            // Если фракция уже есть (например, от победителя лотереи), не перезаписываем
            if (registry.Factions.ContainsKey(pref.FactionName))
                continue;

            registry.Factions[pref.FactionName] = new FactionRegistryData
            {
                Name = pref.FactionName,
                Leader = NetEntity.Invalid,
                Members = new List<NetEntity>(),
                Color = pref.Color,
                Description = pref.Description,
                IconPath = pref.IconPath,
                Status = FactionStatus.Active,
                IsRecruiting = pref.IsRecruiting,
            };
            Dirty(mapUid, registry);
        }
    }

    /// <summary>
    /// Обновляет данные фракции во всех реестрах
    /// </summary>
    private void UpdateFactionRegistry(FactionComponent faction)
    {
        var leaderNet = GetNetEntity(faction.Leader != default ? faction.Leader : faction.Owner);
        var membersNet = new List<NetEntity>();
        foreach (var member in faction.Members)
        {
            var netMember = GetNetEntity(member);
            if (netMember != leaderNet)
                membersNet.Add(netMember);
        }

        var query = EntityQueryEnumerator<FactionRegistryComponent>();
        while (query.MoveNext(out var mapEntity, out var registry))
        {
            if (!registry.Factions.ContainsKey(faction.FactionName))
                continue;

            var data = registry.Factions[faction.FactionName];
            data.Color = faction.FactionColor;
            data.Description = faction.Description;
            data.IconPath = faction.IconPath;
            data.Status = faction.Status;
            data.IsRecruiting = faction.IsRecruiting;
            data.Leader = leaderNet;
            data.Members = membersNet;

            registry.Factions[faction.FactionName] = data;
            Dirty(mapEntity, registry);
        }

        // Обновляем список
        UpdateAvailableFactionsList();
    }

    /// <summary>
    /// Удаляет фракцию из всех реестров
    /// </summary>
    private void UnregisterFaction(string factionName)
    {
        var query = EntityQueryEnumerator<FactionRegistryComponent>();
        while (query.MoveNext(out var mapEntity, out var registry))
        {
            if (registry.Factions.Remove(factionName))
            {
                Dirty(mapEntity, registry);
            }
        }

        // Обновляем список
        UpdateAvailableFactionsList();
    }

    private void OnFactionStartup(EntityUid uid, FactionComponent component, ComponentStartup args)
    {
        if (!component.IsCreator)
            return;

        var xform = Transform(uid);
        if (xform.MapID == MapId.Nullspace)
            return;

        RegisterFaction(component);
    }

    private void OnFactionShutdown(EntityUid uid, FactionComponent component, ComponentShutdown args)
    {
        if (!component.IsCreator)
            return;

        var xform = Transform(uid);
        if (xform.MapID == MapId.Nullspace)
            return;

        UnregisterFaction(component.FactionName);
    }

    private void OnFactionCreateRequest(FactionCreateRequestMessage msg, EntitySessionEventArgs args)
    {
        var name = msg.FactionName?.Trim();
        if (string.IsNullOrWhiteSpace(name))
            return;

        var player = args.SenderSession.AttachedEntity;
        if (!player.HasValue)
            return;

        if (!ValidateFactionName(name, args.SenderSession.UserId, null, out var error))
        {
            _chatManager.DispatchServerMessage(args.SenderSession, error);
            return;
        }

        CreateFaction(player.Value, name);
    }

    private void CreateFaction(EntityUid player, string factionName)
    {
        if (!EntityManager.TryGetComponent<FactionComponent>(player, out var factionComponent))
        {
            factionComponent = EntityManager.AddComponent<FactionComponent>(player);

            factionComponent.FactionName = factionName;
            factionComponent.IsCreator = true;
            factionComponent.Rank = "Лидер";

            _adminLog.Add(LogType.FactionCreated, LogImpact.Medium,
                $"{ToPrettyString(player):player} создал фракцию с названием {factionName}");

            Dirty(player, factionComponent);

            RegisterFaction(factionComponent);
        }
    }

    private void OnFactionStateRequest(FactionStateRequestMessage msg, EntitySessionEventArgs args)
    {
        var player = args.SenderSession.AttachedEntity;

        if (!player.HasValue)
            return;

        if (EntityManager.TryGetComponent<FactionComponent>(player, out var factionComponent))
        {
            if (factionComponent.IsCreator == true)
                msg.Creator = true;

            msg.FactionName = factionComponent.FactionName;
        }
    }

    private void OnFactionLeaderPrefs(NibiruFactionLeaderPrefsMessage msg, EntitySessionEventArgs args)
    {
        var name = msg.FactionName?.Trim();
        if (string.IsNullOrWhiteSpace(name))
            return;

        if (!ValidateFactionName(name, args.SenderSession.UserId, null, out var error))
        {
            _chatManager.DispatchServerMessage(args.SenderSession, error);
            return;
        }

        msg.FactionName = name;
        msg.Description = msg.Description?.Trim() ?? "";
        if (msg.Description.Length > 500)
            msg.Description = msg.Description.Substring(0, 500);

        _pendingFactionLeaderPrefs[args.SenderSession.UserId] = msg;
    }

    /// <summary>
    /// Добавляет заспавненного игрока в выбранную фракцию
    /// </summary>
    public bool TryJoinPlayerToFaction(EntityUid playerEntity, string factionName)
    {
        // Ищем фракцию во всех реестрах
        var query = EntityQueryEnumerator<FactionRegistryComponent, MapComponent>();
        while (query.MoveNext(out var mapUid, out var registry, out _))
        {
            if (!registry.Factions.TryGetValue(factionName, out var factionData))
                continue;

            // Находим живого члена фракции для телепортации
            EntityUid? spawnNear = null;

            // Сначала проверяем лидера
            var leaderUid = GetEntity(factionData.Leader);
            if (_entityManager.EntityExists(leaderUid) &&
                TryComp<MobStateComponent>(leaderUid, out var leaderMob) &&
                leaderMob.CurrentState == MobState.Alive)
            {
                spawnNear = leaderUid;
            }
            else
            {
                // Ищем любого живого члена
                var deadNetMembers = new List<NetEntity>();
                foreach (var netMember in factionData.Members)
                {
                    var memberUid = GetEntity(netMember);
                    if (!_entityManager.EntityExists(memberUid))
                    {
                        deadNetMembers.Add(netMember);
                        continue;
                    }
                    if (TryComp<MobStateComponent>(memberUid, out var memberMob) &&
                        memberMob.CurrentState == MobState.Alive)
                    {
                        spawnNear = memberUid;
                        break;
                    }
                }

                // Удаляем несуществующих членов из данных реестра
                foreach (var dead in deadNetMembers)
                    factionData.Members.Remove(dead);
            }

            if (spawnNear != null)
            {
                // Телепортируем игрока рядом с членом фракции
                var targetXform = Transform(spawnNear.Value);
                var offset = _random.NextVector2(1f, 2f);
                var newCoords = targetXform.Coordinates.Offset(offset);

                _transform.SetCoordinates(playerEntity, newCoords);
            }
            else
            {
                // Если нет никого рядом, значит либо это первая фракция из лобби, либо все мертвы
                // Если в реестре еще нет лидера, то первый зашедший становится лидером
                if (leaderUid == EntityUid.Invalid && factionData.Members.Count == 0)
                {
                    leaderUid = playerEntity;
                }
            }

            // Добавляем в фракцию
            var playerFaction = EnsureComp<FactionComponent>(playerEntity);
            playerFaction.FactionName = factionName;
            playerFaction.Leader = leaderUid;
            playerFaction.FactionColor = factionData.Color;
            playerFaction.Description = factionData.Description;
            playerFaction.IconPath = factionData.IconPath;
            playerFaction.Status = factionData.Status;
            playerFaction.IsRecruiting = factionData.IsRecruiting;

            if (leaderUid == playerEntity)
            {
                playerFaction.Rank = "Лидер";
                playerFaction.IsCreator = true;
            }
            else
            {
                playerFaction.Rank = "Новобранец";
                playerFaction.IsCreator = false;
            }

            if (TryComp<FactionComponent>(leaderUid, out var leaderComp))
            {
                if (leaderUid != playerEntity && !leaderComp.Members.Contains(playerEntity))
                    leaderComp.Members.Add(playerEntity);
                
                Dirty(leaderUid, leaderComp);
                UpdateFactionRegistry(leaderComp);
            }

            Dirty(playerEntity, playerFaction);

            _popup.PopupEntity(
                Loc.GetString("faction-join-success", ("faction", factionName)),
                playerEntity,
                playerEntity);

            return true;
        }

        return false;
    }

    private void OnMobStateChanged(EntityUid uid, FactionComponent component, MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead || args.OldMobState >= args.NewMobState || !component.IsCreator)
            return;

        var xform = Transform(uid);
        var mapUid = _transform.GetMap(uid);

        if (TryComp<FactionComponent>(component.Heir, out var heir)
        && heir.FactionName == component.FactionName
        && component.Heir.Valid
        && TryComp<MobStateComponent>(component.Heir, out var mobStateComponent)
        && mobStateComponent.CurrentState == MobState.Alive)
        {
            heir.IsCreator = true;
            heir.Members = component.Members;
            heir.Members.Remove(component.Heir);
            heir.Rank = "Лідер";
            component.IsCreator = false;

            foreach (var member in heir.Members)
            {
                if (TryComp<FactionComponent>(member, out var memberComp))
                {
                    memberComp.Leader = component.Heir;
                    Dirty(member, memberComp);
                }
            }

            Dirty(component.Heir, heir);

            UpdateFactionRegistry(heir);
        }
        else if (component.Members.Count > 0)
        {
            var randomMember = _random.Pick(component.Members);

            if (TryComp<FactionComponent>(randomMember, out var memberComp))
            {
                memberComp.IsCreator = true;
                memberComp.Members = component.Members;
                memberComp.Members.Remove(randomMember);
                memberComp.Rank = "Лідер";
                component.IsCreator = false;

                foreach (var member in memberComp.Members)
                {
                    if (TryComp<FactionComponent>(member, out var memComp))
                    {
                        memComp.Leader = randomMember;
                        Dirty(member, memComp);
                    }
                }

                Dirty(randomMember, memberComp);

                UpdateFactionRegistry(memberComp);
            }
        }
    }

    private void OnHeirChoose(HeirChooseMessage msg, EntitySessionEventArgs args)
    {
        var player = args.SenderSession.AttachedEntity;
        var heir = GetEntity(msg.Heir);

        if (!player.HasValue || heir == player.Value)
            return;

        if (!TryComp<FactionComponent>(player.Value, out var factionComponent)
            || !factionComponent.IsCreator
            || !TryComp<FactionComponent>(heir, out var heirComponent)
            || heirComponent.FactionName != factionComponent.FactionName)
        {
            _popup.PopupEntity(
                Loc.GetString("not-in-youre-faction"),
                player.Value,
                player.Value);
            return;
        }

        factionComponent.Heir = heir;
        Dirty(player.Value, factionComponent);

        UpdateFactionRegistry(factionComponent);
    }

    private void OnTitleTransfer(FactionTitleTransferMessage msg, EntitySessionEventArgs args)
    {
        var player = args.SenderSession.AttachedEntity;
        var entity = GetEntity(msg.entity);

        if (!player.HasValue || entity == player.Value)
            return;

        if (!TryComp<FactionComponent>(player.Value, out var factionComponent)
            || !factionComponent.IsCreator
            || !TryComp<FactionComponent>(entity, out var entityComponent)
            || entityComponent.FactionName != factionComponent.FactionName)
        {
            _popup.PopupEntity(
                Loc.GetString("not-in-youre-faction"),
                player.Value,
                player.Value);
            return;
        }

        factionComponent.IsCreator = false;
        factionComponent.Leader = entity;
        var tempRank = factionComponent.Rank;
        factionComponent.Rank = entityComponent.Rank;

        entityComponent.Members = factionComponent.Members;
        entityComponent.Members.Remove(entity);
        entityComponent.Members.Add(player.Value);
        entityComponent.IsCreator = true;
        entityComponent.Rank = "Лідер";

        foreach (var member in factionComponent.Members)
        {
            if (TryComp<FactionComponent>(member, out var memberComp))
            {
                memberComp.Leader = entity;
                Dirty(member, memberComp);
            }
        }

        Dirty(player.Value, factionComponent);
        Dirty(entity, entityComponent);

        UpdateFactionRegistry(entityComponent);
    }

    private void OnLeaveFaction(FactionLeaveMessage msg, EntitySessionEventArgs args)
    {
        var player = args.SenderSession.AttachedEntity;

        if (!player.HasValue)
            return;

        if (!TryComp<FactionComponent>(player.Value, out var factionComponent)
            || factionComponent.IsCreator)
        {
            _popup.PopupEntity(
                Loc.GetString("no-faction-to-leave"),
                player.Value,
                player.Value);
            return;
        }

        if (!TryComp<FactionComponent>(factionComponent.Leader, out var leaderComponent))
            return;

        leaderComponent.Members.Remove(player.Value);
        Dirty(factionComponent.Leader, leaderComponent);

        UpdateFactionRegistry(leaderComponent);

        RemComp<FactionComponent>(player.Value);
    }

    private void OnKickMemberFaction(FactionKickMemberMessage msg, EntitySessionEventArgs args)
    {
        var player = args.SenderSession.AttachedEntity;
        var member = GetEntity(msg.Member);

        if (!player.HasValue)
            return;

        if (!TryComp<FactionComponent>(player.Value, out var factionComponent)
            || !TryComp<FactionComponent>(member, out var memberComponent)
            || memberComponent.FactionName != factionComponent.FactionName
            || !factionComponent.IsCreator)
        {
            _popup.PopupEntity(
                Loc.GetString("cant-kick-member"),
                player.Value,
                player.Value);
            return;
        }

        factionComponent.Members.Remove(member);
        Dirty(player.Value, factionComponent);

        UpdateFactionRegistry(factionComponent);

        _popup.PopupEntity(
            Loc.GetString("faction-kicked", ("factionName", factionComponent.FactionName)),
            member,
            member);

        RemComp<FactionComponent>(member);
    }

    private void OnDeleteFaction(FactionDeleteMessage msg, EntitySessionEventArgs args)
    {
        var player = args.SenderSession.AttachedEntity;

        if (!player.HasValue)
            return;

        if (!TryComp<FactionComponent>(player.Value, out var factionComponent)
            || !factionComponent.IsCreator)
        {
            _popup.PopupEntity(
                Loc.GetString("not-leader"),
                player.Value,
                player.Value);
            return;
        }

        foreach (var member in factionComponent.Members)
        {
            if (TryComp<FactionComponent>(member, out var memberComp))
            {
                _popup.PopupEntity(
                    Loc.GetString("faction-disbanded", ("factionName", factionComponent.FactionName)),
                    member,
                    member);

                RemComp<FactionComponent>(member);
            }
        }

        UnregisterFaction(factionComponent.FactionName);

        RemComp<FactionComponent>(player.Value);
    }

    private void OnChangeMemberRank(FactionChangeMemberRankMessage msg, EntitySessionEventArgs args)
    {
        var player = args.SenderSession.AttachedEntity;
        var member = GetEntity(msg.Member);

        if (!player.HasValue)
            return;

        if (!TryComp<FactionComponent>(player.Value, out var factionComponent)
            || !TryComp<FactionComponent>(member, out var memberComponent)
            || memberComponent.FactionName != factionComponent.FactionName
            || !factionComponent.IsCreator)
        {
            _popup.PopupEntity(
                Loc.GetString("cant-change-rank"),
                player.Value,
                player.Value);
            return;
        }

        memberComponent.Rank = msg.NewRank;
        Dirty(member, memberComponent);

        _popup.PopupEntity(
            Loc.GetString("rank-changed", ("rank", msg.NewRank)),
            member,
            member);
    }

    private void OnFactionStateChange(FactionChangeStateMessage msg, EntitySessionEventArgs args)
    {
        var player = args.SenderSession.AttachedEntity;

        if (!player.HasValue)
            return;

        if (!TryComp<FactionComponent>(player.Value, out var factionComponent)
            || !factionComponent.IsCreator)
        {
            _popup.PopupEntity(
                Loc.GetString("not-leader"),
                player.Value,
                player.Value);
            return;
        }

        bool needUpdate = false;

        if (msg.FactionName != null)
        {
            var name = msg.FactionName.Trim();
            if (name != factionComponent.FactionName)
            {
                if (!ValidateFactionName(name, args.SenderSession.UserId, factionComponent.FactionName, out var error))
                {
                    _chatManager.DispatchServerMessage(args.SenderSession, error);
                    return;
                }

                var query = EntityQueryEnumerator<FactionRegistryComponent, MapComponent>();
                while (query.MoveNext(out var mapEntity, out var reg, out _))
                {
                    if (reg.Factions.Remove(factionComponent.FactionName, out var oldData))
                    {
                        oldData.Name = name;
                        reg.Factions[name] = oldData;
                        Dirty(mapEntity, reg);
                    }
                }

                factionComponent.FactionName = name;

                foreach (var member in factionComponent.Members)
                {
                    if (TryComp<FactionComponent>(member, out var memberComp))
                    {
                        memberComp.FactionName = name;
                        Dirty(member, memberComp);

                        _popup.PopupEntity(
                            Loc.GetString("faction-name-changed", ("factionName", name)),
                            member,
                            member);
                    }
                }

                needUpdate = true;
            }
        }

        if (msg.Description != null)
        {
            var desc = msg.Description.Trim();
            if (desc.Length > 500)
                desc = desc.Substring(0, 500);

            if (desc != factionComponent.Description)
            {
                factionComponent.Description = desc;
                foreach (var member in factionComponent.Members)
                {
                    if (TryComp<FactionComponent>(member, out var memberComp))
                    {
                        memberComp.Description = desc;
                        Dirty(member, memberComp);
                    }
                }
                needUpdate = true;
            }
        }

        if (msg.IconPath != null && msg.IconPath != factionComponent.IconPath)
        {
            factionComponent.IconPath = msg.IconPath;
            foreach (var member in factionComponent.Members)
            {
                if (TryComp<FactionComponent>(member, out var memberComp))
                {
                    memberComp.IconPath = msg.IconPath;
                    Dirty(member, memberComp);
                }
            }
            needUpdate = true;
        }

        if (msg.Color != null && msg.Color != factionComponent.FactionColor)
        {
            factionComponent.FactionColor = msg.Color.Value;
            foreach (var member in factionComponent.Members)
            {
                if (TryComp<FactionComponent>(member, out var memberComp))
                {
                    memberComp.FactionColor = msg.Color.Value;
                    Dirty(member, memberComp);
                }
            }
            needUpdate = true;
        }

        if (msg.Status != null && msg.Status != factionComponent.Status)
        {
            factionComponent.Status = msg.Status.Value;
            foreach (var member in factionComponent.Members)
            {
                if (TryComp<FactionComponent>(member, out var memberComp))
                {
                    memberComp.Status = msg.Status.Value;
                    Dirty(member, memberComp);
                }
            }
            needUpdate = true;
        }

        if (msg.IsRecruiting != null && msg.IsRecruiting != factionComponent.IsRecruiting)
        {
            factionComponent.IsRecruiting = msg.IsRecruiting.Value;
            foreach (var member in factionComponent.Members)
            {
                if (TryComp<FactionComponent>(member, out var memberComp))
                {
                    memberComp.IsRecruiting = msg.IsRecruiting.Value;
                    Dirty(member, memberComp);
                }
            }
            needUpdate = true;
        }

        if (needUpdate)
        {
            Dirty(player.Value, factionComponent);
            UpdateFactionRegistry(factionComponent);
        }
    }

    /// <summary>
    /// Валидация названия фракции
    /// </summary>
    private bool ValidateFactionName(string name, NetUserId excludeUserId, string? currentFactionName, out string error)
    {
        error = "";
        name = name.Trim();

        if (name.Length < 3)
        {
            error = Loc.GetString("faction-name-too-short");
            return false;
        }

        if (name.Length > 32)
        {
            error = Loc.GetString("faction-name-too-long");
            return false;
        }

        // Проверка в лобби
        foreach (var (userId, pref) in _pendingFactionLeaderPrefs)
        {
            if (userId == excludeUserId)
                continue;

            if (pref.FactionName.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                error = Loc.GetString("faction-already-exist", ("factionName", name));
                return false;
            }
        }

        // Проверка в реестре
        var query = EntityQueryEnumerator<FactionRegistryComponent>();
        while (query.MoveNext(out var registry))
        {
            foreach (var (fName, _) in registry.Factions)
            {
                // Если мы переименовываем текущую фракцию, то игнорируем совпадение с её старым названием
                if (currentFactionName != null && fName == currentFactionName)
                    continue;

                if (fName.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    error = Loc.GetString("faction-already-exist", ("factionName", name));
                    return false;
                }
            }
        }

        return true;
    }
}
