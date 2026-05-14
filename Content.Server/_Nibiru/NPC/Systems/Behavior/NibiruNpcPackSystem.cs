// Obsolete root using removed
using Content.Shared._Nibiru.NPC.Behavior;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.NPC;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Robust.Shared.Timing;

namespace Content.Server._Nibiru.NPC.Systems.Behavior;

/// <summary>
/// Управляет стайным поведением NPC
/// Синхронизирует цели между членами стаи, обрабатывает иерархию
/// и панику при потере лидера.
/// </summary>
public sealed class NibiruNpcPackSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly NpcFactionSystem _faction = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NibiruNpcPackComponent, ComponentStartup>(OnPackStartup);
        SubscribeLocalEvent<NibiruNpcPackComponent, ComponentShutdown>(OnPackShutdown);
    }

    private void OnPackStartup(EntityUid uid, NibiruNpcPackComponent component, ComponentStartup args)
    {
        // Если PackId не задан, генерируем уникальный
        if (string.IsNullOrEmpty(component.PackId))
            component.PackId = $"pack_{uid}_{_timing.CurTick}";

        // Автоопределение лидера: ищем других членов стаи рядом
        if (!component.IsLeader)
            TryFindLeader(uid, component);
    }

    private void OnPackShutdown(EntityUid uid, NibiruNpcPackComponent component, ComponentShutdown args)
    {
        // Если умер лидер — стая паникует
        if (component.IsLeader)
            NotifyPackLeaderDead(uid, component);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<NibiruNpcPackComponent, NibiruNpcBehaviorComponent, ActiveNPCComponent>();
        while (query.MoveNext(out var uid, out var pack, out var behavior, out _))
        {
            // Обработка паники
            if (pack.PanicTimer > 0)
            {
                pack.PanicTimer -= frameTime;

                // Во время паники все разбегаются
                if (behavior.CurrentState != NibiruNpcState.Fleeing)
                    behavior.CurrentState = NibiruNpcState.Fleeing;

                continue;
            }

            // Синхронизация целей от членов стаи
            SyncPackTargets(uid, pack, behavior);
        }
    }

    /// <summary>
    /// Когда один член стаи обнаруживает врага, передает цель всей стае.
    /// </summary>
    private void SyncPackTargets(EntityUid uid, NibiruNpcPackComponent pack, NibiruNpcBehaviorComponent behavior)
    {
        if (behavior.CurrentTarget == null)
            return;

        var myXform = Transform(uid);
        var packQuery = EntityQueryEnumerator<NibiruNpcPackComponent, NibiruNpcBehaviorComponent>();

        while (packQuery.MoveNext(out var otherUid, out var otherPack, out var otherBehavior))
        {
            if (otherUid == uid)
                continue;

            if (otherPack.PackId != pack.PackId)
                continue;

            // Проверяем дистанцию связи
            var otherXform = Transform(otherUid);
            if (!myXform.Coordinates.TryDistance(EntityManager, otherXform.Coordinates, out var dist))
                continue;

            if (dist > pack.PackCommunicationRange)
                continue;

            // Передаём цель, если у сородича нет своей
            if (otherBehavior.CurrentTarget == null && otherBehavior.CurrentState == NibiruNpcState.Idle)
            {
                otherBehavior.CurrentTarget = behavior.CurrentTarget;
                otherBehavior.CurrentState = NibiruNpcState.Chasing;
            }
        }
    }

    /// <summary>
    /// Уведомляет стаю о смерти лидера, вызывая панику.
    /// </summary>
    private void NotifyPackLeaderDead(EntityUid leaderUid, NibiruNpcPackComponent leaderPack)
    {
        var query = EntityQueryEnumerator<NibiruNpcPackComponent, NibiruNpcBehaviorComponent>();
        while (query.MoveNext(out var uid, out var pack, out var behavior))
        {
            if (uid == leaderUid || pack.PackId != leaderPack.PackId)
                continue;

            pack.PanicTimer = pack.PanicDuration;
            pack.LeaderUid = null;
            behavior.CurrentTarget = null;
        }
    }

    /// <summary>
    /// Ищет лидера стаи среди ближайших сородичей.
    /// </summary>
    private void TryFindLeader(EntityUid uid, NibiruNpcPackComponent pack)
    {
        var myXform = Transform(uid);
        var query = EntityQueryEnumerator<NibiruNpcPackComponent>();

        while (query.MoveNext(out var otherUid, out var otherPack))
        {
            if (otherUid == uid)
                continue;

            if (otherPack.PackId != pack.PackId || !otherPack.IsLeader)
                continue;

            var otherXform = Transform(otherUid);
            if (myXform.Coordinates.TryDistance(EntityManager, otherXform.Coordinates, out var dist)
                && dist <= pack.PackCommunicationRange)
            {
                pack.LeaderUid = otherUid;
                return;
            }
        }
    }

    /// <summary>
    /// Формирует стаю из NPC, находящихся рядом друг с другом.
    /// </summary>
    public void FormPack(EntityUid leader, float radius)
    {
        if (!TryComp<NibiruNpcPackComponent>(leader, out var leaderPack))
            return;

        leaderPack.IsLeader = true;
        var leaderXform = Transform(leader);

        var nearbyEntities = new HashSet<EntityUid>();
        _lookup.GetEntitiesInRange(leader, radius, nearbyEntities);

        foreach (var nearby in nearbyEntities)
        {
            if (nearby == leader || !TryComp<NibiruNpcPackComponent>(nearby, out var pack))
                continue;

            pack.PackId = leaderPack.PackId;
            pack.LeaderUid = leader;
            pack.IsLeader = false;
        }
    }
}
