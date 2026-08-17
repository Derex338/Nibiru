using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Content.Shared._Nibiru.ZLevelRanged.Components;
using Content.Shared.Physics;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using System.Numerics;

namespace Content.Shared._Nibiru.ZLevelRanged.Systems;

/// <summary>
/// Manages projectiles capable of moving between Z-levels.
/// When reaching 70% of the path, checks for the presence of a tile below and teleports the projectile to the level below if it's missing.
/// </summary>
public sealed partial class ZLevelProjectileSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private CESharedZLevelsSystem _zLevels = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private IMapManager _mapManager = default!;

    private EntityQuery<PhysicsComponent> _physicsQuery;
    private EntityQuery<TransformComponent> _xformQuery;
    private EntityQuery<ProjectileComponent> _projectileQuery;
    private EntityQuery<CEZLevelMapComponent> _zMapQuery;
    private EntityQuery<MapGridComponent> _gridQuery;

    public override void Initialize()
    {
        base.Initialize();

        _physicsQuery = GetEntityQuery<PhysicsComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>();
        _projectileQuery = GetEntityQuery<ProjectileComponent>();
        _zMapQuery = GetEntityQuery<CEZLevelMapComponent>();
        _gridQuery = GetEntityQuery<MapGridComponent>();

        SubscribeLocalEvent<ZLevelCapableWeaponComponent, AmmoShotEvent>(OnAmmoShot);
    }

    private void OnAmmoShot(EntityUid uid, ZLevelCapableWeaponComponent component, AmmoShotEvent args)
    {
        // Skip lobbed shots - they are handled separately
        if (args.Lobbed)
            return;

        // Check if the weapon has ZLevelCapable component
        if (!TryComp<ZLevelCapableWeaponComponent>(uid, out var zLevelWeapon))
            return;

        // Add component to all fired projectiles
        foreach (var projectile in args.FiredProjectiles)
        {
            if (!_projectileQuery.HasComp(projectile))
                continue;

            if (!_physicsQuery.TryComp(projectile, out var physics))
                continue;

            if (!_xformQuery.TryComp(projectile, out var xform))
                continue;

            var comp = EnsureComp<ZLevelProjectileComponent>(projectile);
            comp.StartPosition = _transform.GetWorldPosition(xform);
            comp.InitialSpeed = physics.LinearVelocity.Length();
            comp.FallCheckDistance = zLevelWeapon.FallCheckDistance;
            comp.DirectFire = zLevelWeapon.AllowDirectFire;
            comp.OriginalMapId = xform.MapID;
            comp.FallChecked = false;
            comp.TimeAlive = 0f;

            // Calculate estimated flight time (usually 1-3 seconds for projectiles)
            // This is used to determine when to check for falling
            comp.EstimatedFlightTime = comp.InitialSpeed > 0 ? 20f / comp.InitialSpeed : 1.5f; // 20 meters - average range
            comp.EstimatedFlightTime = Math.Clamp(comp.EstimatedFlightTime, 0.5f, 3.0f);

            Dirty(projectile, comp);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ZLevelProjectileComponent, TransformComponent, PhysicsComponent, ProjectileComponent>();
        while (query.MoveNext(out var uid, out var zLevel, out var xform, out var physics, out var projectile))
        {
            zLevel.TimeAlive += frameTime;

            if (zLevel.FallChecked)
                continue;

            if (!zLevel.CanFallThrough)
                continue;

            // Skip if no start position
            if (zLevel.StartPosition == null)
                continue;

            // Check if 70% of flight time reached
            var flightProgress = zLevel.TimeAlive / zLevel.EstimatedFlightTime;
            if (flightProgress < zLevel.FallCheckDistance)
                continue;

            // Mark check as completed
            zLevel.FallChecked = true;

            // Get current position
            var currentPos = _transform.GetWorldPosition(xform);

            // Check for tile below
            if (HasTileBelow(currentPos, xform.MapID, out var mapBelow))
            {
                Dirty(uid, zLevel);
                continue;
            }

            // No tile - teleport projectile to level below
            if (mapBelow != null)
            {
                TransferProjectileDown(uid, currentPos, mapBelow.Value, physics, xform);

                // Reset check to allow checking on the new level (recursive falling)
                zLevel.FallChecked = false;
                zLevel.StartPosition = currentPos;
                zLevel.TimeAlive = 0f;
            }

            Dirty(uid, zLevel);
        }
    }

    /// <summary>
    /// Checks if there is a tile below the projectile on the current level.
    /// If no tile is found - returns false and the MapId of the level below to fall to.
    /// </summary>
    private bool HasTileBelow(Vector2 worldPos, MapId currentMap, out MapId? mapBelow)
    {
        mapBelow = null;

        // Find grid on current level
        var currentMapCoords = new MapCoordinates(worldPos, currentMap);
        if (!_mapManager.TryFindGridAt(currentMapCoords, out var currentGrid, out var currentGridComp))
            return true; // No grid - don't fall (projectile in space)

        // Check for Z-level component
        if (!_zMapQuery.TryComp(currentGrid, out var zMapComp))
            return true; // Not Z-level map - don't fall

        // Check if there is a tile BELOW the projectile on the CURRENT level
        var tileIndices = _map.TileIndicesFor(currentGrid, currentGridComp, currentMapCoords);
        if (!_map.TryGetTileRef(currentGrid, currentGridComp, tileIndices, out var currentTileRef))
            return true; // Failed to get tile - don't fall

        // If tile is NOT empty - don't fall, there is a floor
        if (!currentTileRef.Tile.IsEmpty)
            return true;

        // Tile is empty - projectile should fall!
        // Search for level below with non-empty tile
        var currentLevel = (currentGrid, zMapComp);
        while (_zLevels.TryMapDown(currentLevel, out var mapBelowEntity))
        {
            if (!_xformQuery.TryComp(mapBelowEntity.Value.Owner, out var mapBelowXform))
                break;

            var targetMapId = mapBelowXform.MapID;
            var belowMapCoords = new MapCoordinates(worldPos, targetMapId);

            // Check if there is grid on level below
            if (_mapManager.TryFindGridAt(belowMapCoords, out var belowGrid, out var belowGridComp))
            {
                var belowTileIndices = _map.TileIndicesFor(belowGrid, belowGridComp, belowMapCoords);
                if (_map.TryGetTileRef(belowGrid, belowGridComp, belowTileIndices, out var belowTileRef) &&
                    !belowTileRef.Tile.IsEmpty)
                {
                    // Found non-empty tile on level below - fall there
                    mapBelow = targetMapId;
                    return false;
                }
            }

            // Continue searching down
            if (_zMapQuery.TryComp(belowGrid, out var belowZMapComp))
                currentLevel = (belowGrid, belowZMapComp);
            else
                break;
        }

        // Couldn't find a solid level below - don't fall (void)
        return true;
    }

    /// <summary>
    /// Teleports the projectile to the level below while maintaining velocity
    /// </summary>
    private void TransferProjectileDown(EntityUid projectile, Vector2 worldPos, MapId targetMap, PhysicsComponent physics, TransformComponent xform)
    {
        // Save current velocity
        var velocity = physics.LinearVelocity;
        var angularVelocity = physics.AngularVelocity;

        // Teleport to new level
        var newMapCoords = new MapCoordinates(worldPos, targetMap);
        _transform.SetMapCoordinates(projectile, newMapCoords);

        // Restore velocity (it could be reset during teleportation)
        _physics.SetLinearVelocity(projectile, velocity, body: physics);
        _physics.SetAngularVelocity(projectile, angularVelocity, body: physics);

        // Update component to track new position
        if (TryComp<ZLevelProjectileComponent>(projectile, out var zLevel))
        {
            zLevel.OriginalMapId = targetMap;
            Dirty(projectile, zLevel);
        }
    }
}
