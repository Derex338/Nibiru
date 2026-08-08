using Content.Shared._CE.DayCycle;
using Content.Shared.Bed.Sleep;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._Nibiru.NPC.Behavior;

public sealed partial class AnimalSleepSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SleepingSystem _sleepingSystem = default!;
    [Dependency] private CEDayCycleSystem _dayCycle = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private StatusEffectsSystem _statusEffects = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NibiruAnimalSleepComponent, DamageChangedEvent>(OnDamageChanged);
    }

    private void OnDamageChanged(EntityUid uid, NibiruAnimalSleepComponent component, DamageChangedEvent args)
    {
        if (args.DamageIncreased && HasComp<SleepingComponent>(uid) && component.Energy > 50)
        {
            _sleepingSystem.TryWaking((uid, null), force: true);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_net.IsClient)
            return;

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<NibiruAnimalSleepComponent, TransformComponent>();
        var isDayCache = new Dictionary<EntityUid, bool>();

        while (query.MoveNext(out var uid, out var sleepComp, out var xform))
        {
            if (xform.MapUid == null)
                continue;

            var mapUid = xform.MapUid.Value;
            if (!isDayCache.TryGetValue(mapUid, out var isDay))
            {
                isDay = _dayCycle.IsDayNow(mapUid);
                isDayCache[mapUid] = isDay;
            }

            bool isNaturalSleepTime = sleepComp.Cycle == SleepCycle.Diurnal ? !isDay : isDay;
            bool isSleeping = HasComp<SleepingComponent>(uid);

            if (isSleeping)
            {
                // Recover energy
                sleepComp.Energy = MathF.Min(sleepComp.MaxEnergy, sleepComp.Energy + sleepComp.EnergyRecoverRate * frameTime);

                // Wake up if it's natural wake time AND we have > 0 energy
                if (!isNaturalSleepTime && sleepComp.Energy > 20)
                {
                    _sleepingSystem.TryWaking((uid, null));
                    continue;
                }

                // Check proximity wake
                if (sleepComp.EnableProximityWake)
                {
                    if (CheckProximityWake(uid, sleepComp, xform))
                    {
                        _sleepingSystem.TryWaking((uid, null));
                        continue;
                    }
                }

                // Spawn Zzz effect
                if (sleepComp.SleepVisualEffectPrototype != null && curTime >= sleepComp.NextVisualEffectTime)
                {
                    sleepComp.NextVisualEffectTime = curTime + sleepComp.SleepVisualEffectInterval;
                    Spawn(sleepComp.SleepVisualEffectPrototype, xform.Coordinates);
                }
            }
            else
            {
                // Drain energy
                sleepComp.Energy = MathF.Max(0f, sleepComp.Energy - sleepComp.EnergyDrainRate * frameTime);

                if (sleepComp.Energy <= 0f)
                {
                    if (!_statusEffects.TryAddStatusEffectDuration(uid, SleepingSystem.StatusEffectForcedSleeping, TimeSpan.FromSeconds(30)))
                    {
                        _sleepingSystem.TrySleeping((uid, null));
                    }
                }
                else if (isNaturalSleepTime)
                {
                    _sleepingSystem.TrySleeping((uid, null));
                }
            }
        }
    }

    private bool CheckProximityWake(EntityUid uid, NibiruAnimalSleepComponent component, TransformComponent xform)
    {
        var entities = _lookup.GetEntitiesInRange(xform.Coordinates, component.WakeProximityRadius);
        foreach (var ent in entities)
        {
            if (ent == uid) continue;

            // If an entity is moving fast enough near the animal, it wakes up.
            if (TryComp<PhysicsComponent>(ent, out var physics))
            {
                if (physics.LinearVelocity.Length() >= component.WakeProximitySpeedThreshold)
                {
                    return true;
                }
            }
        }
        return false;
    }
}
