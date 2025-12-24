using Content.Server._Nibiru.World;
using Content.Server.Administration.Logs;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking.Rules;
using Content.Shared._Nibiru.GameTicking.Rules;
using Content.Shared.Database;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using JetBrains.Annotations;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Content.Server._Nibiru.Factions;
using Content.Shared._Nibiru.Factions;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server._Nibiru.GameTicking.Rules;

/// <summary>
/// Система управления игровым режимом Nibiru Survival
/// </summary>
public sealed partial class NibiruSurvivalRuleSystem : GameRuleSystem<NibiruSurvivalRuleComponent>
{
    [Dependency] private readonly NibiruWorldSystem _world = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly FactionSystem _factionSystem = default!;

    private ISawmill _sawmill = default!;

    /// <summary>
    /// Хранилище выбранных фракций игроками
    /// </summary>
    public readonly Dictionary<NetUserId, string?> PlayerFactionChoices = new();

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerBeforeSpawnEvent>(OnBeforeSpawn);
        //SubscribeNetworkEvent<LateJoinFactionMessage>(OnLateJoinFactionChoice);

        //InitializeCommands();
    }

    /// <summary>
    /// Сохраняет выбор фракции игрока
    /// </summary>
    public void OnLateJoinFactionChoice(ICommonSession session, string? FactionName)
    {
       //if (session.UserId is not { } userId)
       //     return;

        // Сохраняем выбор (null означает одиночный спавн)
        PlayerFactionChoices[session.UserId] = FactionName;

        _sawmill?.Info($"Player {session.Name} chose faction: {FactionName ?? "solo"}");
    }

    protected override void Added(EntityUid uid, NibiruSurvivalRuleComponent comp, GameRuleComponent gameRule, GameRuleAddedEvent args)
    {
        base.Added(uid, comp, gameRule, args);

        _world.InitializeWorld(comp);
    }

    private void OnBeforeSpawn(PlayerBeforeSpawnEvent ev)
    {
        var query = EntityQueryEnumerator<NibiruSurvivalRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var survivalComp, out var rule))
        {
            if (!GameTicker.IsGameRuleActive(uid, rule))
                continue;

            // Сначала спавним игрока обычным способом
            var entity = _world.SpawnPlayer(ev);

            // Затем проверяем, выбрал ли он фракцию
            if (ev.Player.UserId is { } userId &&
                PlayerFactionChoices.TryGetValue(userId, out var factionName))
            {
                // Если фракция выбрана, присоединяем к ней
                if (!string.IsNullOrEmpty(factionName) && entity != null)
                {
                    _factionSystem.TryJoinPlayerToFaction(entity.Value, factionName);
                }

                // Удаляем выбор после использования
                PlayerFactionChoices.Remove(userId);
            }

            ev.Handled = true;
            return;
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<DelayedStartRuleComponent, WorldRuleComponent>();
        while (query.MoveNext(out var uid, out var delay, out var rule))
        {
            if (_timing.CurTime < delay.RuleStartTime)
                continue;

            StartWorldRule(new(uid, rule));
        }
    }

    public NibiruSurvivalRuleComponent GetRule()
    {
        while (EntityQueryEnumerator<NibiruSurvivalRuleComponent>().MoveNext(out var comp))
        {
            return comp;
        }

        return EntityManager.ComponentFactory.GetComponent<NibiruSurvivalRuleComponent>();
    }

    public bool IsGameRuleActive(EntityUid ruleEntity, WorldRuleComponent? component = null)
    {
        return Resolve(ruleEntity, ref component) && HasComp<ActiveGameRuleComponent>(ruleEntity);
    }

    /// <summary>
    /// Adds a world rule to the list, but does not
    /// start it yet, instead waiting until the rule is actually started by other code
    /// </summary>
    /// <returns>The entity for the added worldrule</returns>
    [PublicAPI]
    public EntityUid AddWorldRule(EntProtoId ruleId, EntityUid target, EntityCoordinates targetCoordinates)
    {
        var ruleEntity = Spawn(ruleId, MapCoordinates.Nullspace);
        var comp = Comp<WorldRuleComponent>(ruleEntity);

        comp.Target = target;
        comp.TargetCoordinates = targetCoordinates;

        var str = $"Added world rule {ToPrettyString(ruleEntity)} for {ToPrettyString(target)}";
        _sawmill.Info(str);
        _chat.SendAdminAnnouncement(str);

        _adminLogger.Add(LogType.EventStarted, $"Added game rule {ToPrettyString(ruleEntity)} for {ToPrettyString(target)}");

        var ev = new WorldRuleAddedEvent(ruleEntity, ruleId, target, targetCoordinates);
        RaiseLocalEvent(ruleEntity, ref ev, true);
        return ruleEntity;
    }

    /// <summary>
    /// World rules can be 'started' separately from being added. 'Starting' them usually
    /// happens at round start while they can be added and removed before then.
    /// </summary>
    [PublicAPI]
    public bool StartWorldRule(
        EntProtoId ruleId,
        EntityUid target,
        EntityCoordinates targetCoordinates,
        bool ignoreDelay = false)
    {
        return StartWorldRule(ruleId, target, targetCoordinates, out _, ignoreDelay);
    }

    /// <summary>
    /// World rules can be 'started' separately from being added. 'Starting' them usually
    /// happens at round start while they can be added and removed before then.
    /// </summary>
    [PublicAPI]
    public bool StartWorldRule(
        EntProtoId ruleId,
        EntityUid target,
        EntityCoordinates targetCoordinates,
        out EntityUid ruleEntity,
        bool ignoreDelay = false)
    {
        ruleEntity = AddWorldRule(ruleId, target, targetCoordinates);
        return StartWorldRule(ruleEntity, ignoreDelay);
    }

    [PublicAPI]
    public bool StartWorldRule(Entity<WorldRuleComponent?> ruleEntity, bool ignoreDelay = false)
    {
        if (!Resolve(ruleEntity, ref ruleEntity.Comp)
            || !ruleEntity.Comp.Target.IsValid()
            || !ruleEntity.Comp.TargetCoordinates.IsValid(EntityManager))
            return false;

        return StartWorldRule(ruleEntity, ruleEntity.Comp.Target, ruleEntity.Comp.TargetCoordinates, ignoreDelay);
    }

    /// <summary>
    /// Game rules can be 'started' separately from being added. 'Starting' them usually
    /// happens at round start while they can be added and removed before then.
    /// </summary>
    [PublicAPI]
    public bool StartWorldRule(
        Entity<WorldRuleComponent?> ruleEntity,
        EntityUid target,
        EntityCoordinates targetCoordinates,
        bool ignoreDelay = false)
    {
        if (!Resolve(ruleEntity, ref ruleEntity.Comp)
            || HasComp<ActiveGameRuleComponent>(ruleEntity)
            || HasComp<EndedGameRuleComponent>(ruleEntity)
            || MetaData(ruleEntity).EntityPrototype is not { } proto)
            return false;

        ruleEntity.Comp.TargetCoordinates = targetCoordinates;
        ruleEntity.Comp.Target = target;

        // If we already have it, then we just skip the delay as it has already happened.
        if (!ignoreDelay && !RemComp<DelayedStartRuleComponent>(ruleEntity) && ruleEntity.Comp.Delay is { } delay)
        {
            var delayTime = TimeSpan.FromSeconds(delay.Next(_random));

            if (delayTime > TimeSpan.Zero)
            {
                var str = $"Queued start for world rule {ToPrettyString(ruleEntity)} with delay {delayTime}";
                _sawmill.Info(str);
                _chat.SendAdminAnnouncement(str);
                _adminLogger.Add(LogType.EventStarted,
                    $"Queued start for world rule {ToPrettyString(ruleEntity)} with delay {delayTime}");

                var delayed = EnsureComp<DelayedStartRuleComponent>(ruleEntity);
                delayed.RuleStartTime = _timing.CurTime + delayTime;
                return true;
            }
        }

        var msg = $"Started world rule {ToPrettyString(ruleEntity)}";
        _sawmill.Info(msg);
        _chat.SendAdminAnnouncement(msg);
        _adminLogger.Add(LogType.EventStarted,
            $"Started world rule {ToPrettyString(ruleEntity)}");

        EnsureComp<ActiveGameRuleComponent>(ruleEntity);

        var ev = new WorldRuleStartedEvent(ruleEntity, proto, target, targetCoordinates);
        RaiseLocalEvent(ruleEntity, ref ev, true);
        return true;
    }

    /// <summary>
    /// Ends a world rule.
    /// </summary>
    [PublicAPI]
    public bool EndWorldRule(Entity<WorldRuleComponent?> uid)
    {
        if (!Resolve(uid, ref uid.Comp))
            return false;

        // don't end it multiple times
        if (HasComp<EndedGameRuleComponent>(uid))
            return false;

        if (MetaData(uid).EntityPrototype is not { } proto) // you really fucked up
            return false;

        RemComp<ActiveGameRuleComponent>(uid);
        EnsureComp<EndedGameRuleComponent>(uid);

        _sawmill.Info($"Ended world rule {ToPrettyString(uid)} for {ToPrettyString(uid.Comp.Target)}");
        _adminLogger.Add(LogType.EventStopped, $"Ended world rule {ToPrettyString(uid)} for {ToPrettyString(uid.Comp.Target)}");

        var ev = new WorldRuleEndedEvent(uid, proto, uid.Comp.Target, uid.Comp.TargetCoordinates);
        RaiseLocalEvent(uid, ref ev, true);
        return true;
    }

    /// <summary>
    /// Возвращает список доступных фракций для UI
    /// </summary>
    public IReadOnlyList<FactionInfo> GetAvailableFactions()
    {
        return _factionSystem.AvailableFactions;
    }
}

[ByRefEvent]
public readonly record struct WorldRuleAddedEvent(EntityUid RuleEntity, EntProtoId RuleId, EntityUid Target, EntityCoordinates TargetCoordinates);

[ByRefEvent]
public readonly record struct WorldRuleStartedEvent(EntityUid RuleEntity, EntProtoId RuleId, EntityUid Target, EntityCoordinates TargetCoordinates);

[ByRefEvent]
public readonly record struct WorldRuleEndedEvent(EntityUid RuleEntity, EntProtoId RuleId, EntityUid Target, EntityCoordinates TargetCoordinates);
