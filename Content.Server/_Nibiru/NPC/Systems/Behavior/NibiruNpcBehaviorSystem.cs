using System.Numerics;
using Robust.Shared.Maths;
using Content.Server.NPC.Components;
using Content.Server.NPC.Systems;
// Obsolete root using removed
using Content.Shared._Nibiru.NPC.Behavior;
using Content.Shared._Nibiru.NPC.Training;
using Content.Shared._Nibiru.NPC.Commands;
using Content.Shared._Nibiru.NPC.Livestock;
using Content.Shared._Nibiru.NPC.Utility;
using Content.Shared.CombatMode;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.Weapons.Melee;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Content.Server._Nibiru.NPC.Systems.Utility;
using Content.Shared.Tag;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;

namespace Content.Server._Nibiru.NPC.Systems.Behavior;

/// <summary>
/// Основная система поведения NPC Nibiru.
/// Управляет конечным автоматом состояний: Idle, Patrol, Chase, Attack, Flee, Follow, Return.
/// Интегрируется с ванильной системой стиринга для навигации.
/// </summary>
public sealed class NibiruNpcBehaviorSystem : EntitySystem
{
    [Dependency] private readonly NibiruNpcPerceptionSystem _perception = default!;
    [Dependency] private readonly NpcFactionSystem _faction = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly NPCSteeringSystem _steering = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly NibiruAnimalSoundSystem _sounds = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SharedMeleeWeaponSystem _melee = default!;
    [Dependency] private readonly SharedCombatModeSystem _combat = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly Robust.Shared.Map.IMapManager _mapManager = default!;
    [Dependency] private readonly Robust.Shared.Map.ITileDefinitionManager _tileDefManager = default!;
    [Dependency] private readonly Content.Shared.Nutrition.EntitySystems.HungerSystem _hunger = default!;

    private const float ThreatCheckInterval = 0.5f;
    private float _threatCheckAccumulator;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NibiruNpcBehaviorComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<NibiruNpcBehaviorComponent, DamageChangedEvent>(OnDamaged);
        SubscribeLocalEvent<NibiruNpcBehaviorComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
        SubscribeLocalEvent<NibiruNpcBehaviorComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnRefreshSpeed(EntityUid uid, NibiruNpcBehaviorComponent component, RefreshMovementSpeedModifiersEvent args)
    {
        if (component.IsCombatActionActive && component.CombatStyle == NibiruCombatStyle.Charge)
        {
            args.ModifySpeed(2.5f, 2.5f); // Increase speed during charge
        }
    }

    private void OnStartup(EntityUid uid, NibiruNpcBehaviorComponent component, ComponentStartup args)
    {
        var xform = Transform(uid);
        component.HomePosition = xform.Coordinates;
        component.CurrentState = NibiruNpcState.Idle;
    }

    private void OnDamaged(EntityUid uid, NibiruNpcBehaviorComponent component, DamageChangedEvent args)
    {
        if (!args.DamageIncreased)
            return;

        _sounds.PlayHurtSound(uid, component);

        if (args.Origin is not {} attacker)
            return;

        component.HostileMemory[attacker] = _timing.CurTime + TimeSpan.FromSeconds(component.MemoryDuration);

        switch (component.BehaviorType)
        {
            case NibiruNpcBehaviorType.Passive:
            case NibiruNpcBehaviorType.Shy:
                component.CurrentTarget = attacker;
                component.CurrentState = NibiruNpcState.Fleeing;
                break;

            case NibiruNpcBehaviorType.Neutral:
                component.CurrentTarget = attacker;
                component.CurrentState = NibiruNpcState.Chasing;
                break;

            case NibiruNpcBehaviorType.Aggressive:
                if (component.CurrentTarget == null || !EntityManager.EntityExists(component.CurrentTarget.Value))
                {
                    component.CurrentTarget = attacker;
                    component.CurrentState = NibiruNpcState.Chasing;
                }
                break;
        }
    }

    private void OnMobStateChanged(EntityUid uid, NibiruNpcBehaviorComponent component, MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Dead)
        {
            _sounds.PlayDeathSound(uid, component);
            _steering.Unregister(uid);
        }

        if (args.NewMobState is MobState.Dead or MobState.Critical)
        {
            component.CurrentState = NibiruNpcState.Idle;
            component.CurrentTarget = null;
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

        var query = EntityQueryEnumerator<NibiruNpcBehaviorComponent, NibiruNpcPerceptionComponent, ActiveNPCComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var behavior, out var perception, out _, out var xform))
        {
            if (behavior.CombatTimer > 0)
                behavior.CombatTimer -= frameTime;

            if (_mobState.IsIncapacitated(uid))
                continue;

            CleanupMemory(behavior);

            switch (behavior.CurrentState)
            {
                case NibiruNpcState.Idle:
                    ProcessIdle(uid, behavior, perception, xform, frameTime, doThreatCheck);
                    break;
                case NibiruNpcState.Patrolling:
                    ProcessPatrolling(uid, behavior, perception, xform, frameTime, doThreatCheck);
                    break;
                case NibiruNpcState.Chasing:
                    ProcessChasing(uid, behavior, perception, xform);
                    break;
                case NibiruNpcState.Attacking:
                    ProcessAttacking(uid, behavior, xform);
                    break;
                case NibiruNpcState.Fleeing:
                    ProcessFleeing(uid, behavior, perception, xform);
                    break;
                case NibiruNpcState.Following:
                    ProcessFollowing(uid, behavior, xform);
                    break;
                case NibiruNpcState.Returning:
                    ProcessReturning(uid, behavior, xform, frameTime);
                    break;
            }
        }
    }

    /// <summary>
    /// Очищает устаревшие записи из памяти о врагах.
    /// </summary>
    private void CleanupMemory(NibiruNpcBehaviorComponent behavior)
    {
        var curTime = _timing.CurTime;
        var toRemove = new List<EntityUid>();
        foreach (var (entity, expiry) in behavior.HostileMemory)
        {
            if (curTime > expiry || !EntityManager.EntityExists(entity))
                toRemove.Add(entity);
        }

        foreach (var entity in toRemove)
            behavior.HostileMemory.Remove(entity);
    }

    #region State Processors

    private void ProcessIdle(EntityUid uid, NibiruNpcBehaviorComponent behavior,
        NibiruNpcPerceptionComponent perception, TransformComponent xform, float frameTime, bool doThreatCheck)
    {
        if (doThreatCheck)
        {
            var threat = FindThreat(uid, behavior, perception);
            if (threat != null)
            {
                HandleThreatDetected(uid, behavior, threat.Value);
                return;
            }

            // Если голоден — ищем еду
            if (TryComp<Content.Shared.Nutrition.Components.HungerComponent>(uid, out var hunger) &&
                hunger.CurrentThreshold <= Content.Shared.Nutrition.Components.HungerThreshold.Peckish)
            {
                if (TryEatTile(uid, hunger, xform))
                    return;

                var food = FindFood(uid, perception);
                if (food != null)
                {
                    behavior.CurrentTarget = food;
                    behavior.CurrentState = NibiruNpcState.Chasing;
                    return;
                }
            }
        }

        behavior.PatrolAccumulator += frameTime;
        if (behavior.PatrolAccumulator >= behavior.PatrolInterval)
        {
            behavior.PatrolAccumulator = 0f;
            behavior.CurrentState = NibiruNpcState.Patrolling;
        }
    }

    private void ProcessPatrolling(EntityUid uid, NibiruNpcBehaviorComponent behavior,
        NibiruNpcPerceptionComponent perception, TransformComponent xform, float frameTime, bool doThreatCheck)
    {
        if (doThreatCheck)
        {
            var threat = FindThreat(uid, behavior, perception);
            if (threat != null)
            {
                HandleThreatDetected(uid, behavior, threat.Value);
                return;
            }

            // Если голоден — ищем еду
            if (TryComp<Content.Shared.Nutrition.Components.HungerComponent>(uid, out var hunger) &&
                hunger.CurrentThreshold <= Content.Shared.Nutrition.Components.HungerThreshold.Peckish)
            {
                if (TryEatTile(uid, hunger, xform))
                    return;

                var food = FindFood(uid, perception);
                if (food != null)
                {
                    behavior.CurrentTarget = food;
                    behavior.CurrentState = NibiruNpcState.Chasing;
                    return;
                }
            }
        }

        if (behavior.HomePosition != null)
        {
            var homePos = behavior.HomePosition.Value;
            if (homePos.TryDistance(EntityManager, xform.Coordinates, out var dist) && dist > behavior.PatrolRadius * 1.5f)
            {
                behavior.CurrentState = NibiruNpcState.Returning;
                return;
            }
        }

        behavior.PatrolAccumulator += frameTime;
        if (behavior.PatrolAccumulator >= behavior.PatrolInterval)
        {
            behavior.PatrolAccumulator = 0f;
            MoveToRandomPatrolPoint(uid, behavior, xform);
        }
    }

    private void ProcessChasing(EntityUid uid, NibiruNpcBehaviorComponent behavior,
        NibiruNpcPerceptionComponent perception, TransformComponent xform)
    {
        if (behavior.CurrentTarget == null || !EntityManager.EntityExists(behavior.CurrentTarget.Value))
        {
            behavior.CurrentState = NibiruNpcState.Returning;
            behavior.CurrentTarget = null;
            return;
        }

        var target = behavior.CurrentTarget.Value;
        if (_mobState.IsIncapacitated(target))
        {
            behavior.CurrentState = NibiruNpcState.Returning;
            behavior.CurrentTarget = null;
            return;
        }

        if (!TryComp<TransformComponent>(target, out var targetXform))
        {
            behavior.CurrentState = NibiruNpcState.Returning;
            behavior.CurrentTarget = null;
            return;
        }

        if (!xform.Coordinates.TryDistance(EntityManager, targetXform.Coordinates, out var distance))
        {
            behavior.CurrentState = NibiruNpcState.Returning;
            behavior.CurrentTarget = null;
            return;
        }

        if (distance > behavior.DeaggroRange)
        {
            behavior.CurrentState = NibiruNpcState.Returning;
            behavior.CurrentTarget = null;
            behavior.IsCombatActionActive = false;
            _movementSpeed.RefreshMovementSpeedModifiers(uid);
            _steering.Unregister(uid);
            return;
        }

        // Логика разбега (Charge)
        if (behavior.CombatStyle == NibiruCombatStyle.Charge && !behavior.IsCombatActionActive && behavior.CombatTimer <= 0)
        {
            if (distance >= 4f && distance <= 8f)
            {
                behavior.IsCombatActionActive = true;
                _movementSpeed.RefreshMovementSpeedModifiers(uid);
                _popup.PopupEntity(Loc.GetString("nibiru-animal-combat-charge"), uid);
            }
        }

        if (distance <= 1.5f)
        {
            if (behavior.IsCombatActionActive && behavior.CombatStyle == NibiruCombatStyle.Charge)
            {
                behavior.IsCombatActionActive = false;
                behavior.CombatTimer = 5f;
                _movementSpeed.RefreshMovementSpeedModifiers(uid);
            }
            // Если цель — еда, съедаем её
            if (_tag.HasTag(target, "Food"))
            {
                ConsumeFood(uid, target);
                behavior.CurrentState = NibiruNpcState.Idle;
                behavior.CurrentTarget = null;
                return;
            }

            behavior.CurrentState = NibiruNpcState.Attacking;
            return;
        }

        _steering.Register(uid, new EntityCoordinates(target, Vector2.Zero));
    }

    private void ConsumeFood(EntityUid uid, EntityUid food)
    {
        if (TryComp<Content.Shared.Nutrition.Components.HungerComponent>(uid, out var hunger))
        {
            // Упрощенное поедание: восстанавливаем 100 ед. и удаляем еду
            _hunger.ModifyHunger(uid, 100f, hunger);
            _sounds.PlayFeedingSound(uid);
            QueueDel(food);
        }
    }

    private EntityUid? FindFood(EntityUid uid, NibiruNpcPerceptionComponent perception)
    {
        foreach (var detected in perception.DetectedEntities)
        {
            if (_tag.HasTag(detected, "Food"))
                return detected;
        }
        return null;
    }

    private bool TryEatTile(EntityUid uid, Content.Shared.Nutrition.Components.HungerComponent hunger, TransformComponent xform)
    {
        // Проверяем, является ли животное травоядным
        if (!TryComp<NibiruTamableComponent>(uid, out var tamable) || tamable.Diet != NibiruAnimalDiet.Herbivore)
            return false;

        if (xform.GridUid == null)
            return false;

        if (!TryComp<Robust.Shared.Map.Components.MapGridComponent>(xform.GridUid, out var grid))
            return false;

        var centerTileRef = grid.GetTileRef(xform.Coordinates);
        var centerIndices = centerTileRef.GridIndices;

        // Ищем траву в области 3х3
        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                var checkIndices = new Vector2i(centerIndices.X + x, centerIndices.Y + y);
                var tileRef = grid.GetTileRef(checkIndices);
                var tileDef = (Content.Shared.Maps.ContentTileDefinition)_tileDefManager[tileRef.Tile.TypeId];

                if (tileDef.Name.ToLower().Contains("grass") || tileDef.ID.ToLower().Contains("grass") || tileDef.ID.ToLower().Contains("jungle"))
                {
                    if (!string.IsNullOrEmpty(tileDef.BaseTurf))
                    {
                        var baseTileDef = _tileDefManager[tileDef.BaseTurf];
                        grid.SetTile(checkIndices, new Robust.Shared.Map.Tile(baseTileDef.TileId));

                        _hunger.ModifyHunger(uid, 50f, hunger);
                        _sounds.PlayFeedingSound(uid);
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private void ProcessAttacking(EntityUid uid, NibiruNpcBehaviorComponent behavior, TransformComponent xform)
    {
        if (behavior.CurrentTarget == null || !EntityManager.EntityExists(behavior.CurrentTarget.Value))
        {
            behavior.CurrentState = NibiruNpcState.Returning;
            behavior.CurrentTarget = null;
            return;
        }

        var target = behavior.CurrentTarget.Value;
        if (_mobState.IsIncapacitated(target))
        {
            behavior.CurrentState = NibiruNpcState.Returning;
            behavior.CurrentTarget = null;
            return;
        }

        if (!TryComp<TransformComponent>(target, out var targetXform))
        {
            behavior.CurrentState = NibiruNpcState.Returning;
            behavior.CurrentTarget = null;
            return;
        }

        if (!xform.Coordinates.TryDistance(EntityManager, targetXform.Coordinates, out var distance))
        {
            behavior.CurrentState = NibiruNpcState.Returning;
            behavior.CurrentTarget = null;
            return;
        }

        if (distance > 2f)
        {
            behavior.CurrentState = NibiruNpcState.Chasing;
            return;
        }

        if (TryComp<CombatModeComponent>(uid, out var combatMode) && !combatMode.IsInCombatMode)
            _combat.SetInCombatMode(uid, true, combatMode);

        if (_melee.TryGetWeapon(uid, out var weaponUid, out var weapon))
        {
            if (_timing.CurTime >= weapon.NextAttack)
            {
                if (_melee.AttemptLightAttack(uid, weaponUid, weapon, target))
                {
                    // Логика Hit and Run
                    if (behavior.CombatStyle == NibiruCombatStyle.HitAndRun)
                    {
                        behavior.IsCombatActionActive = true;
                        behavior.CombatTimer = 1.2f;
                        behavior.CurrentState = NibiruNpcState.Fleeing;
                        return;
                    }
                }
            }
        }

        _steering.Register(uid, new EntityCoordinates(target, Vector2.Zero));
    }

    private void ProcessFleeing(EntityUid uid, NibiruNpcBehaviorComponent behavior,
        NibiruNpcPerceptionComponent perception, TransformComponent xform)
    {
        if (TryComp<CombatModeComponent>(uid, out var combatMode) && combatMode.IsInCombatMode)
            _combat.SetInCombatMode(uid, false, combatMode);

        // Если это тактический отскок
        if (behavior.IsCombatActionActive && behavior.CombatStyle == NibiruCombatStyle.HitAndRun)
        {
            if (behavior.CombatTimer <= 0)
            {
                behavior.IsCombatActionActive = false;
                behavior.CurrentState = NibiruNpcState.Chasing;
                return;
            }
        }

        if (behavior.CurrentTarget == null || !EntityManager.EntityExists(behavior.CurrentTarget.Value))
        {
            var myPos = _xform.GetWorldPosition(xform);
            var randomOffset = _random.NextVector2(behavior.FleeDistance);
            var fleeTarget = myPos + randomOffset;

            var fleeCoords = new EntityCoordinates(
                xform.ParentUid,
                Vector2.Transform(fleeTarget, _xform.GetInvWorldMatrix(xform.ParentUid)));

            _steering.Register(uid, fleeCoords);

            behavior.PatrolAccumulator += 1f;
            if (behavior.PatrolAccumulator > 5f)
            {
                behavior.PatrolAccumulator = 0f;
                behavior.CurrentState = NibiruNpcState.Returning;
                behavior.CurrentTarget = null;
            }
            return;
        }

        var target = behavior.CurrentTarget.Value;

        if (!TryComp<TransformComponent>(target, out var targetXform))
        {
            behavior.CurrentState = NibiruNpcState.Idle;
            behavior.CurrentTarget = null;
            return;
        }

        if (!xform.Coordinates.TryDistance(EntityManager, targetXform.Coordinates, out var distance))
        {
            behavior.CurrentState = NibiruNpcState.Idle;
            behavior.CurrentTarget = null;
            return;
        }

        if (distance > behavior.FleeDistance)
        {
            behavior.CurrentState = NibiruNpcState.Idle;
            behavior.CurrentTarget = null;
            _steering.Unregister(uid);
            return;
        }

        var currentPos = _xform.GetWorldPosition(xform);
        var threatPos = _xform.GetWorldPosition(targetXform);
        var fleeDir = currentPos - threatPos;

        if (fleeDir.LengthSquared() > 0.01f)
        {
            // Убегаем в противоположную сторону от угрозы
            fleeDir = Vector2.Normalize(fleeDir) * behavior.FleeDistance;
            var fleeTarget = currentPos + fleeDir;

            var fleeCoords = new EntityCoordinates(
                xform.ParentUid,
                Vector2.Transform(fleeTarget, _xform.GetInvWorldMatrix(xform.ParentUid)));

            _steering.Register(uid, fleeCoords);
        }
    }

    private void ProcessFollowing(EntityUid uid, NibiruNpcBehaviorComponent behavior, TransformComponent xform)
    {
        if (behavior.CurrentTarget == null || !EntityManager.EntityExists(behavior.CurrentTarget.Value))
        {
            behavior.CurrentState = NibiruNpcState.Idle;
            behavior.CurrentTarget = null;
            return;
        }

        var target = behavior.CurrentTarget.Value;

        if (!TryComp<TransformComponent>(target, out var targetXform))
        {
            behavior.CurrentState = NibiruNpcState.Idle;
            behavior.CurrentTarget = null;
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

    private void ProcessReturning(EntityUid uid, NibiruNpcBehaviorComponent behavior, TransformComponent xform, float frameTime)
    {
        if (behavior.HomePosition == null)
        {
            behavior.CurrentState = NibiruNpcState.Idle;
            return;
        }

        if (behavior.HomePosition.Value.TryDistance(EntityManager, xform.Coordinates, out var dist) && dist < 2f)
        {
            behavior.PatrolAccumulator = 0f;
            behavior.CurrentState = NibiruNpcState.Idle;
            _steering.Unregister(uid);
            return;
        }

        behavior.PatrolAccumulator += frameTime;
        if (behavior.PatrolAccumulator > 30f) // Если 30 секунд не может вернуться, делает текущее место новым домом
        {
            behavior.PatrolAccumulator = 0f;
            behavior.HomePosition = xform.Coordinates;
            behavior.CurrentState = NibiruNpcState.Idle;
            _steering.Unregister(uid);
            return;
        }

        _steering.Register(uid, behavior.HomePosition.Value);
    }

    #endregion

    #region Threat Detection

    /// <summary>
    /// Ищет ближайшую угрозу среди обнаруженных сенсорикой сущностей.
    /// </summary>
    private EntityUid? FindThreat(EntityUid uid, NibiruNpcBehaviorComponent behavior, NibiruNpcPerceptionComponent perception)
    {
        EntityUid? closest = null;
        float closestDist = float.MaxValue;

        if (!TryComp<TransformComponent>(uid, out var myXform))
            return null;

        foreach (var detected in perception.DetectedEntities)
        {
            if (!EntityManager.EntityExists(detected))
                continue;

            if (!_faction.IsEntityFriendly(uid, detected) ||
                behavior.HostileMemory.ContainsKey(detected))
            {
                if (TryComp<NibiruTamableComponent>(uid, out var tamable) &&
                    tamable.IsTamed && tamable.OwnerUid == detected)
                    continue;

                if (!TryComp<TransformComponent>(detected, out var targetXform))
                    continue;

                if (!myXform.Coordinates.TryDistance(EntityManager, targetXform.Coordinates, out var dist))
                    continue;

                if (behavior.BehaviorType == NibiruNpcBehaviorType.Aggressive && dist > behavior.AggroRange)
                    continue;

                if (behavior.BehaviorType == NibiruNpcBehaviorType.Passive)
                {
                    if (!behavior.HostileMemory.ContainsKey(detected))
                        continue;
                    if (dist > behavior.FleeRange)
                        continue;
                }

                if (behavior.BehaviorType == NibiruNpcBehaviorType.Shy && dist > behavior.FleeRange)
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

    /// <summary>
    /// Обрабатывает обнаружение угрозы в зависимости от типа NPC.
    /// </summary>
    private void HandleThreatDetected(EntityUid uid, NibiruNpcBehaviorComponent behavior, EntityUid threat)
    {
        behavior.CurrentTarget = threat;

        switch (behavior.BehaviorType)
        {
            case NibiruNpcBehaviorType.Aggressive:
                behavior.CurrentState = NibiruNpcState.Chasing;
                break;
            case NibiruNpcBehaviorType.Neutral:
                if (behavior.HostileMemory.ContainsKey(threat))
                    behavior.CurrentState = NibiruNpcState.Chasing;
                break;
            case NibiruNpcBehaviorType.Passive:
            case NibiruNpcBehaviorType.Shy:
                behavior.CurrentState = NibiruNpcState.Fleeing;
                break;
        }
    }

    #endregion

    #region Navigation Helpers

    private void MoveToRandomPatrolPoint(EntityUid uid, NibiruNpcBehaviorComponent behavior, TransformComponent xform)
    {
        if (behavior.HomePosition == null)
            return;

        if (!EntityManager.EntityExists(behavior.HomePosition.Value.EntityId))
        {
            behavior.HomePosition = xform.Coordinates;
            return;
        }

        var homePos = _xform.GetWorldPosition(behavior.HomePosition.Value.EntityId);
        var offset = _random.NextVector2(behavior.PatrolRadius);
        var patrolTarget = homePos + offset;

        var patrolCoords = new EntityCoordinates(
            xform.ParentUid,
            Vector2.Transform(patrolTarget, _xform.GetInvWorldMatrix(xform.ParentUid)));

        _steering.Register(uid, patrolCoords);
    }

    #endregion
}
