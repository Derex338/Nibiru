// Obsolete root using removed
using Content.Shared._Nibiru.NPC.Behavior;
using Content.Shared._Nibiru.NPC.Behavior.Components;
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
public sealed partial class NibiruNpcPackSystem : EntitySystem
{
[Dependency] private IGameTiming _timing = default!;
[Dependency] private EntityLookupSystem _lookup = default!;

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

        var query = EntityQueryEnumerator<NibiruNpcPackComponent, NibiruNpcStateMachineComponent, ActiveNPCComponent>();
        while (query.MoveNext(out var uid, out var pack, out var state, out _))
        {
            if (pack.PanicTimer > 0)
            {
                pack.PanicTimer -= frameTime;
                if (state.CurrentState != NibiruNpcState.Fleeing)
                    state.CurrentState = NibiruNpcState.Fleeing;
                continue;
            }
            SyncPackTargets(uid, pack, state);
        }
    }

    private void SyncPackTargets(EntityUid uid, NibiruNpcPackComponent pack, NibiruNpcStateMachineComponent state)
    {
        if (state.CurrentTarget == null)
            return;

        var myXform = Transform(uid);
        var packQuery = EntityQueryEnumerator<NibiruNpcPackComponent, NibiruNpcStateMachineComponent>();

        while (packQuery.MoveNext(out var otherUid, out var otherPack, out var otherState))
        {
            if (otherUid == uid)
                continue;

            if (otherPack.PackId != pack.PackId)
                continue;

            var otherXform = Transform(otherUid);
            if (!myXform.Coordinates.TryDistance(EntityManager, otherXform.Coordinates, out var dist))
                continue;

            if (dist > pack.PackCommunicationRange)
                continue;

            if (otherState.CurrentTarget == null && otherState.CurrentState == NibiruNpcState.Idle)
            {
                otherState.CurrentTarget = state.CurrentTarget;
                otherState.CurrentState = NibiruNpcState.Chasing;
            }
        }
    }

    private void NotifyPackLeaderDead(EntityUid leaderUid, NibiruNpcPackComponent leaderPack)
    {
        var query = EntityQueryEnumerator<NibiruNpcPackComponent, NibiruNpcStateMachineComponent>();
        while (query.MoveNext(out var uid, out var pack, out var state))
        {
            if (uid == leaderUid || pack.PackId != leaderPack.PackId)
                continue;

            pack.PanicTimer = pack.PanicDuration;
            pack.LeaderUid = null;
            state.CurrentTarget = null;
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
