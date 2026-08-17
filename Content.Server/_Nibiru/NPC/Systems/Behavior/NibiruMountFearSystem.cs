using Content.Shared._Nibiru.NPC.Behavior.Components;
using Content.Shared._Nibiru.NPC.Training;
using Content.Shared.Buckle.Components;
using Content.Shared.Buckle;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;
using Content.Shared._Nibiru.NPC.Behavior;

namespace Content.Server._Nibiru.NPC.Systems.Behavior;

/// <summary>
/// Handles the fear bar of mountable animals.
/// Fear accumulates from damage, the number of nearby threats, and fire.
/// When at maximum, it throws the rider and runs away.
/// Constant exposure to stress trains resilience.
/// </summary>
public sealed partial class NibiruMountFearSystem : EntitySystem
{
    [Dependency] private SharedBuckleSystem _buckle = default!;
    [Dependency] private NpcFactionSystem _faction = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NibiruMountFearComponent, DamageChangedEvent>(OnDamaged);
    }

    private void OnDamaged(EntityUid uid, NibiruMountFearComponent component, DamageChangedEvent args)
    {
        if (!args.DamageIncreased)
            return;

        var fearIncrease = component.FearPerDamage * GetTrainingMultiplier(component);
        component.FearLevel = MathF.Min(component.FearLevel + fearIncrease, component.MaxFear);

        CheckPanic(uid, component);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<NibiruMountFearComponent, RideableComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var fear, out var rideable, out var xform))
        {
            if (fear.IsPanicking)
            {
                fear.PanicTimer -= frameTime;
                if (fear.PanicTimer <= 0)
                {
                    fear.IsPanicking = false;
                    fear.FearLevel = fear.MaxFear * 0.3f;
                }
                continue;
            }

            // Periodically check for threats
            fear.ThreatCheckAccumulator += frameTime;
            if (fear.ThreatCheckAccumulator >= fear.ThreatCheckInterval)
            {
                fear.ThreatCheckAccumulator = 0f;
                ScanThreats(uid, fear, xform);
            }

            // Fear decay when no threats are present
            if (fear.FearLevel > 0)
            {
                fear.FearLevel = MathF.Max(0, fear.FearLevel - fear.FearDecayRate * frameTime);
            }

            // Nervousness at medium levels
            if (fear.FearLevel > fear.MaxFear * 0.5f && fear.NervousSound != null)
            {
                // Will be played periodically in ScanThreats
            }
        }
    }

    /// <summary>
    /// Scans the surroundings for threats and increases fear.
    /// </summary>
    private void ScanThreats(EntityUid uid, NibiruMountFearComponent fear, TransformComponent xform)
    {
        var mapCoords = _xform.GetMapCoordinates((uid, xform));
        int threatCount = 0;
        bool fireNearby = false;

        foreach (var entity in _lookup.GetEntitiesInRange<NpcFactionMemberComponent>(mapCoords, fear.ThreatScanRadius))
        {
            if (entity.Owner == uid)
                continue;

            if (!_faction.IsEntityFriendly(uid, entity.Owner))
                threatCount++;
        }

        // Check for nearby fire
        var nearbyEntities = new HashSet<EntityUid>();
        _lookup.GetEntitiesInRange(uid, fear.ThreatScanRadius, nearbyEntities);

        foreach (var nearby in nearbyEntities)
        {
            if (!TryComp(nearby, out MetaDataComponent? meta) || meta.EntityPrototype == null)
                continue;

            var protoId = meta.EntityPrototype.ID;
            if (protoId.Contains("Torch") || protoId.Contains("Bonfire") ||
                protoId.Contains("Campfire") || protoId.Contains("Fire"))
            {
                fireNearby = true;
                break;
            }
        }

        var trainingMult = GetTrainingMultiplier(fear);

        // Accumulating fear from threats
        if (threatCount > 0)
        {
            var threatFear = threatCount * fear.FearPerNearbyThreat * trainingMult;
            fear.FearLevel = MathF.Min(fear.FearLevel + threatFear, fear.MaxFear);

            // Accumulating stress resistance experience
            fear.StressTraining = MathF.Min(
                fear.StressTraining + fear.TrainingPerStressTick,
                fear.MaxStressTraining);
        }

        // Fear from fire
        if (fireNearby)
        {
            fear.FearLevel = MathF.Min(fear.FearLevel + fear.FearFromFire * trainingMult, fear.MaxFear);

            fear.StressTraining = MathF.Min(
                fear.StressTraining + fear.TrainingPerStressTick * 0.5f,
                fear.MaxStressTraining);
        }

        CheckPanic(uid, fear);
    }

    /// <summary>
    /// Checks if fear has reached a critical level. If so — panic.
    /// </summary>
    private void CheckPanic(EntityUid uid, NibiruMountFearComponent fear)
    {
        if (fear.IsPanicking || fear.FearLevel < fear.MaxFear)
            return;

        fear.IsPanicking = true;
        fear.PanicTimer = fear.PanicDuration;

        // Unbuckling all riders
        if (TryComp<StrapComponent>(uid, out var strap))
        {
            foreach (var rider in new List<EntityUid>(strap.BuckledEntities))
            {
                _buckle.TryUnbuckle(rider, rider, true);
            }
        }

        // Switch to fleeing state
        if (TryComp<NibiruNpcStateMachineComponent>(uid, out var state))
        {
            state.CurrentState = NibiruNpcState.Fleeing;
            state.CurrentTarget = null;
        }

        // Panic sound
        if (fear.PanicSound != null)
            _audio.PlayPvs(fear.PanicSound, uid);
    }

    /// <summary>
    /// Calculates the fear reduction multiplier from training.
    /// At maximum training, fear is reduced by MaxFearReduction (up to 70%).
    /// </summary>
    private float GetTrainingMultiplier(NibiruMountFearComponent fear)
    {
        var trainingRatio = fear.StressTraining / fear.MaxStressTraining;
        return 1f - trainingRatio * fear.MaxFearReduction;
    }
}
