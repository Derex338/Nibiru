using System.Numerics;
using Content.Server.NPC.Systems;
using Content.Shared._Nibiru.NPC.Behavior;
using Content.Shared._Nibiru.NPC.Behavior.Components;
using Content.Shared._Nibiru.NPC.Commands;
using Content.Shared._Nibiru.NPC.Training;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Nibiru.NPC.Systems.Behavior;

public sealed partial class NibiruAnimalAbilitySystem : EntitySystem
{
    [Dependency] private NPCSteeringSystem _steering = default!;
    [Dependency] private NpcFactionSystem _faction = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;

    private void ProcessPestControl(EntityUid uid, NibiruAnimalAbilityComponent ability, TransformComponent xform)
    {
        var myPos = _xform.GetMapCoordinates((uid, xform));
        var nearbyEntities = new HashSet<EntityUid>();
        _lookup.GetEntitiesInRange(uid, ability.SearchRadius * 0.5f, nearbyEntities);

        foreach (var nearby in nearbyEntities)
        {
            if (nearby == uid)
                continue;

            if (!TryComp(nearby, out MetaDataComponent? meta) || meta.EntityPrototype == null)
                continue;

            var protoId = meta.EntityPrototype.ID;
            if (protoId.Contains("Mouse") || protoId.Contains("Cockroach") || protoId.Contains("Pest"))
            {
                if (TryComp<NibiruNpcStateMachineComponent>(uid, out var state))
                {
                    state.CurrentTarget = nearby;
                    state.CurrentState = NibiruNpcState.Chasing;
                    ability.CooldownAccumulator = ability.AbilityCooldown;
                    return;
                }
            }
        }
    }

    private void ProcessDelivery(EntityUid uid, NibiruAnimalAbilityComponent ability, TransformComponent xform)
    {
        if (ability.CarriedItem == null || !Exists(ability.CarriedItem.Value))
            return;

        if (TryComp<NibiruNpcStateMachineComponent>(uid, out var state) &&
            state.CurrentTarget != null &&
            state.CurrentState == NibiruNpcState.Following)
        {
            var targetXform = Transform(state.CurrentTarget.Value);
            if (xform.Coordinates.TryDistance(EntityManager, targetXform.Coordinates, out var dist) && dist < 1.5f)
            {
                var itemXform = Transform(ability.CarriedItem.Value);
                _xform.SetCoordinates(ability.CarriedItem.Value, targetXform.Coordinates);
                ability.CarriedItem = null;
                ability.CooldownAccumulator = ability.AbilityCooldown;

                state.CurrentState = NibiruNpcState.Returning;
            }
        }
    }

    public bool StartDelivery(EntityUid bird, EntityUid item, EntityUid target)
    {
        if (!TryComp<NibiruAnimalAbilityComponent>(bird, out var ability))
            return false;

        // ... (checks)

        if (TryComp<NibiruNpcStateMachineComponent>(bird, out var state))
        {
            state.CurrentTarget = target;
            state.CurrentState = NibiruNpcState.Following;
        }

        return true;
    }
}
