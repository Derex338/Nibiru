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
/// Обрабатывает снаряды способные перемещаться между Z-уровнями.
/// При достижении 70% пути проверяет наличие тайла внизу и телепортирует снаряд на уровень ниже если его нет.
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
        // Пропускаем навесную стрельбу - она обрабатывается отдельно
        if (args.Lobbed)
            return;

        // Проверяем есть ли у оружия компонент ZLevelCapable
        if (!TryComp<ZLevelCapableWeaponComponent>(uid, out var zLevelWeapon))
            return;

        // Добавляем компонент ко всем выпущенным снарядам
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

            // Вычисляем примерное время полета (для снарядов обычно 1-3 секунды)
            // Это используется для определения момента проверки падения
            comp.EstimatedFlightTime = comp.InitialSpeed > 0 ? 20f / comp.InitialSpeed : 1.5f; // 20 метров - средняя дальность
            comp.EstimatedFlightTime = Math.Clamp(comp.EstimatedFlightTime, 0.5f, 3.0f);

            Dirty(projectile, comp);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Оптимизация: обрабатываем только снаряды с нашим компонентом
        var query = EntityQueryEnumerator<ZLevelProjectileComponent, TransformComponent, PhysicsComponent, ProjectileComponent>();
        while (query.MoveNext(out var uid, out var zLevel, out var xform, out var physics, out var projectile))
        {
            // Инкрементируем время жизни
            zLevel.TimeAlive += frameTime;

            // Пропускаем если уже проверили падение
            if (zLevel.FallChecked)
                continue;

            // Пропускаем если снаряд не может падать
            if (!zLevel.CanFallThrough)
                continue;

            // Пропускаем если нет начальной позиции
            if (zLevel.StartPosition == null)
                continue;

            // Проверяем достигли ли 70% времени полета
            var flightProgress = zLevel.TimeAlive / zLevel.EstimatedFlightTime;
            if (flightProgress < zLevel.FallCheckDistance)
                continue;

            // Отмечаем что проверка выполнена
            zLevel.FallChecked = true;

            // Получаем текущую позицию
            var currentPos = _transform.GetWorldPosition(xform);

            // Проверяем есть ли тайл внизу
            if (HasTileBelow(currentPos, xform.MapID, out var mapBelow))
            {
                Dirty(uid, zLevel);
                continue;
            }

            // Тайла нет - телепортируем снаряд на уровень ниже
            if (mapBelow != null)
            {
                TransferProjectileDown(uid, currentPos, mapBelow.Value, physics, xform);

                // Сбрасываем проверку чтобы на новом уровне тоже проверить (рекурсивное падение)
                zLevel.FallChecked = false;
                zLevel.StartPosition = currentPos;
                zLevel.TimeAlive = 0f;
            }

            Dirty(uid, zLevel);
        }
    }

    /// <summary>
    /// Проверяет есть ли тайл под снарядом на текущем уровне.
    /// Если тайла нет - возвращает false и MapId уровня ниже куда нужно упасть.
    /// </summary>
    private bool HasTileBelow(Vector2 worldPos, MapId currentMap, out MapId? mapBelow)
    {
        mapBelow = null;

        // Находим грид на текущем уровне
        var currentMapCoords = new MapCoordinates(worldPos, currentMap);
        if (!_mapManager.TryFindGridAt(currentMapCoords, out var currentGrid, out var currentGridComp))
            return true; // Нет грида - не падаем (снаряд в космосе)

        // Проверяем есть ли Z-level компонент
        if (!_zMapQuery.TryComp(currentGrid, out var zMapComp))
            return true; // Не Z-level карта - не падаем

        // Проверяем есть ли тайл ПОД снарядом на ТЕКУЩЕМ уровне
        var tileIndices = _map.TileIndicesFor(currentGrid, currentGridComp, currentMapCoords);
        if (!_map.TryGetTileRef(currentGrid, currentGridComp, tileIndices, out var currentTileRef))
            return true; // Не удалось получить тайл - не падаем

        // Если тайл НЕ пустой - не падаем, есть пол
        if (!currentTileRef.Tile.IsEmpty)
            return true;

        // Тайл пустой - снаряд должен упасть!
        // Ищем уровень ниже с непустым тайлом
        var currentLevel = (currentGrid, zMapComp);
        while (_zLevels.TryMapDown(currentLevel, out var mapBelowEntity))
        {
            if (!_xformQuery.TryComp(mapBelowEntity.Value.Owner, out var mapBelowXform))
                break;

            var targetMapId = mapBelowXform.MapID;
            var belowMapCoords = new MapCoordinates(worldPos, targetMapId);

            // Проверяем есть ли грид на уровне ниже
            if (_mapManager.TryFindGridAt(belowMapCoords, out var belowGrid, out var belowGridComp))
            {
                var belowTileIndices = _map.TileIndicesFor(belowGrid, belowGridComp, belowMapCoords);
                if (_map.TryGetTileRef(belowGrid, belowGridComp, belowTileIndices, out var belowTileRef) &&
                    !belowTileRef.Tile.IsEmpty)
                {
                    // Нашли непустой тайл на уровне ниже - туда и упадём
                    mapBelow = targetMapId;
                    return false;
                }
            }

            // Продолжаем искать дальше вниз
            if (_zMapQuery.TryComp(belowGrid, out var belowZMapComp))
                currentLevel = (belowGrid, belowZMapComp);
            else
                break;
        }

        // Не нашли твёрдый уровень ниже - не падаем (бездна)
        return true;
    }

    /// <summary>
    /// Телепортирует снаряд на уровень ниже с сохранением скорости
    /// </summary>
    private void TransferProjectileDown(EntityUid projectile, Vector2 worldPos, MapId targetMap, PhysicsComponent physics, TransformComponent xform)
    {
        // Сохраняем текущую скорость
        var velocity = physics.LinearVelocity;
        var angularVelocity = physics.AngularVelocity;

        // Телепортируем на новый уровень
        var newMapCoords = new MapCoordinates(worldPos, targetMap);
        _transform.SetMapCoordinates(projectile, newMapCoords);

        // Восстанавливаем скорость (она могла сброситься при телепортации)
        _physics.SetLinearVelocity(projectile, velocity, body: physics);
        _physics.SetAngularVelocity(projectile, angularVelocity, body: physics);

        // Обновляем компонент чтобы отслеживать новую позицию
        if (TryComp<ZLevelProjectileComponent>(projectile, out var zLevel))
        {
            zLevel.OriginalMapId = targetMap;
            Dirty(projectile, zLevel);
        }
    }
}
