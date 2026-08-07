using Content.Shared._Nibiru.Factions;
using Content.Server.Popups;
using Content.Shared.Construction.Prototypes;
using Content.Server.Administration.Logs;
using Content.Server.Chat.Managers;
using Robust.Shared.Random;
using Content.Server.Mind;
using Robust.Shared.Map;
using Robust.Shared.Timing;
using Content.Shared.Humanoid;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Content.Shared.GameTicking;

using Content.Shared.Body;
using Content.Shared.Mobs;

namespace Content.Server._Nibiru.Factions;

public sealed partial class FactionSystem : EntitySystem
{
[Dependency] private IPrototypeManager _prototypeManager = default!;
[Dependency] private EntityLookupSystem _lookup = default!;
[Dependency] private PopupSystem _popup = default!;
[Dependency] private IAdminLogManager _adminLog = default!;
[Dependency] private IRobustRandom _random = default!;
[Dependency] private MindSystem _mindSystem = default!;
[Dependency] private SharedTransformSystem _transform = default!;
[Dependency] private IMapManager _mapManager = default!;
[Dependency] private FactionBroadcaster _broadcast = default!;
[Dependency] private IGameTiming _timing = default!;
[Dependency] private IEntityManager _entityManager = default!;
[Dependency] private IChatManager _chatManager = default!;
[Dependency] private SharedVisualBodySystem _visualBody = default!;

    /// <summary>
    /// Кэш доступных фракций для отправки клиентам
    /// Обновляется периодически
    /// </summary>
    public IReadOnlyList<FactionInfo> AvailableFactions { get; private set; } = new List<FactionInfo>();

    /// <summary>
    /// Временное хранилище предпочтений для лидеров фракций, присланных из лобби
    /// </summary>
    private readonly Dictionary<NetUserId, NibiruFactionLeaderPrefsMessage> _pendingFactionLeaderPrefs = new();

    public IReadOnlyDictionary<NetUserId, NibiruFactionLeaderPrefsMessage> PendingFactionLeaderPrefs => _pendingFactionLeaderPrefs;

    public override void Initialize()
    {
        base.Initialize();

        InitializeNetworking();
        InitializeEvents();
    }

    private void InitializeEvents()
    {
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

    private List<FactionRole> GetDefaultRoles()
    {
        return new List<FactionRole>
        {
            new FactionRole
            {
                Name = Loc.GetString("faction-default-rank-recruit"),
                CanInvite = false,
                CanResearch = false,
                CanManageRoles = false,
                CanInherit = false
            }
        };
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

    /// <summary>
    /// Обновляет кэш данных о членах фракции для UI лидера
    /// </summary>
    public void UpdateMemberDataUI(EntityUid leaderUid, FactionComponent leaderComp)
    {
        leaderComp.MemberData.Clear();
        foreach (var memberUid in leaderComp.Members)
        {
            var data = new FactionMemberData()
            {
                Entity = GetNetEntity(memberUid),
                Name = Name(memberUid),
                Rank = Loc.GetString("faction-rank-no-rank")
            };

            if (TryComp<FactionComponent>(memberUid, out var memberComp))
            {
                data.Rank = string.IsNullOrEmpty(memberComp.Rank) ? Loc.GetString("faction-rank-no-rank") : memberComp.Rank;
            }

            leaderComp.MemberData.Add(data);
        }
        Dirty(leaderUid, leaderComp);
    }
}
