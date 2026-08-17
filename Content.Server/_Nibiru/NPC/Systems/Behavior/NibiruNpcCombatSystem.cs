using Content.Server.Jittering;
using Content.Server.NPC.Systems;
using Content.Shared._Nibiru.NPC.Behavior;
using Content.Shared._Nibiru.NPC.Behavior.Components;
using Content.Shared.CombatMode;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Jittering;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Systems;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Melee;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;
using System.Numerics;

namespace Content.Server._Nibiru.NPC.Systems.Behavior;

/// <summary>
/// Nibiru NPC combat behavior system.
///
/// Implements three combat styles:
///
/// <b>Default</b> — classic melee combat with a short retreat after hitting.
/// The animal pursues the target, attacks with melee, retreats using steering,
/// waits for cooldown and repeats. Used for most animals.
///
/// <b>HitAndLeap</b> — "bite and leap back" tactic (for wolves and similar predators).
/// The animal approaches the target, bites, then receives a physics impulse strictly
/// backward relative to its body rotation — without using NPC navigation.
/// After landing it pauses and then goes back on the attack.
///
/// <b>Charge</b> — Charge attack (for horned animals: goats, cows).
/// The animal enters the charge range, fully stops, turns
/// to the target, starts shaking (WindUp), then flies in a straight line through physical
/// impulse, dealing damage and knocking back everything in its path. Stops on collision
/// with a wall or when the maximum charge time is reached.
/// </summary>
public sealed partial class NibiruNpcCombatSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedMeleeWeaponSystem _melee = default!;
    [Dependency] private SharedCombatModeSystem _combat = default!;
    [Dependency] private JitteringSystem _jitter = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private NPCSteeringSystem _steering = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private ThrowingSystem _throwing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<NibiruNpcChargeAttackComponent, StartCollideEvent>(OnChargeCollide);
    }

    //  Entry point

    /// <summary>
    /// Called by <see cref="NibiruNpcBehaviorSystem"/> on each frame
    /// for Attacking and Fleeing states.
    /// </summary>
    public void ProcessCombat(
        EntityUid uid,
        NibiruNpcStateMachineComponent state,
        NibiruNpcCombatComponent combat,
        TransformComponent xform,
        float frameTime)
    {
        switch (state.CurrentState)
        {
            case NibiruNpcState.Attacking:
                ProcessAttacking(uid, state, combat, xform, frameTime);
                break;

            case NibiruNpcState.Fleeing:
                ProcessFleeing(uid, state, xform);
                break;
        }
    }

    //  Attacking — styles dispatcher
    private void ProcessAttacking(
        EntityUid uid,
        NibiruNpcStateMachineComponent state,
        NibiruNpcCombatComponent combat,
        TransformComponent xform,
        float frameTime)
    {
        // Target validation
        if (!ValidateTarget(uid, state, out var target, out var targetXform))
        {
            ResetToReturning(uid, state, combat);
            return;
        }

        if (!xform.Coordinates.TryDistance(EntityManager, targetXform.Coordinates, out var distance))
        {
            ResetToReturning(uid, state, combat);
            return;
        }

        //  Styles dispatcher
        switch (combat.CombatStyle)
        {
            case NibiruCombatStyle.HitAndRun
                when TryComp<NibiruNpcHitAndRunAttackComponent>(uid, out var leap):
                ProcessHitAndLeap(uid, state, combat, leap, xform, target, targetXform, distance, frameTime);
                break;

            default:
                ProcessDefault(uid, state, combat, xform, target, targetXform, distance, frameTime);
                break;
        }
    }

    //  Default — classic melee combat with a short retreat

    /// <summary>
    /// Classic style: approach → strike → retreat → wait → repeat.
    /// Retreat is handled via NPC steering (safe, no physics artifacts).
    /// Suitable for large slow animals and basic enemies.
    /// </summary>
    private void ProcessDefault(
        EntityUid uid,
        NibiruNpcStateMachineComponent state,
        NibiruNpcCombatComponent combat,
        TransformComponent xform,
        EntityUid target,
        TransformComponent targetXform,
        float distance,
        float frameTime)
    {
        // Retreat phase
        if (combat.IsRetreating)
        {
            combat.RetreatTimer -= frameTime;
            if (combat.RetreatTimer <= 0f)
            {
                combat.IsRetreating = false;
                // Reset steering — NPC will resume chasing
            }
            return;
        }

        // Too far — continue chasing
        if (distance > 2.0f)
        {
            state.CurrentState = NibiruNpcState.Chasing;
            return;
        }

        // Activate combat mode
        if (TryComp<CombatModeComponent>(uid, out var combatMode) && !combatMode.IsInCombatMode)
            _combat.SetInCombatMode(uid, true, combatMode);

        // Attack
        if (_melee.TryGetWeapon(uid, out var weaponUid, out var weapon) &&
            _timing.CurTime >= weapon.NextAttack)
        {
            _melee.AttemptLightAttack(uid, weaponUid, weapon, target);

            // Begin retreat from the target
            var myPos = _xform.GetWorldPosition(xform);
            var targetPos = _xform.GetWorldPosition(targetXform);
            var awayDir = (myPos - targetPos);
            if (awayDir.LengthSquared() > 0.01f)
                awayDir = Vector2.Normalize(awayDir);
            else
                awayDir = Vector2.UnitX;

            var retreatPoint = new EntityCoordinates(
                xform.ParentUid,
                xform.LocalPosition + awayDir * combat.PostAttackRetreatDistance);

            _steering.Register(uid, retreatPoint);
            combat.IsRetreating = true;
            combat.RetreatTimer = combat.PostAttackCooldown;
        }
        else
        {
            // Not time to attack yet - approach the target
            _steering.Register(uid, new EntityCoordinates(target, Vector2.Zero));
        }
    }

    //  HitAndLeap - bite and leap back

    /// <summary>
    /// Wolf tactics - bite and leap back using physics.
    ///
    /// Phases:
    ///   Idle    → approaches through steering, transition to Biting when attack range is reached
    ///   Biting  → attacks with melee, fixes leap vector, immediately → Leaping
    ///   Leaping → physics impulse backward (SetLinearVelocity), steering disabled
    ///   Cooldown→ waits WaitDuration seconds, then returns to Idle
    /// </summary>
    private void ProcessHitAndLeap(
        EntityUid uid,
        NibiruNpcStateMachineComponent state,
        NibiruNpcCombatComponent combat,
        NibiruNpcHitAndRunAttackComponent leap,
        TransformComponent xform,
        EntityUid target,
        TransformComponent targetXform,
        float distance,
        float frameTime)
    {
        switch (leap.Phase)
        {
            //  Idle: move to target through steering
            case LeapPhase.Idle:
            {
                // If target has run too far - return
                if (distance > 15f)
                {
                    ResetToReturning(uid, state, combat);
                    return;
                }

                // Activate combat mode for visibility
                if (TryComp<CombatModeComponent>(uid, out var cm) && !cm.IsInCombatMode)
                    _combat.SetInCombatMode(uid, true, cm);

                if (distance <= leap.AttackRange)
                {
                    // Reached - bite
                    _steering.Unregister(uid);
                    leap.Phase = LeapPhase.Biting;
                }
                else
                {
                    _steering.Register(uid, new EntityCoordinates(target, Vector2.Zero));
                }
                break;
            }

            //  Biting: perform attack, then immediately leap
            case LeapPhase.Biting:
            {
                if (TryComp<CombatModeComponent>(uid, out var cm) && !cm.IsInCombatMode)
                    _combat.SetInCombatMode(uid, true, cm);

                if (_melee.TryGetWeapon(uid, out var wUid, out var w) && _timing.CurTime >= w.NextAttack)
                {
                    _melee.AttemptLightAttack(uid, wUid, w, target);

                    // Leap vector: strictly backward relative to body rotation
                    // (i.e. the inverse vector from target to us, not just "backward on screen")
                    var myWorldPos = _xform.GetWorldPosition(xform);
                    var targetWorldPos = _xform.GetWorldPosition(targetXform);
                    var toTarget = targetWorldPos - myWorldPos;

                    // Direction "backward from target" = normalized vector (me → target), inverted
                    leap.LeapDirection = toTarget.LengthSquared() > 0.01f
                        ? -Vector2.Normalize(toTarget)   // exactly back from target
                        : -Vector2.UnitX;

                    // Calculate the duration of the leap phase from distance and speed
                    leap.Timer = leap.LeapDistance / leap.LeapSpeed;
                    leap.Phase = LeapPhase.Leaping;

                    // Immediately apply impulse
                    if (TryComp<PhysicsComponent>(uid, out var phys))
                        _physics.SetLinearVelocity(uid, leap.LeapDirection * leap.LeapSpeed, body: phys);
                }
                break;
            }

            //  Leaping: flying backward through physics
            case LeapPhase.Leaping:
            {
                leap.Timer -= frameTime;

                // Maintain speed every tick (physics can dissipate it)
                if (TryComp<PhysicsComponent>(uid, out var phys))
                    _physics.SetLinearVelocity(uid, leap.LeapDirection * leap.LeapSpeed, body: phys);

                if (leap.Timer <= 0f)
                {
                    // Landed — brake
                    if (TryComp<PhysicsComponent>(uid, out var physStop))
                        _physics.SetLinearVelocity(uid, Vector2.Zero, body: physStop);

                    leap.Timer = leap.WaitDuration;
                    leap.Phase = LeapPhase.Cooldown;
                }
                break;
            }

            //  Cooldown: wait before next attack
            case LeapPhase.Cooldown:
            {
                leap.Timer -= frameTime;

                if (leap.Timer <= 0f)
                {
                    leap.Phase = LeapPhase.Idle;
                    // Let steering lead to the target again
                    _steering.Register(uid, new EntityCoordinates(target, Vector2.Zero));
                }
                break;
            }
        }
    }

    //  Charge — charge of the horned

    /// <summary>
    /// Called from <see cref="NibiruNpcBehaviorSystem"/> in the Charging state.
    ///
    /// Phases:
    ///   Idle     → waits in ProcessChasing; transition to WindUp through BehaviorSystem
    ///   WindUp   → stops, shakes, waits ShakeDuration
    ///   Charging → physical run in a straight line, damage through collisions
    ///   Cooldown → rests CooldownDuration, then returns to Chasing
    /// </summary>
    public void ProcessCharging(
        EntityUid uid,
        NibiruNpcStateMachineComponent state,
        NibiruNpcCombatComponent combat,
        NibiruNpcChargeAttackComponent charge,
        TransformComponent xform,
        float frameTime)
    {
        switch (charge.Phase)
        {
            //  WindUp: shaking, turning to target
            case ChargePhase.WindUp:
            {
                charge.Timer -= frameTime;

                if (charge.Timer <= 0f)
                {
                    // Remove shaking — charge started
                    RemCompDeferred<JitteringComponent>(uid);

                    charge.Timer = charge.MaxDuration;
                    charge.Phase = ChargePhase.Charging;

                    // Immediately apply impulse
                    if (TryComp<PhysicsComponent>(uid, out var phys))
                        _physics.SetLinearVelocity(uid, charge.Direction * charge.Speed, body: phys);
                }
                break;
            }

            //  Charging: flying in a straight line
            case ChargePhase.Charging:
            {
                charge.Timer -= frameTime;

                // Maintain speed every tick
                if (TryComp<PhysicsComponent>(uid, out var phys))
                    _physics.SetLinearVelocity(uid, charge.Direction * charge.Speed, body: phys);

                if (charge.Timer <= 0f)
                {
                    // Time expired - stop
                    StopCharge(uid, state, charge);
                }
                break;
            }

            //  Cooldown: rest after charge
            case ChargePhase.Cooldown:
            {
                charge.Timer -= frameTime;

                if (charge.Timer <= 0f)
                {
                    charge.Phase = ChargePhase.Idle;
                    state.CurrentState = NibiruNpcState.Chasing;

                    // Enable navigation back to target
                    if (state.CurrentTarget != null)
                        _steering.Register(uid, new EntityCoordinates(state.CurrentTarget.Value, Vector2.Zero));
                }
                break;
            }
        }
    }

    /// <summary>
    /// Initiates WindUp for the charge. Called from BehaviorSystem when the target
    /// enters the required distance range.
    /// </summary>
    public void StartChargeWindUp(
        EntityUid uid,
        NibiruNpcStateMachineComponent state,
        NibiruNpcChargeAttackComponent charge,
        TransformComponent xform,
        EntityUid target)
    {
        if (!TryComp(target, out TransformComponent? targetXform))
            return;

        // Fix the charge direction
        var myPos = _xform.GetWorldPosition(xform);
        var targetPos = _xform.GetWorldPosition(targetXform);
        var dir = targetPos - myPos;
        charge.Direction = dir.LengthSquared() > 0.01f
            ? Vector2.Normalize(dir)
            : Vector2.UnitX;

        // Rotate to face target
        _xform.SetLocalRotation(uid, charge.Direction.ToAngle());

        // Stop and disable navigation
        _steering.Unregister(uid);
        if (TryComp<PhysicsComponent>(uid, out var phys))
            _physics.SetLinearVelocity(uid, Vector2.Zero, body: phys);

        // Clear list of previously hit entities
        charge.HitEntities.Clear();

        // Start shaking
        _jitter.AddJitter(uid, 10f, 40f);

        charge.Timer = charge.ShakeDuration;
        charge.Phase = ChargePhase.WindUp;
        state.CurrentState = NibiruNpcState.Charging;
    }

    /// <summary>
    /// Stops the charge and transitions the animal to Cooldown.
    /// </summary>
    public void StopCharge(
        EntityUid uid,
        NibiruNpcStateMachineComponent state,
        NibiruNpcChargeAttackComponent charge)
    {
        if (TryComp<PhysicsComponent>(uid, out var phys))
            _physics.SetLinearVelocity(uid, Vector2.Zero, body: phys);

        // Remove shaking if it remained
        RemCompDeferred<JitteringComponent>(uid);

        charge.Timer = charge.CooldownDuration;
        charge.Phase = ChargePhase.Cooldown;
        state.CurrentState = NibiruNpcState.Charging;
    }

    private void OnChargeCollide(
        EntityUid uid,
        NibiruNpcChargeAttackComponent charge,
        ref StartCollideEvent args)
    {
        if (charge.Phase != ChargePhase.Charging)
            return;

        var other = args.OtherEntity;
        if (other == uid)
            return;

        //  Collision with a static object (wall, door)
        if (charge.StopOnWallCollision)
        {
            if (TryComp<PhysicsComponent>(other, out var otherPhys) &&
                otherPhys.BodyType == BodyType.Static)
            {
                if (TryComp<NibiruNpcStateMachineComponent>(uid, out var stateMachine))
                    StopCharge(uid, stateMachine, charge);
                return;
            }
        }

        //  Collision with a living entity
        if (!_mobState.IsAlive(other))
            return;

        // Each entity receives damage only once per charge
        if (!charge.HitEntities.Add(other))
            return;

        // Inflict damage
        if (charge.Damage != null)
            _damageable.TryChangeDamage(other, charge.Damage, true, origin: uid);

        // Knockback in the direction of the charge
        var myPos = _xform.GetWorldPosition(uid);
        var otherPos = _xform.GetWorldPosition(other);
        var knockDir = (otherPos - myPos).LengthSquared() > 0.01f
            ? Vector2.Normalize(otherPos - myPos)
            : charge.Direction;

        _throwing.TryThrow(other, knockDir * charge.KnockbackForce, 1f, uid);
    }

    //  Fleeing

    private void ProcessFleeing(
        EntityUid uid,
        NibiruNpcStateMachineComponent state,
        TransformComponent xform)
    {
        if (TryComp<CombatModeComponent>(uid, out var combatMode) && combatMode.IsInCombatMode)
            _combat.SetInCombatMode(uid, false, combatMode);

        if (state.CurrentTarget == null || !Exists(state.CurrentTarget.Value))
        {
            state.CurrentState = NibiruNpcState.Returning;
            state.CurrentTarget = null;
            return;
        }

        var target = state.CurrentTarget.Value;
        if (!TryComp(target, out TransformComponent? targetXform))
        {
            state.CurrentState = NibiruNpcState.Returning;
            state.CurrentTarget = null;
            return;
        }

        if (!xform.Coordinates.TryDistance(EntityManager, targetXform.Coordinates, out var distance))
            return;

        var fleeDistance = 12f;
        if (TryComp<NibiruNpcAggroComponent>(uid, out var aggro))
            fleeDistance = aggro.FleeDistance;

        if (distance > fleeDistance)
        {
            state.CurrentState = NibiruNpcState.Returning;
            state.CurrentTarget = null;
            _steering.Unregister(uid);
            return;
        }

        var myPos = _xform.GetWorldPosition(xform);
        var targetPos = _xform.GetWorldPosition(targetXform);
        var dir = myPos - targetPos;
        if (dir.LengthSquared() > 0.01f)
        {
            dir = Vector2.Normalize(dir);
            var fleeCoords = new EntityCoordinates(xform.ParentUid, xform.LocalPosition + dir * 5f);
            _steering.Register(uid, fleeCoords);
        }
    }

    /// <summary>
    /// Validates that the target still exists, is alive, and is reachable.
    /// </summary>
    private bool ValidateTarget(
        EntityUid uid,
        NibiruNpcStateMachineComponent state,
        out EntityUid target,
        out TransformComponent targetXform)
    {
        target = default;
        targetXform = default!;

        if (state.CurrentTarget == null || !Exists(state.CurrentTarget.Value))
            return false;

        target = state.CurrentTarget.Value;

        if (_mobState.IsIncapacitated(target))
            return false;

        if (!TryComp(target, out TransformComponent? xform) || xform == null)
            return false;

        targetXform = xform;
        return true;
    }

    /// <summary>
    /// Resets combat state and transitions NPC to Returning.
    /// </summary>
    private void ResetToReturning(
        EntityUid uid,
        NibiruNpcStateMachineComponent state,
        NibiruNpcCombatComponent combat)
    {
        state.CurrentTarget = null;
        state.CurrentState = NibiruNpcState.Returning;
        combat.IsRetreating = false;
        combat.RetreatTimer = 0f;

        // Reset HitAndLeap phase if exists
        if (TryComp<NibiruNpcHitAndRunAttackComponent>(uid, out var leap))
        {
            leap.Phase = LeapPhase.Idle;
            leap.Timer = 0f;
            if (TryComp<PhysicsComponent>(uid, out var phys))
                _physics.SetLinearVelocity(uid, Vector2.Zero, body: phys);
        }

        _steering.Unregister(uid);
    }
}
