using System.Numerics;
using Content.Server.NPC.Systems;
using Content.Shared._Nibiru.NPC.Behavior;
using Content.Shared._Nibiru.NPC.Behavior.Components;
using Content.Shared._Nibiru.NPC.Livestock;
using Content.Shared.NPC;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Server._Nibiru.NPC.Systems.Behavior;

/// <summary>
/// Обрабатывает территориальное поведение, циклы сна и боязнь огня.
/// </summary>
public sealed partial class NibiruAdvancedBehaviorSystem : EntitySystem
{
    [Dependency] private NpcFactionSystem _faction = default!;
    [Dependency] private NPCSteeringSystem _steering = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private IRobustRandom _random = default!;

    private const float TerritoryCheckInterval = 2f;
    private const float FireCheckInterval = 0.2f;
    private float _territoryAccumulator;
    private float _fireAccumulator;

    public override void Initialize()
    {
        base.Initialize();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _territoryAccumulator += frameTime;
        _fireAccumulator += frameTime;

        if (_territoryAccumulator >= TerritoryCheckInterval)
        {
            _territoryAccumulator = 0f;
            UpdateTerritoriality();
        }

        //UpdateSleepCycles(frameTime);

        if (_fireAccumulator >= FireCheckInterval)
        {
            _fireAccumulator = 0f;
            UpdateFireFear();
        }
    }

    #region Территориальность

    private void UpdateTerritoriality()
    {
        var query = EntityQueryEnumerator<NibiruTerritorialComponent, NibiruNpcStateMachineComponent, NibiruNpcPerceptionComponent, TransformComponent >();
        while (query.MoveNext(out var uid, out var territorial, out var behavior, out var perception, out var xform))
        {
            if (!HasComp<ActiveNPCComponent>(uid))
                continue;

            CheckOffspring(uid, territorial, xform);

            if (behavior.HomePosition == null)
                continue;

            foreach (var detected in perception.DetectedEntities)
            {
                if (!Exists(detected))
                    continue;

                if (_faction.IsEntityFriendly(uid, detected))
                    continue;

                if (!TryComp(detected, out TransformComponent? detectedXform) || !TryComp< NibiruNpcAggroComponent>(uid, out var agro))
                    continue;

                if (!behavior.HomePosition.Value.TryDistance(EntityManager, detectedXform.Coordinates, out var distToHome))
                    continue;

                if (distToHome <= territorial.TerritoryRadius)
                {
                    var multiplier = territorial.HasOffspringNearby
                        ? territorial.OffspringProtectionMultiplier
                        : territorial.TerritoryAggressionMultiplier;

                    behavior.CurrentTarget = detected;
                    behavior.CurrentState = NibiruNpcState.Chasing;
                    agro.AggroRange *= multiplier;

                    if (territorial.WarningSound != null)
                        _audio.PlayPvs(territorial.WarningSound, uid);

                    break;
                }
            }
        }
    }

    private void CheckOffspring(EntityUid uid, NibiruTerritorialComponent territorial, TransformComponent xform)
    {
        territorial.HasOffspringNearby = false;

        var nearbyEntities = new HashSet<EntityUid>();
        _lookup.GetEntitiesInRange(uid, territorial.TerritoryRadius, nearbyEntities);

        foreach (var nearby in nearbyEntities)
        {
            if (nearby == uid || !TryComp<NibiruLivestockComponent>(nearby, out var livestock))
                continue;

            if (!TryComp(nearby, out MetaDataComponent? nearbyMeta) ||
                !TryComp(uid, out MetaDataComponent? myMeta))
                continue;

            if (nearbyMeta.EntityPrototype?.ID == myMeta.EntityPrototype?.ID)
            {
                territorial.HasOffspringNearby = true;
                return;
            }
        }
    }

    #endregion

    #region Циклы сна

    private void UpdateSleepCycles(float frameTime)
    {
        var query = EntityQueryEnumerator<NibiruSleepCycleComponent, NibiruNpcStateMachineComponent, NibiruNpcPerceptionComponent>();
        while (query.MoveNext(out var uid, out var sleep, out var behavior, out var perception))
        {
            sleep.CycleAccumulator += frameTime;

            if (sleep.IsSleeping)
            {
                if (sleep.CycleAccumulator >= sleep.SleepDuration)
                {
                    WakeUp(uid, sleep, perception);
                }
                else
                {
                    if (behavior.CurrentState != NibiruNpcState.Idle)
                    {
                        if (TryComp< NibiruNpcMemoryComponent>(uid, out var memoryComponent) && memoryComponent.HostileMemory.Count > 0)
                        {
                            WakeUp(uid, sleep, perception);
                        }
                        else
                        {
                            behavior.CurrentState = NibiruNpcState.Idle;
                            behavior.CurrentTarget = null;
                        }
                    }
                }
            }
            else
            {
                if (sleep.CycleAccumulator >= sleep.WakeDuration)
                {
                    FallAsleep(uid, sleep, behavior, perception);
                }
            }
        }
    }

    private void FallAsleep(EntityUid uid, NibiruSleepCycleComponent sleep,
        NibiruNpcStateMachineComponent behavior, NibiruNpcPerceptionComponent perception)
    {
        sleep.IsSleeping = true;
        sleep.CycleAccumulator = 0f;

        perception.VisionRange *= sleep.SleepPerceptionMultiplier;
        perception.HearingRange *= sleep.SleepPerceptionMultiplier;

        behavior.CurrentState = NibiruNpcState.Idle;
        behavior.CurrentTarget = null;
        _steering.Unregister(uid);

        if (sleep.SleepSound != null)
            _audio.PlayPvs(sleep.SleepSound, uid);
    }

    private void WakeUp(EntityUid uid, NibiruSleepCycleComponent sleep, NibiruNpcPerceptionComponent perception)
    {
        if (sleep.IsSleeping)
        {
            perception.VisionRange /= sleep.SleepPerceptionMultiplier;
            perception.HearingRange /= sleep.SleepPerceptionMultiplier;
        }

        sleep.IsSleeping = false;
        sleep.CycleAccumulator = 0f;

        if (sleep.WakeSound != null)
            _audio.PlayPvs(sleep.WakeSound, uid);
    }

    #endregion

    #region Боязнь огня

    private void UpdateFireFear()
    {
        var query = EntityQueryEnumerator<NibiruFireFearComponent, NibiruNpcStateMachineComponent, ActiveNPCComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var fireFear, out var behavior, out _, out var xform))
        {
            var nearbyEntities = new HashSet<EntityUid>();
            _lookup.GetEntitiesInRange(uid, fireFear.FireDetectionRange, nearbyEntities);

            EntityUid? fireSource = null;
            foreach (var nearby in nearbyEntities)
            {
                if (!Exists(nearby))
                    continue;

                if (!TryComp(nearby, out MetaDataComponent? meta) || meta.EntityPrototype == null)
                    continue;

                var protoId = meta.EntityPrototype.ID;
                foreach (var tag in fireFear.FireTags)
                {
                    if (protoId.Contains(tag))
                    {
                        fireSource = nearby;
                        break;
                    }
                }
                if (fireSource != null)
                    break;
            }

            if (fireSource == null)
                continue;

            behavior.CurrentTarget = fireSource;
            behavior.CurrentState = NibiruNpcState.Fleeing;

            if (fireFear.FireFearSound != null)
                _audio.PlayPvs(fireFear.FireFearSound, uid);
        }
    }

    #endregion
}
