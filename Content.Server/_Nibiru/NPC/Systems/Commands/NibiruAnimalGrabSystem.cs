using Content.Shared._Nibiru.NPC.Behavior;
using Content.Shared._Nibiru.NPC.Behavior.Components;
using Content.Shared._Nibiru.NPC.Commands;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Pulling.Events;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;
using System.Numerics;

namespace Content.Server._Nibiru.NPC.Systems.Commands;

public sealed partial class NibiruAnimalGrabSystem : EntitySystem
{
    [Dependency] private PullingSystem _pulling = default!;
    [Dependency] private MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NibiruAnimalGrabbedComponent, ComponentStartup>(OnGrabbedStartup);
        SubscribeLocalEvent<NibiruAnimalGrabbedComponent, ComponentShutdown>(OnGrabbedShutdown);
        SubscribeLocalEvent<NibiruAnimalGrabbedTargetComponent, RefreshMovementSpeedModifiersEvent>(OnTargetRefreshSpeed);
        SubscribeLocalEvent<NibiruAnimalGrabbedComponent, NibiruAnimalDetachDoAfterEvent>(OnDetachDoAfter);

        SubscribeLocalEvent<NibiruAnimalGrabbedComponent, PullStoppedMessage>(OnPullStop);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<NibiruAnimalGrabbedComponent>();
        while (query.MoveNext(out var animalUid, out var grabbed))
        {
            if (grabbed.Target == null || !Exists(grabbed.Target.Value))
            {
                RemComp<NibiruAnimalGrabbedComponent>(animalUid);
                continue;
            }

            var target = grabbed.Target.Value;

            if (grabbed.TickDamage != null)
            {
                grabbed.DamageAccumulator += frameTime;
                if (grabbed.DamageAccumulator >= grabbed.DamageInterval)
                {
                    grabbed.DamageAccumulator -= grabbed.DamageInterval;
                    _damageable.TryChangeDamage(target, grabbed.TickDamage, origin: animalUid);
                }
            }

            grabbed.ShakeAccumulator += frameTime;
            if (grabbed.ShakeAccumulator >= grabbed.ShakeInterval)
            {
                grabbed.ShakeAccumulator -= grabbed.ShakeInterval;
                grabbed.ShakeDirection = -grabbed.ShakeDirection;

                if (TryComp(animalUid, out TransformComponent? animalXform) &&
                    TryComp(target, out TransformComponent? targetXform) &&
                    TryComp<PhysicsComponent>(target, out var targetPhys))
                {
                    var animalPos = _xform.GetWorldPosition(animalXform);
                    var targetPos = _xform.GetWorldPosition(targetXform);

                    // Perpendicular to the vector animal→target
                    var toTarget = targetPos - animalPos;
                    Vector2 perp;
                    if (toTarget.LengthSquared() > 0.01f)
                    {
                        var norm = Vector2.Normalize(toTarget);
                        perp = new Vector2(-norm.Y, norm.X);
                    }
                    else
                    {
                        perp = Vector2.UnitX;
                    }

                    // Apply lateral impulse — do not throw, just change velocity.
                    // The speed is small, so the target only jerks slightly.
                    var shakeVelocity = perp * grabbed.ShakeAmplitude * grabbed.ShakeDirection;
                    _physics.SetLinearVelocity(target, shakeVelocity, body: targetPhys);
                }
            }
        }
    }

    private void OnGrabbedStartup(EntityUid uid, NibiruAnimalGrabbedComponent component, ComponentStartup args)
    {
        if (component.Target != null)
        {
            var targetComp = EnsureComp<NibiruAnimalGrabbedTargetComponent>(component.Target.Value);
            targetComp.Grabber = uid;
            Dirty(component.Target.Value, targetComp);
            _movementSpeed.RefreshMovementSpeedModifiers(component.Target.Value);
        }
    }

    private void OnGrabbedShutdown(EntityUid uid, NibiruAnimalGrabbedComponent component, ComponentShutdown args)
    {
        if (component.Target != null && Exists(component.Target.Value))
        {
            if (TryComp<PhysicsComponent>(component.Target.Value, out var phys))
                _physics.SetLinearVelocity(component.Target.Value, Vector2.Zero, body: phys);

            RemComp<NibiruAnimalGrabbedTargetComponent>(component.Target.Value);
            _movementSpeed.RefreshMovementSpeedModifiers(component.Target.Value);
        }
    }

    private void OnTargetRefreshSpeed(EntityUid uid, NibiruAnimalGrabbedTargetComponent component, RefreshMovementSpeedModifiersEvent args)
    {
        args.ModifySpeed(component.SlowdownMultiplier, component.SlowdownMultiplier);
    }

    //  Detachment only via DoAfter (no instant Pull break)

    /// <summary>
    /// Intercept any attempt to stop Pull (including target movement).
    /// Allow detachment ONLY via explicit owner request via DoAfter.
    /// </summary>
    private void OnAnimalInteractHand(EntityUid uid, NibiruAnimalGrabbedComponent component, AttemptStopPullingEvent args)
    {
        // If the target itself is not trying to free itself — ignore,
        // but in any case block automatic break.
        args.Cancelled = true;

        // If interaction is explicitly from the target — offer DoAfter for freeing
        if (args.User == null || args.User != component.Target)
            return;

        var doAfterArgs = new DoAfterArgs(EntityManager, args.User.Value, component.DetachDuration,
            new NibiruAnimalDetachDoAfterEvent(), uid, uid)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true
        };

        _doAfter.TryStartDoAfter(doAfterArgs);
        _popup.PopupEntity(Loc.GetString("nibiru-animal-grab-detaching"), uid, args.User.Value);
    }

    private void OnDetachDoAfter(EntityUid uid, NibiruAnimalGrabbedComponent component, NibiruAnimalDetachDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        // Stop the pull
        if (TryComp<PullableComponent>(uid, out var pullable))
            _pulling.TryStopPull(uid, pullable);

        // Remove the grab component
        RemComp<NibiruAnimalGrabbedComponent>(uid);

        // The animal returns to Idle mode
        if (TryComp<NibiruNpcStateMachineComponent>(uid, out var state))
        {
            state.CurrentState = NibiruNpcState.Idle;
            state.CurrentTarget = null;
        }

        _popup.PopupEntity(Loc.GetString("nibiru-animal-grab-detached"), uid, args.User);
    }

    private void OnPullStop(EntityUid uid, NibiruAnimalGrabbedComponent component, PullStoppedMessage args)
    {
        // If the pull was broken not through DoAfter — remove the grab component
        if (args.PulledUid != uid)
            return;
        RemComp<NibiruAnimalGrabbedComponent>(uid);

        if (TryComp<NibiruNpcStateMachineComponent>(args.PullerUid, out var state))
        {
            state.CurrentState = NibiruNpcState.Idle;
            state.CurrentTarget = null;
        }
    }

    /// <summary>
    /// Causes the animal to cling to the target.
    /// </summary>
    public bool TryGrabTarget(EntityUid animal, EntityUid target, DamageSpecifier? biteDamage = null)
    {
        // Inflict bite damage
        if (biteDamage != null)
            _damageable.TryChangeDamage(target, biteDamage, origin: animal);

        // Invert the pull: the target drags the animal
        if (!_pulling.TryStartPull(target, animal))
            return false;

        // Add the grab component
        var grabbed = EnsureComp<NibiruAnimalGrabbedComponent>(animal);
        grabbed.Target = target;
        Dirty(animal, grabbed);

        return true;
    }
}
