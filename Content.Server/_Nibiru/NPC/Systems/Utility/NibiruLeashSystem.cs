using System.Numerics;
using Content.Server.NPC.Systems;
using Content.Server.Parallax;
using Content.Server.Stack;
using Content.Shared._Nibiru.NPC.Behavior.Components;
using Content.Shared._Nibiru.NPC.Utility;
using Content.Shared._Nibiru.NPC.Training;
using Content.Shared._Nibiru.NPC.Livestock;
using Content.Server._Nibiru.NPC.Systems.Behavior;
using Content.Shared.Alert;
using Content.Shared.Interaction;
using Content.Shared.Movement.Systems;
using Content.Shared.NPC;
using Content.Shared.Popups;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Audio.Systems;
using Content.Shared._Nibiru.NPC.Behavior;

namespace Content.Server._Nibiru.NPC.Systems.Utility;

public sealed partial class NibiruLeashSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private SharedJointSystem _joints = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private StackSystem _stack = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private NPCSteeringSystem _steering = default!;
    [Dependency] private BiomeSystem _biome = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NibiruLeashableComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<NibiruLeashAnchorComponent, InteractUsingEvent>(OnAnchorInteractUsing);
        SubscribeLocalEvent<NibiruLeashHolderComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovementSpeed);
    }

    private void OnAnchorInteractUsing(EntityUid uid, NibiruLeashAnchorComponent component, InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        // Check if the item is a rope
        if (!TryComp(args.Used, out MetaDataComponent? meta) || meta.EntityPrototype == null)
            return;

        var protoId = meta.EntityPrototype.ID;
        if (!protoId.Contains("Rope") && !protoId.Contains("Leash"))
            return;

        // If the player is already leading an animal, attach it to this anchor
        if (TryComp<NibiruLeashHolderComponent>(args.User, out var holder) && holder.LeashedAnimal.Valid)
        {
            if (TryComp<NibiruLeashableComponent>(holder.LeashedAnimal, out var leasable))
            {
                LeashTo(holder.LeashedAnimal, uid, leasable, args.Used);
                _popup.PopupEntity(Loc.GetString("nibiru-leash-attached-to-anchor", ("animal", holder.LeashedAnimal)), uid, args.User);
                args.Handled = true;
            }
        }
    }

    private void OnInteractUsing(EntityUid uid, NibiruLeashableComponent component, InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        // Check if the item is a rope
        if (!TryComp(args.Used, out MetaDataComponent? meta) || meta.EntityPrototype == null)
            return;

        var protoId = meta.EntityPrototype.ID;
        if (!protoId.Contains("Rope") && !protoId.Contains("Leash"))
            return;

        // If the animal is already leashed
        if (component.IsLeashed)
        {
            // If the player clicks with a rope on an animal already leashed to them - detach it
            if (component.LeashedTo == args.User)
            {
                Unleash(uid, component);
                _popup.PopupEntity(Loc.GetString("nibiru-leash-detached", ("animal", uid)), args.User, args.User);
            }
            else
            {
                // Take over the rope (don't spend it)
                LeashTo(uid, args.User, component, null);
                _popup.PopupEntity(Loc.GetString("nibiru-leash-attached", ("animal", uid)), args.User, args.User);
            }
        }
        else
        {
            // If not leashed - leash to the player (don't spend the rope)
            LeashTo(uid, args.User, component, null);
            _popup.PopupEntity(Loc.GetString("nibiru-leash-attached", ("animal", uid)), args.User, args.User);
        }

        args.Handled = true;
    }

    private void OnRefreshMovementSpeed(EntityUid uid, NibiruLeashHolderComponent component, RefreshMovementSpeedModifiersEvent args)
    {
        args.ModifySpeed(component.WalkSpeedModifier, component.SprintSpeedModifier);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<NibiruLeashableComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var leash, out var xform))
        {
            if (!leash.IsLeashed || leash.LeashedTo == null)
                continue;

            if (!Exists(leash.LeashedTo.Value))
            {
                Unleash(uid, leash);
                continue;
            }

            if (TryComp(leash.LeashedTo.Value, out TransformComponent? targetXform))
            {
                if (xform.Coordinates.TryDistance(EntityManager, targetXform.Coordinates, out var dist))
                {
                    var isAnchor = HasComp<NibiruLeashAnchorComponent>(leash.LeashedTo.Value);

                    // Physical constraint: if the animal is too far from the anchor, force it back
                    if (isAnchor && dist > leash.LeashLength)
                    {
                        var animalPos = _xform.GetMapCoordinates(uid);
                        var targetPosCoords = _xform.GetMapCoordinates(leash.LeashedTo.Value);
                        var dir = animalPos.Position - targetPosCoords.Position;
                        if (dir.LengthSquared() > 0.01f)
                        {
                            var newPos = targetPosCoords.Position + dir.Normalized() * leash.LeashLength;
                            _xform.SetWorldPosition(uid, newPos);
                            _steering.Unregister(uid); // Reset current path as it led outside the allowed range
                        }
                    }

                    // Chance of breaking
                    if (dist > leash.LeashLength * 1.5f)
                    {
                        var holder = leash.LeashedTo.Value;
                        Unleash(uid, leash, true); // true = broken
                        _popup.PopupEntity(Loc.GetString("nibiru-leash-broke-free", ("animal", uid)), holder, holder, PopupType.LargeCaution);
                        continue;
                    }
                }
            }

            // Attempt to break free (for untamed)
            if (leash.TryingToBreakFree)
            {
                leash.BreakFreeAccumulator += frameTime;
                if (leash.BreakFreeAccumulator >= leash.BreakFreeInterval)
                {
                    leash.BreakFreeAccumulator = 0f;

                    // It's twice as hard to break free at an anchor
                    var chance = leash.BreakFreeChance;
                    if (HasComp<NibiruLeashAnchorComponent>(leash.LeashedTo.Value))
                        chance *= 0.5f;

                    if (_random.Prob(chance))
                    {
                        var holder = leash.LeashedTo.Value;
                        Unleash(uid, leash, true);
                        _popup.PopupEntity(Loc.GetString("nibiru-leash-broke-free", ("animal", uid)), holder, holder, PopupType.LargeCaution);
                    }
                }
            }
        }
    }

    private void LeashTo(EntityUid animal, EntityUid target, NibiruLeashableComponent component, EntityUid? usedItem = null)
    {
        // Расходуем верёвку, если она используется
        if (usedItem != null && TryComp(usedItem.Value, out MetaDataComponent? meta))
        {
            if (!_stack.TryUse(usedItem.Value, 1))
                return;

            component.RopePrototype = meta.EntityPrototype?.ID;
        }

        // Detach from previous
        if (component.IsLeashed && component.LeashedTo != null)
        {
            if (TryComp<NibiruLeashHolderComponent>(component.LeashedTo.Value, out var oldHolder))
            {
                RemComp<NibiruLeashHolderComponent>(component.LeashedTo.Value);
                _movementSpeed.RefreshMovementSpeedModifiers(component.LeashedTo.Value);
                _alerts.ClearAlert(component.LeashedTo.Value, "Pulling");
            }
        }

        component.IsLeashed = true;
        component.LeashedTo = target;
        _biome.ClaimBiomeMob(animal);

        // If leashing to a player (not an anchor)
        if (!HasComp<NibiruLeashAnchorComponent>(target))
        {
            var holder = EnsureComp<NibiruLeashHolderComponent>(target);
            holder.LeashedAnimal = animal;
            _movementSpeed.RefreshMovementSpeedModifiers(target);
            _alerts.ShowAlert(target, "Pulling");

            // Override behavior — follow the holder
            if (TryComp<NibiruNpcStateMachineComponent>(animal, out var state))
            {
                state.CurrentTarget = target;
                state.CurrentState = NibiruNpcState.Following;
            }
        }
        else
        {
            // If leashing to an anchor, the animal stays near the anchor (Idle)
            if (TryComp<NibiruNpcStateMachineComponent>(animal, out var state))
            {
                state.CurrentTarget = null;
                state.CurrentState = NibiruNpcState.Idle;
                state.HomePosition = Transform(target).Coordinates;
                _steering.Unregister(animal);
            }
        }

        // Untamed animals try to break free
        if (!TryComp<NibiruTamableComponent>(animal, out var tamable) || !tamable.IsTamed)
        {
            component.TryingToBreakFree = true;
        }

        // Leash visual on the animal
        _appearance.SetData(animal, LivestockVisuals.IsLeashed, true);
        Dirty(animal, component);

        if (component.LeashSound != null)
            _audio.PlayPvs(component.LeashSound, animal);
    }

    private void Unleash(EntityUid animal, NibiruLeashableComponent component, bool broken = false)
    {
        var holderUid = component.LeashedTo;

        if (holderUid != null && TryComp<NibiruLeashHolderComponent>(holderUid.Value, out var holder))
        {
            RemComp<NibiruLeashHolderComponent>(holderUid.Value);
            _movementSpeed.RefreshMovementSpeedModifiers(holderUid.Value);
            _alerts.ClearAlert(holderUid.Value, "Pulling");
        }

        // If the leash is broken - drop the rope
        if (broken && component.RopePrototype != null && holderUid != null)
        {
            var animalPos = _xform.GetMapCoordinates(animal);
            var holderPos = _xform.GetMapCoordinates(holderUid.Value);
            var spawnPos = (animalPos.Position + holderPos.Position) / 2;

            var spawnMapPos = new MapCoordinates(spawnPos, animalPos.MapId);
            var spawned = Spawn(component.RopePrototype, spawnMapPos);
        }

        component.IsLeashed = false;
        component.LeashedTo = null;
        component.TryingToBreakFree = false;
        component.RopePrototype = null;
        Dirty(animal, component);
        component.BreakFreeAccumulator = 0f;

        // Return to normal behavior
        if (TryComp<NibiruNpcStateMachineComponent>(animal, out var state))
        {
            state.CurrentTarget = null;
            state.CurrentState = NibiruNpcState.Idle;
        }

        // Leash visual on the animal
        _appearance.SetData(animal, LivestockVisuals.IsLeashed, false);
    }
}
