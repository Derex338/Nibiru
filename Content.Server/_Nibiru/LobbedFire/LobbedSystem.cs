using Content.Shared._Nibiru.LobbedFire;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using System.Numerics;

namespace Content.Server._Nibiru.LobbedFire;

public sealed partial class LobbedSystem : SharedLobbedSystem
{
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedRoofSystem _roof = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private IMapManager _mapManager = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
[Dependency] private SharedAppearanceSystem _appearance = default!;
[Dependency] private Content.Shared.Blocking.BlockingSystem _blocking = default!;
[Dependency] private Content.Shared._CE.ZLevels.Core.EntitySystems.CESharedZLevelsSystem _zLevels = default!;

    private readonly List<PendingLobbedShot> _pending = new();
    private readonly List<FallingProjectile> _falling = new();
    private readonly HashSet<EntityUid> _hitCandidates = new();

    private const float LandingIndicatorLeadTime = 0.55f;
    private const float FallDuration = 0.28f;
    private const float LandingHeight = 1.65f;
    private const float LandingHitRadius = 0.65f; // Увеличен для более надежного попадания между уровнями

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GunComponent, AmmoShotEvent>(OnAmmoShot);
        SubscribeLocalEvent<EmbeddableProjectileComponent, ProjectileEmbedEvent>(OnProjectileEmbed);
        SubscribeLocalEvent<EmbeddableProjectileComponent, EmbedDetachEvent>(OnProjectileDetach);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        for (var i = _pending.Count - 1; i >= 0; i--)
        {
            var pending = _pending[i];
            pending.TimeAlive += frameTime;

            if (!pending.IndicatorSpawned && pending.TimeAlive >= pending.IndicatorDelay)
            {
                EnsureRoofCheck(ref pending);

                var indicator = Spawn("LobbedIndicator", new MapCoordinates(pending.TargetPosition, pending.ActualTargetMapId!.Value));
                if (TryComp<LobbedIndicatorComponent>(indicator, out var indicatorComp))
                {
                    indicatorComp.FlightDuration = MathF.Max(0.1f, pending.FlightDuration - pending.IndicatorDelay + FallDuration);
                    Dirty(indicator, indicatorComp);
                }

                pending.IndicatorSpawned = true;
            }

            if (pending.TimeAlive >= pending.FlightDuration)
            {
                EnsureRoofCheck(ref pending);

                // Спавн снаряда на правильном уровне (с учетом крыши)
                var targetMapId = pending.ActualTargetMapId!.Value;
                var spawnPos = new MapCoordinates(pending.TargetPosition + new Vector2(0, LandingHeight), targetMapId);
                var projectile = Spawn(pending.ProtoId, spawnPos);

                if (pending.Shooter != null && TryComp<ProjectileComponent>(projectile, out var projComp))
                {
                    projComp.Shooter = pending.Shooter;
                    projComp.Weapon = pending.Weapon;
                    Dirty(projectile, projComp);
                }

                if (!TryComp<PhysicsComponent>(projectile, out var phys))
                    phys = EnsureComp<PhysicsComponent>(projectile);

                _physics.SetCanCollide(projectile, false, body: phys);
                _physics.SetLinearVelocity(projectile, new Vector2(0, -LandingHeight / FallDuration), body: phys);

                _falling.Add(new FallingProjectile
                {
                    ProjectileUid = projectile,
                    TargetPosition = pending.TargetPosition,
                    MapId = targetMapId,
                    Shooter = pending.Shooter,
                    HitRoof = targetMapId != pending.MapId,
                });

                _pending.RemoveAt(i);
            }
            else
            {
                _pending[i] = pending;
            }
        }

        for (var i = _falling.Count - 1; i >= 0; i--)
        {
            var falling = _falling[i];
            falling.TimeAlive += frameTime;

            if (falling.TimeAlive >= FallDuration)
            {
                var targetMap = new MapCoordinates(falling.TargetPosition, falling.MapId);
                _transform.SetMapCoordinates(falling.ProjectileUid, targetMap);

                var xform = Transform(falling.ProjectileUid);
                var target = GetLandingTarget(falling, xform, falling.HitRoof);

                bool embedded = false;
                if (target != EntityUid.Invalid &&
                    HasComp<EmbeddableProjectileComponent>(falling.ProjectileUid))
                {
                    embedded = DoLandingImpact(falling.ProjectileUid, target, targetMap, falling.Shooter);
                }

                if (TryComp<PhysicsComponent>(falling.ProjectileUid, out var phys))
                {
                    if (embedded)
                    {
                        // Не включаем collision для embedded снарядов - они уже static и встроены
                        // SetCanCollide(true) вызвал бы повторное срабатывание collision events
                        _physics.SetBodyType(falling.ProjectileUid, BodyType.Static, body: phys);
                    }
                    else
                    {
                        _physics.SetCanCollide(falling.ProjectileUid, true, body: phys);
                        _physics.SetLinearVelocity(falling.ProjectileUid, Vector2.Zero, body: phys);
                    }
                }

                _falling.RemoveAt(i);
            }
            else
            {
                _falling[i] = falling;
            }
        }
    }

    private void OnAmmoShot(EntityUid uid, GunComponent comp, AmmoShotEvent args)
    {
        if (!args.Lobbed)
            return;

        MapCoordinates? targetMap = null;
        if (comp.ShootCoordinates is { } coords)
            targetMap = _transform.ToMapCoordinates(coords);

        if (targetMap == null)
            return;

        var roofCheckEntity = uid;
        foreach (var projectile in args.FiredProjectiles)
        {
            if (TryComp<ProjectileComponent>(projectile, out var projectileComp) &&
                projectileComp.Shooter is { } shooter)
            {
                roofCheckEntity = shooter;
                break;
            }
        }

        if (HasRoofOver(roofCheckEntity))
        {
            foreach (var projectile in args.FiredProjectiles)
            {
                QueueDel(projectile);
            }

            return;
        }

        foreach (var projectile in args.FiredProjectiles)
        {
            if (!TryComp(projectile, out MetaDataComponent? meta))
                continue;

            var protoId = meta.EntityPrototype?.ID;
            if (string.IsNullOrEmpty(protoId))
                continue;

            var startPos = _transform.GetWorldPosition(projectile);
            var targetPos = targetMap.Value.Position;
            var dist = Vector2.Distance(startPos, targetPos);

            if (comp.MaxRange > 0f)
            {
                if (dist > comp.MaxRange)
                {
                    var dir = (targetPos - startPos);
                    if (dir.LengthSquared() > 0.001f)
                    {
                        targetPos = startPos + Vector2.Normalize(dir) * comp.MaxRange;
                        dist = comp.MaxRange;
                    }
                }
            }

            var flightTime = Math.Clamp(dist / 22f, 0.65f, 2.6f);

            EntityUid? projectileShooter = null;
            EntityUid? weaponUid = null;
            if (TryComp<ProjectileComponent>(projectile, out var projComp))
            {
                projectileShooter = projComp.Shooter;
                weaponUid = projComp.Weapon;
            }

            _pending.Add(new PendingLobbedShot
            {
                ProtoId = protoId,
                TargetPosition = targetPos,
                MapId = targetMap.Value.MapId,
                FlightDuration = flightTime,
                IndicatorDelay = MathF.Max(0f, flightTime - LandingIndicatorLeadTime),
                Shooter = projectileShooter,
                Weapon = weaponUid,
            });

            QueueDel(projectile);
        }
    }

    private bool HasRoofOver(EntityUid uid)
    {
        var coordinates = _transform.GetMapCoordinates(uid);
        return IsTargetRooved(coordinates);
    }

    private bool IsTargetRooved(MapCoordinates coordinates)
    {
        if (!_mapManager.TryFindGridAt(coordinates, out var gridUid, out var grid))
            return false;

        if (!TryComp<RoofComponent>(gridUid, out var roof))
            return false;

        var tile = _map.TileIndicesFor(gridUid, grid, coordinates);
        return _roof.IsRooved((gridUid, grid, roof), tile);
    }

    /// <summary>
    /// Определяет правильный MapId для приземления снаряда с учётом крыши сверху и пустоты снизу.
    /// Проверяет только один раз, результат кэшируется в pending.ActualTargetMapId.
    /// </summary>
    private void EnsureRoofCheck(ref PendingLobbedShot pending)
    {
        if (pending.ActualTargetMapId.HasValue)
            return;

        var checkMap = new MapCoordinates(pending.TargetPosition, pending.MapId);
        var actualMapId = pending.MapId;

        if (!_mapManager.TryFindGridAt(checkMap, out var gridUid, out var grid) ||
            !TryComp<Content.Shared._CE.ZLevels.Core.Components.CEZLevelMapComponent>(gridUid, out var zMapComp))
        {
            pending.ActualTargetMapId = actualMapId;
            return;
        }

        var tile = _map.TileIndicesFor(gridUid, grid, checkMap);

        // Проверка крыши сверху — если есть тайл выше, снаряд приземляется на уровень выше
        if (_zLevels.HasTileAbove(tile, (gridUid, zMapComp)) &&
            _zLevels.TryMapUp((gridUid, zMapComp), out var mapAbove))
        {
            actualMapId = Transform(mapAbove.Value.Owner).MapID;
            pending.ActualTargetMapId = actualMapId;
            return;
        }

        // Проверка пустоты снизу — если под целью нет тайла, снаряд падает на уровень ниже
        if (_map.TryGetTileRef(gridUid, grid, tile, out var tileRef) && tileRef.Tile.IsEmpty)
        {
            // Пустота — ищем первый уровень ниже с тайлом
            var currentGrid = (gridUid, zMapComp);
            while (_zLevels.TryMapDown(currentGrid, out var mapBelow))
            {
                var mapIdBelow = Transform(mapBelow.Value.Owner).MapID;
                var checkBelow = new MapCoordinates(pending.TargetPosition, mapIdBelow);

                if (_mapManager.TryFindGridAt(checkBelow, out var gridBelow, out var gridDataBelow) &&
                    TryComp<Content.Shared._CE.ZLevels.Core.Components.CEZLevelMapComponent>(gridBelow, out var zMapCompBelow))
                {
                    var tileBelow = _map.TileIndicesFor(gridBelow, gridDataBelow, checkBelow);
                    if (_map.TryGetTileRef(gridBelow, gridDataBelow, tileBelow, out var tileRefBelow) && !tileRefBelow.Tile.IsEmpty)
                    {
                        actualMapId = mapIdBelow;
                        break;
                    }
                    currentGrid = (gridBelow, zMapCompBelow);
                }
                else
                {
                    break;
                }
            }
        }

        pending.ActualTargetMapId = actualMapId;
    }

    private EntityUid GetLandingTarget(FallingProjectile falling, TransformComponent projectileXform, bool hitRoof = false)
    {
        // Если попали в тайл выше, стрела должна попасть в пол, а не искать живые цели
        if (hitRoof)
        {
            return projectileXform.GridUid ?? projectileXform.MapUid ?? EntityUid.Invalid;
        }

        _hitCandidates.Clear();
        _lookup.GetEntitiesInRange(falling.MapId, falling.TargetPosition, LandingHitRadius, _hitCandidates, LookupFlags.Dynamic | LookupFlags.Sundries);

        EntityUid target = EntityUid.Invalid;
        var bestDistance = float.MaxValue;

        foreach (var candidate in _hitCandidates)
        {
            if (candidate == falling.ProjectileUid ||
                candidate == falling.Shooter ||
                Deleted(candidate) ||
                !HasComp<DamageableComponent>(candidate))
            {
                continue;
            }

            var candidatePos = _transform.GetWorldPosition(candidate);
            var distance = Vector2.DistanceSquared(candidatePos, falling.TargetPosition);
            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            target = candidate;
        }

        if (target != EntityUid.Invalid)
            return target;

        return projectileXform.GridUid ?? projectileXform.MapUid ?? EntityUid.Invalid;
    }

    private bool DoLandingImpact(EntityUid uid, EntityUid target, MapCoordinates impactCoordinates, EntityUid? shooter)
    {
        // Проверяем что цель - это не grid/map, а живая entity
        // Снаряды/предметы должны встраиваться только в damageable entities, не в землю
        var isLivingTarget = TryComp<DamageableComponent>(target, out var damageable);

        // Overhead shield block check
        if (isLivingTarget && _blocking.IsBlockingOverhead(target, out var blocking))
        {
            _audio.PlayPvs(blocking.BlockSound, target);

            // Still play impact effect for visuals if available
            if (TryComp<ProjectileComponent>(uid, out var projComp) && projComp.ImpactEffect != null)
            {
                var entityCoordinates = _transform.ToCoordinates(_map.GetMap(impactCoordinates.MapId), impactCoordinates);
                RaiseNetworkEvent(new ImpactEffectEvent(projComp.ImpactEffect, GetNetCoordinates(entityCoordinates)), Filter.Pvs(entityCoordinates, entityMan: EntityManager));
            }

            return false;
        }

        // Поднимаем событие попадания и наносим урон только для живых целей
        if (isLivingTarget)
        {
            DamageSpecifier? damage = null;
            bool ignoreResistances = false;

            if (TryComp<ProjectileComponent>(uid, out var projectile))
            {
                damage = projectile.Damage * _damageable.UniversalProjectileDamageModifier;
                ignoreResistances = projectile.IgnoreResistances;

                var hitEvent = new ProjectileHitEvent(damage, target, shooter ?? projectile.Shooter);
                RaiseLocalEvent(uid, ref hitEvent);
            }
            else if (TryComp<MeleeWeaponComponent>(uid, out var melee))
            {
                damage = melee.Damage;
            }
            else if (TryComp<DamageOnHighSpeedImpactComponent>(uid, out var speedImpact))
            {
                damage = speedImpact.Damage;
            }

            if (damage != null)
            {
                _damageable.TryChangeDamage((target, damageable!), damage, out _, ignoreResistances, origin: shooter);
            }
        }

        var impactEntityCoordinates = _transform.ToCoordinates(_map.GetMap(impactCoordinates.MapId), impactCoordinates);

        if (TryComp<ProjectileComponent>(uid, out var pComp))
        {
            if (pComp.SoundHit != null)
                _audio.PlayPvs(pComp.SoundHit, impactEntityCoordinates);

            if (pComp.ImpactEffect != null)
                RaiseNetworkEvent(new ImpactEffectEvent(pComp.ImpactEffect, GetNetCoordinates(impactEntityCoordinates)), Filter.Pvs(impactEntityCoordinates, entityMan: EntityManager));
        }

        // Возвращаем true только если предмет встроился в живую цель
        return isLivingTarget;
    }

    private void OnProjectileEmbed(EntityUid uid, EmbeddableProjectileComponent component, ref ProjectileEmbedEvent args)
    {
        if (!HasComp<MapGridComponent>(args.Embedded) && !HasComp<MapComponent>(args.Embedded))
            return;

        _appearance.SetData(uid, NibiruLobbedArrowVisuals.Grounded, true);
    }

    private void OnProjectileDetach(EntityUid uid, EmbeddableProjectileComponent component, ref EmbedDetachEvent args)
    {
        _appearance.SetData(uid, NibiruLobbedArrowVisuals.Grounded, false);
    }

    private struct PendingLobbedShot
    {
        public string ProtoId;
        public Vector2 TargetPosition;
        public MapId MapId;
        public MapId? ActualTargetMapId; // Реальный MapId после проверки крыши (null = еще не проверено)
        public float FlightDuration;
        public float IndicatorDelay;
        public float TimeAlive;
        public bool IndicatorSpawned;
        public EntityUid? Shooter;
        public EntityUid? Weapon;
    }

    private struct FallingProjectile
    {
        public EntityUid ProjectileUid;
        public Vector2 TargetPosition;
        public MapId MapId;
        public float TimeAlive;
        public EntityUid? Shooter;
        public bool HitRoof;
    }
}
