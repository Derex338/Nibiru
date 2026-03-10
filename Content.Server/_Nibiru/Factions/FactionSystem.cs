using Content.Shared._Nibiru.Factions;
using Robust.Shared.Prototypes;
using Content.Server.Popups;
using Content.Shared.Construction.Prototypes;
using System.Linq;
using Content.Server.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Mobs;
using Robust.Shared.Random;
using Content.Shared.Mobs.Components;
using Content.Server.Mind;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

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
    [Dependency] private readonly IEntityManager _entityManager = default!;

    private static readonly HashSet<Entity<FactionComponent>> ClientLookup = new();

    /// <summary>
    /// Кэш доступных фракций для отправки клиентам
    /// Обновляется периодически
    /// </summary>
    public IReadOnlyList<FactionInfo> AvailableFactions { get; private set; } = new List<FactionInfo>();

    private TimeSpan _nextUpdate = TimeSpan.Zero;
    private const float UpdateInterval = 2f; // Обновляем каждые 2 секунды

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<FactionCreateRequestMessage>(OnFactionCreateRequest);
        SubscribeNetworkEvent<FactionStateRequestMessage>(OnFactionStateRequest);

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

                // Проверяем, что лидер существует и жив
                if (!_entityManager.EntityExists(leaderUid) ||
                    !TryComp<MobStateComponent>(leaderUid, out var mobState) ||
                    mobState.CurrentState != MobState.Alive)
                    continue;

                // Считаем живых членов
                var aliveMembers = 0;
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
                        aliveMembers++;
                }

                // Удаляем несуществующих членов
                foreach (var dead in deadNetMembers)
                    data.Members.Remove(dead);

                if (aliveMembers == 0 || !data.IsRecruiting)
                    continue;

                // Не добавляем дубликаты
                if (factions.Any(f => f.FactionName == factionName))
                    continue;

                factions.Add(new FactionInfo
                {
                    FactionName = factionName,
                    MemberCount = aliveMembers,
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
    private void RegisterFaction(FactionComponent faction)
    {
        var leaderNet = GetNetEntity(faction.Leader != default ? faction.Leader : faction.Owner);
        var membersNet = new List<NetEntity> { leaderNet };

        foreach (var member in faction.Members)
        {
            membersNet.Add(GetNetEntity(member));
        }

        var query = EntityQueryEnumerator<MapComponent>();
        while (query.MoveNext(out var mapUid, out _))
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
            };

            Dirty(mapUid, registry);
        }

        // Сразу обновляем список
        UpdateAvailableFactionsList();
    }

    /// <summary>
    /// Обновляет данные фракции во всех реестрах
    /// </summary>
    private void UpdateFactionRegistry(FactionComponent faction)
    {
        var leaderNet = GetNetEntity(faction.Leader != default ? faction.Leader : faction.Owner);
        var membersNet = new List<NetEntity> { leaderNet };
        foreach (var member in faction.Members)
        {
            membersNet.Add(GetNetEntity(member));
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
        if (string.IsNullOrWhiteSpace(msg.FactionName))
            return;

        var player = args.SenderSession.AttachedEntity;
        if (!player.HasValue)
            return;

        var xform = Transform(player.Value);
        var mapUid = _transform.GetMap(player.Value);

        if (mapUid == null)
            return;

        // Проверяем существование фракции в любом из реестров
        bool exists = false;
        var query = EntityQueryEnumerator<FactionRegistryComponent>();
        while (query.MoveNext(out var registry))
        {
            if (registry.Factions.ContainsKey(msg.FactionName))
            {
                exists = true;
                break;
            }
        }

        if (exists)
        {
            _popup.PopupEntity(
                Loc.GetString("faction-already-exist", ("factionName", msg.FactionName)),
                player.Value,
                player.Value);
            return;
        }

        CreateFaction(player.Value, msg.FactionName);
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

            if (spawnNear == null)
            {
                _popup.PopupEntity(
                    Loc.GetString("faction-join-no-spawn", ("faction", factionName)),
                    playerEntity,
                    playerEntity);
                return false;
            }

            // Телепортируем игрока рядом с членом фракции
            var targetXform = Transform(spawnNear.Value);
            var offset = _random.NextVector2(1f, 2f);
            var newCoords = targetXform.Coordinates.Offset(offset);

            _transform.SetCoordinates(playerEntity, newCoords);

            // Добавляем в фракцию
            var playerFaction = EnsureComp<FactionComponent>(playerEntity);
            playerFaction.FactionName = factionName;
            playerFaction.Leader = leaderUid;
            playerFaction.FactionColor = factionData.Color;
            playerFaction.Description = factionData.Description;
            playerFaction.IconPath = factionData.IconPath;
            playerFaction.Status = factionData.Status;
            playerFaction.IsRecruiting = factionData.IsRecruiting;
            playerFaction.Rank = "Новобранец";

            if (TryComp<FactionComponent>(leaderUid, out var leaderComp))
            {
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

        bool factionNameAvaliable = true;

        if (msg.FactionName != null)
        {
            // Проверяем через все реестры
            var query = EntityQueryEnumerator<FactionRegistryComponent>();
            while (query.MoveNext(out var registry))
            {
                if (registry.Factions.ContainsKey(msg.FactionName))
                {
                    _popup.PopupEntity(
                        Loc.GetString("faction-already-exist", ("factionName", msg.FactionName)),
                        player.Value,
                        player.Value);
                    factionNameAvaliable = false;
                    break;
                }
            }
        }

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

        if (msg.FactionName != null && factionNameAvaliable && msg.FactionName != factionComponent.FactionName)
        {
            var query = EntityQueryEnumerator<FactionRegistryComponent, MapComponent>();
            while (query.MoveNext(out var mapEntity, out var reg, out _))
            {
                if (reg.Factions.Remove(factionComponent.FactionName, out var oldData))
                {
                    oldData.Name = msg.FactionName;
                    reg.Factions[msg.FactionName] = oldData;
                    Dirty(mapEntity, reg);
                }
            }

            factionComponent.FactionName = msg.FactionName;

            foreach (var member in factionComponent.Members)
            {
                if (TryComp<FactionComponent>(member, out var memberComp))
                {
                    memberComp.FactionName = msg.FactionName;
                    Dirty(member, memberComp);

                    _popup.PopupEntity(
                        Loc.GetString("faction-name-changed", ("factionName", msg.FactionName)),
                        member,
                        member);
                }
            }

            needUpdate = true;
        }

        if (msg.Description != null && msg.Description != factionComponent.Description)
        {
            factionComponent.Description = msg.Description;
            foreach (var member in factionComponent.Members)
            {
                if (TryComp<FactionComponent>(member, out var memberComp))
                {
                    memberComp.Description = msg.Description;
                    Dirty(member, memberComp);
                }
            }
            needUpdate = true;
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
}
