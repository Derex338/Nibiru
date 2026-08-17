using Content.Server.Jittering;
using Content.Server.NPC.Systems;
using Content.Shared._Nibiru.NPC.Behavior;
using Content.Shared._Nibiru.NPC.Behavior.Components;
using Content.Shared._Nibiru.NPC.Training;
using Content.Shared.CombatMode;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Jittering;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Systems;
using Content.Shared.Weapons.Melee;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;
using System.Numerics;
using Content.Server._Nibiru.NPC.Systems.Utility;
using Content.Server._Nibiru.NPC.Systems.Commands;
using Content.Shared.Tag;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Chemistry.EntitySystems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Nibiru.NPC.Systems.Behavior;

public sealed partial class NibiruNpcBehaviorSystem : EntitySystem
{
    private static readonly ProtoId<TagPrototype> FoodTag = "Food";

    [Dependency] private NibiruNpcPerceptionSystem _perception = default!;
    [Dependency] private NpcFactionSystem _faction = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private NPCSteeringSystem _steering = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private NibiruNpcCombatSystem _combatSystem = default!;
    [Dependency] private Content.Shared.Nutrition.EntitySystems.HungerSystem _hunger = default!;
    [Dependency] private SharedSolutionContainerSystem _solutionContainer = default!;
    [Dependency] private SharedMapSystem _mapSystem = default!;
    [Dependency] private NibiruAnimalGrabSystem _grabSystem = default!;
    [Dependency] private NibiruAnimalSoundSystem _sounds = default!;
    [Dependency] private Robust.Shared.Map.ITileDefinitionManager _tileDefManager = default!;
    [Dependency] private Content.Shared.Tag.TagSystem _tag = default!;
    [Dependency] private MovementSpeedModifierSystem _movementSpeed = default!;

    private const float ThreatCheckInterval = 0.1f;
    private float _threatCheckAccumulator;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NibiruNpcStateMachineComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<NibiruNpcStateMachineComponent, DamageChangedEvent>(OnDamaged);
        SubscribeLocalEvent<NibiruNpcStateMachineComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
        SubscribeLocalEvent<NibiruNpcStateMachineComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnRefreshSpeed(EntityUid uid, NibiruNpcStateMachineComponent state, RefreshMovementSpeedModifiersEvent args)
    {
        if (state.CurrentState == NibiruNpcState.Charging &&
            TryComp<NibiruNpcChargeAttackComponent>(uid, out var charge) &&
            charge.Phase == ChargePhase.Charging)
        {
            args.ModifySpeed(2.5f, 2.5f);
        }
    }

    private void OnStartup(EntityUid uid, NibiruNpcStateMachineComponent state, ComponentStartup args)
    {
        var xform = Transform(uid);
        state.HomePosition = xform.Coordinates;
        state.CurrentState = NibiruNpcState.Idle;
    }

    private void OnDamaged(EntityUid uid, NibiruNpcStateMachineComponent state, DamageChangedEvent args)
    {
        if (!args.DamageIncreased)
            return;

        if (TryComp<NibiruNpcAudioComponent>(uid, out var audio))
            _sounds.PlayHurtSound(uid, audio);

        if (args.Origin is not {} attacker)
            return;

        if (TryComp<NibiruNpcMemoryComponent>(uid, out var memory))
        {
            memory.HostileMemory[attacker] = _timing.CurTime + TimeSpan.FromSeconds(memory.MemoryDuration);
        }

        switch (state.BehaviorType)
        {
            case NibiruNpcBehaviorType.Passive:
            case NibiruNpcBehaviorType.Shy:
                state.CurrentTarget = attacker;
                state.CurrentState = NibiruNpcState.Fleeing;
                break;

            case NibiruNpcBehaviorType.Neutral:
                state.CurrentTarget = attacker;
                state.CurrentState = NibiruNpcState.Chasing;
                break;

            case NibiruNpcBehaviorType.Aggressive:
                if (state.CurrentTarget == null || !Exists(state.CurrentTarget.Value))
                {
                    state.CurrentTarget = attacker;
                    state.CurrentState = NibiruNpcState.Chasing;
                }
                break;
        }
    }

    private void OnMobStateChanged(EntityUid uid, NibiruNpcStateMachineComponent state, MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Dead)
        {
            if (TryComp<NibiruNpcAudioComponent>(uid, out var audio))
                _sounds.PlayDeathSound(uid, audio);
            _steering.Unregister(uid);
        }

        if (args.NewMobState is MobState.Dead or MobState.Critical)
        {
            state.CurrentState = NibiruNpcState.Idle;
            state.CurrentTarget = null;
            _steering.Unregister(uid);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _threatCheckAccumulator += frameTime;
        var doThreatCheck = _threatCheckAccumulator >= ThreatCheckInterval;
        if (doThreatCheck)
            _threatCheckAccumulator = 0f;

        var query = EntityQueryEnumerator<NibiruNpcStateMachineComponent, NibiruNpcPerceptionComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var state, out var perception, out var xform))
        {
            if (_mobState.IsIncapacitated(uid))
                continue;

            TryComp<NibiruNpcCombatComponent>(uid, out var combat);

            if (TryComp<NibiruNpcMemoryComponent>(uid, out var memory))
            {
                CleanupMemory(memory);
            }

            switch (state.CurrentState)
            {
                case NibiruNpcState.Idle:
                    ProcessIdle(uid, state, perception, xform, frameTime, doThreatCheck);
                    break;
                case NibiruNpcState.Patrolling:
                    ProcessPatrolling(uid, state, perception, xform, frameTime, doThreatCheck);
                    break;
                case NibiruNpcState.Hungry:
                    ProcessHungry(uid, state, perception, xform, frameTime);
                    break;
                case NibiruNpcState.Chasing:
                    ProcessChasing(uid, state, perception, xform);
                    break;
                case NibiruNpcState.Charging:
                    if (combat != null && TryComp<NibiruNpcChargeAttackComponent>(uid, out var chargeComp))
                        _combatSystem.ProcessCharging(uid, state, combat, chargeComp, xform, frameTime);
                    break;
                case NibiruNpcState.Attacking:
                case NibiruNpcState.Fleeing:
                    if (combat != null)
                        _combatSystem.ProcessCombat(uid, state, combat, xform, frameTime);
                    break;
                case NibiruNpcState.Following:
                    ProcessFollowing(uid, state, xform);
                    break;
                case NibiruNpcState.Returning:
                    ProcessReturning(uid, state, xform, frameTime);
                    break;
            }
        }
    }

    private void CleanupMemory(NibiruNpcMemoryComponent memory)
    {
        var curTime = _timing.CurTime;
        var toRemove = new List<EntityUid>();
        foreach (var (entity, expiry) in memory.HostileMemory)
        {
            if (curTime > expiry || !Exists(entity))
                toRemove.Add(entity);
        }

        foreach (var entity in toRemove)
            memory.HostileMemory.Remove(entity);
    }

    private void ProcessIdle(EntityUid uid, NibiruNpcStateMachineComponent state,
        NibiruNpcPerceptionComponent perception, TransformComponent xform, float frameTime, bool doThreatCheck)
    {
        if (doThreatCheck)
        {
            var threat = FindThreat(uid, state, perception);
            if (threat != null)
            {
                HandleThreatDetected(uid, state, threat.Value);
                return;
            }

            if (TryComp<Content.Shared.Nutrition.Components.HungerComponent>(uid, out var hunger) &&
                hunger.CurrentThreshold <= Content.Shared.Nutrition.Components.HungerThreshold.Peckish)
            {
                state.CurrentState = NibiruNpcState.Hungry;
                return;
            }
        }

        if (TryComp<NibiruNpcPatrolComponent>(uid, out var patrol))
        {
            patrol.PatrolAccumulator += frameTime;
            if (patrol.PatrolAccumulator >= patrol.PatrolInterval)
            {
                patrol.PatrolAccumulator = 0f;
                state.CurrentState = NibiruNpcState.Patrolling;
            }
        }
    }

    private void ProcessPatrolling(EntityUid uid, NibiruNpcStateMachineComponent state,
        NibiruNpcPerceptionComponent perception, TransformComponent xform, float frameTime, bool doThreatCheck)
    {
        if (doThreatCheck)
        {
            var threat = FindThreat(uid, state, perception);
            if (threat != null)
            {
                HandleThreatDetected(uid, state, threat.Value);
                return;
            }

            if (TryComp<Content.Shared.Nutrition.Components.HungerComponent>(uid, out var hunger) &&
                hunger.CurrentThreshold <= Content.Shared.Nutrition.Components.HungerThreshold.Peckish)
            {
                state.CurrentState = NibiruNpcState.Hungry;
                return;
            }
        }

        if (TryComp<NibiruNpcPatrolComponent>(uid, out var patrol))
        {
            if (state.HomePosition != null)
            {
                var homePos = state.HomePosition.Value;
                if (homePos.TryDistance(EntityManager, xform.Coordinates, out var dist) && dist > patrol.PatrolRadius * 1.5f)
                {
                    state.CurrentState = NibiruNpcState.Returning;
                    return;
                }
            }

            patrol.PatrolAccumulator += frameTime;
            if (patrol.PatrolAccumulator >= patrol.PatrolInterval)
            {
                patrol.PatrolAccumulator = 0f;
                MoveToRandomPatrolPoint(uid, state, patrol, xform);
            }
        }
    }

    private void ProcessChasing(EntityUid uid, NibiruNpcStateMachineComponent state,
        NibiruNpcPerceptionComponent perception, TransformComponent xform)
    {
        if (state.CurrentTarget == null || !Exists(state.CurrentTarget.Value))
        {
            state.CurrentState = NibiruNpcState.Returning;
            state.CurrentTarget = null;
            return;
        }

        var target = state.CurrentTarget.Value;
        if (_mobState.IsIncapacitated(target))
        {
            state.CurrentState = NibiruNpcState.Returning;
            state.CurrentTarget = null;
            return;
        }

        if (!TryComp(target, out TransformComponent? targetXform))
        {
            state.CurrentState = NibiruNpcState.Returning;
            state.CurrentTarget = null;
            return;
        }

        if (!xform.Coordinates.TryDistance(EntityManager, targetXform.Coordinates, out var distance))
        {
            state.CurrentState = NibiruNpcState.Returning;
            state.CurrentTarget = null;
            return;
        }

        var deaggroRange = 15f;
        if (TryComp<NibiruNpcAggroComponent>(uid, out var aggro))
        {
            deaggroRange = aggro.DeaggroRange;
        }

        if (distance > deaggroRange)
        {
            state.CurrentState = NibiruNpcState.Returning;
            state.CurrentTarget = null;
            _movementSpeed.RefreshMovementSpeedModifiers(uid);
            _steering.Unregister(uid);
            return;
        }

        if (TryComp<NibiruNpcCombatComponent>(uid, out var combatStyleComp) &&
            combatStyleComp.CombatStyle == NibiruCombatStyle.Charge &&
            TryComp<NibiruNpcChargeAttackComponent>(uid, out var chargeComp) &&
            chargeComp.Phase == ChargePhase.Idle)
        {
            if (distance >= chargeComp.MinChargeDistance && distance <= chargeComp.MaxChargeDistance)
            {
                _combatSystem.StartChargeWindUp(uid, state, chargeComp, xform, target);
                return;
            }
        }

        if (state.CurrentCommand == NibiruAnimalCommand.Grab && distance <= 1.5f)
        {
            DamageSpecifier? biteDamage = null;
            if (TryComp<MeleeWeaponComponent>(uid, out var melee) && melee.Damage != null)
            {
                biteDamage = melee.Damage;
            }

            if (_grabSystem.TryGrabTarget(uid, target, biteDamage))
            {
                state.CurrentState = NibiruNpcState.Following;
                state.CurrentTarget = target;
                _steering.Unregister(uid);
                return;
            }
        }

        if (distance <= 1.5f)
        {
            state.CurrentState = NibiruNpcState.Attacking;
            return;
        }

        _steering.Register(uid, new EntityCoordinates(target, Vector2.Zero));
    }

    private void ProcessHungry(EntityUid uid, NibiruNpcStateMachineComponent state,
        NibiruNpcPerceptionComponent perception, TransformComponent xform, float frameTime)
    {
        if (!TryComp<Content.Shared.Nutrition.Components.HungerComponent>(uid, out var hunger) ||
            hunger.CurrentThreshold > Content.Shared.Nutrition.Components.HungerThreshold.Peckish)
        {
            state.CurrentState = NibiruNpcState.Idle;
            return;
        }

        // 1. Try to eat the tile
        if (TryEatTile(uid, hunger, xform))
        {
            state.CurrentState = NibiruNpcState.Idle;
            return;
        }

        // 2. Search for food
        var food = FindFood(uid, perception);
        if (food != null)
        {
            state.CurrentTarget = food;

            if (!TryComp(food.Value, out TransformComponent? foodXform))
            {
                state.CurrentTarget = null;
                return;
            }

            if (xform.Coordinates.TryDistance(EntityManager, foodXform.Coordinates, out var dist) && dist <= 1.5f)
            {
                ConsumeFood(uid, food.Value);
                state.CurrentState = NibiruNpcState.Idle;
                state.CurrentTarget = null;
                _steering.Unregister(uid);
                return;
            }

            _steering.Register(uid, new EntityCoordinates(food.Value, Vector2.Zero));
            return;
        }

        state.CurrentState = NibiruNpcState.Patrolling;
    }

    private void ConsumeFood(EntityUid uid, EntityUid food)
    {
        if (!TryComp<Content.Shared.Nutrition.Components.HungerComponent>(uid, out var hunger))
            return;

        float nutritionAmount = 100f;

        if (TryComp<Content.Shared.Nutrition.Components.EdibleComponent>(food, out var edible))
        {
            if (_solutionContainer.TryGetSolution(food, edible.Solution, out _, out var solution))
            {
                nutritionAmount = (float) solution.Volume * 10f;
            }
        }

        _hunger.ModifyHunger(uid, nutritionAmount, hunger);
        _sounds.PlayFeedingSound(uid);
        QueueDel(food);
    }

    private EntityUid? FindFood(EntityUid uid, NibiruNpcPerceptionComponent perception)
    {
        foreach (var detected in perception.DetectedEntities)
        {
            if (_tag.HasTag(detected, FoodTag) || HasComp<Content.Shared.Nutrition.Components.EdibleComponent>(detected))
                return detected;
        }
        return null;
    }

    private bool TryEatTile(EntityUid uid, Content.Shared.Nutrition.Components.HungerComponent hunger, TransformComponent xform)
    {
        if (!TryComp<NibiruNpcEatingComponent>(uid, out var eating))
            return false;

        if (!TryComp<NibiruTamableComponent>(uid, out var tamable) || tamable.Diet != NibiruAnimalDiet.Herbivore)
            return false;

        if (xform.GridUid == null)
            return false;

        if (!TryComp<Robust.Shared.Map.Components.MapGridComponent>(xform.GridUid, out var grid))
            return false;

        var centerTileRef = _mapSystem.GetTileRef((xform.GridUid.Value, grid), xform.Coordinates);
        var centerIndices = centerTileRef.GridIndices;

        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                var checkIndices = new Vector2i(centerIndices.X + x, centerIndices.Y + y);
                var tileRef = _mapSystem.GetTileRef((xform.GridUid.Value, grid), checkIndices);
                var tileDef = (Content.Shared.Maps.ContentTileDefinition)_tileDefManager[tileRef.Tile.TypeId];

                bool isEdible = false;
                foreach (var edibleType in eating.EdibleTiles)
                {
                    if (tileDef.Name.Contains(edibleType, StringComparison.OrdinalIgnoreCase) ||
                        tileDef.ID.Contains(edibleType, StringComparison.OrdinalIgnoreCase))
                    {
                        isEdible = true;
                        break;
                    }
                }

                if (isEdible)
                {
                    if (!string.IsNullOrEmpty(tileDef.BaseTurf))
                    {
                        var baseTileDef = _tileDefManager[tileDef.BaseTurf];
                        _mapSystem.SetTile((xform.GridUid.Value, grid), checkIndices, new Robust.Shared.Map.Tile(baseTileDef.TileId));

                        _hunger.ModifyHunger(uid, 50f, hunger);
                        _sounds.PlayFeedingSound(uid);
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private void ProcessFollowing(EntityUid uid, NibiruNpcStateMachineComponent state, TransformComponent xform)
    {
        if (state.CurrentTarget == null || !Exists(state.CurrentTarget.Value))
        {
            state.CurrentState = NibiruNpcState.Idle;
            state.CurrentTarget = null;
            return;
        }

        var target = state.CurrentTarget.Value;

        if (!TryComp(target, out TransformComponent? targetXform))
        {
            state.CurrentState = NibiruNpcState.Idle;
            state.CurrentTarget = null;
            return;
        }

        if (!xform.Coordinates.TryDistance(EntityManager, targetXform.Coordinates, out var distance))
            return;

        if (distance < 2f)
        {
            _steering.Unregister(uid);
            return;
        }

        _steering.Register(uid, new EntityCoordinates(target, Vector2.Zero));
    }

    private void ProcessReturning(EntityUid uid, NibiruNpcStateMachineComponent state, TransformComponent xform, float frameTime)
    {
        if (state.HomePosition == null)
        {
            state.CurrentState = NibiruNpcState.Idle;
            return;
        }

        if (state.HomePosition.Value.TryDistance(EntityManager, xform.Coordinates, out var dist) && dist < 2f)
        {
            if (TryComp<NibiruNpcPatrolComponent>(uid, out var patrol))
                patrol.PatrolAccumulator = 0f;
            state.CurrentState = NibiruNpcState.Idle;
            _steering.Unregister(uid);
            return;
        }

        if (TryComp<NibiruNpcPatrolComponent>(uid, out var patrolComp))
        {
            patrolComp.PatrolAccumulator += frameTime;
            if (patrolComp.PatrolAccumulator > 30f)
            {
                patrolComp.PatrolAccumulator = 0f;
                state.HomePosition = xform.Coordinates;
                state.CurrentState = NibiruNpcState.Idle;
                _steering.Unregister(uid);
                return;
            }
        }

        _steering.Register(uid, state.HomePosition.Value);
    }

    private EntityUid? FindThreat(EntityUid uid, NibiruNpcStateMachineComponent state, NibiruNpcPerceptionComponent perception)
    {
        EntityUid? closest = null;
        float closestDist = float.MaxValue;

        if (!TryComp(uid, out TransformComponent? myXform))
            return null;

        var aggroRange = 8f;
        var fleeRange = 6f;
        TryComp<NibiruNpcAggroComponent>(uid, out var aggro);
        if (aggro != null)
        {
            aggroRange = aggro.AggroRange;
            fleeRange = aggro.FleeRange;
        }

        TryComp<NibiruNpcMemoryComponent>(uid, out var memory);

        foreach (var detected in perception.DetectedEntities)
        {
            if (!Exists(detected))
                continue;

            if (!_faction.IsEntityFriendly(uid, detected) ||
                (memory != null && memory.HostileMemory.ContainsKey(detected)))
            {
                if (TryComp<NibiruTamableComponent>(uid, out var tamable) &&
                    tamable.IsTamed && tamable.OwnerUid == detected)
                    continue;

                if (!TryComp(detected, out TransformComponent? targetXform))
                    continue;

                if (!myXform.Coordinates.TryDistance(EntityManager, targetXform.Coordinates, out var dist))
                    continue;

                if (state.BehaviorType == NibiruNpcBehaviorType.Aggressive && dist > aggroRange)
                    continue;

                if (state.BehaviorType == NibiruNpcBehaviorType.Passive)
                {
                    if (memory == null || !memory.HostileMemory.ContainsKey(detected))
                        continue;
                    if (dist > fleeRange)
                        continue;
                }

                if (state.BehaviorType == NibiruNpcBehaviorType.Shy && dist > fleeRange)
                    continue;

                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = detected;
                }
            }
        }

        return closest;
    }

    private void HandleThreatDetected(EntityUid uid, NibiruNpcStateMachineComponent state, EntityUid threat)
    {
        state.CurrentTarget = threat;

        switch (state.BehaviorType)
        {
            case NibiruNpcBehaviorType.Aggressive:
                state.CurrentState = NibiruNpcState.Chasing;
                break;
            case NibiruNpcBehaviorType.Neutral:
                if (TryComp<NibiruNpcMemoryComponent>(uid, out var memory) && memory.HostileMemory.ContainsKey(threat))
                    state.CurrentState = NibiruNpcState.Chasing;
                break;
            case NibiruNpcBehaviorType.Passive:
            case NibiruNpcBehaviorType.Shy:
                state.CurrentState = NibiruNpcState.Fleeing;
                break;
        }
    }

    private void MoveToRandomPatrolPoint(EntityUid uid, NibiruNpcStateMachineComponent state, NibiruNpcPatrolComponent patrol, TransformComponent xform)
    {
        if (state.HomePosition == null)
            return;

        if (!Exists(state.HomePosition.Value.EntityId))
        {
            state.HomePosition = xform.Coordinates;
            return;
        }

        var homePos = _xform.GetWorldPosition(state.HomePosition.Value.EntityId);
        var offset = _random.NextVector2(patrol.PatrolRadius);
        var patrolTarget = homePos + offset;

        var patrolCoords = new EntityCoordinates(
            xform.ParentUid,
            Vector2.Transform(patrolTarget, _xform.GetInvWorldMatrix(xform.ParentUid)));

        _steering.Register(uid, patrolCoords);
    }
}
