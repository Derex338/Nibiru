using Content.Shared._Nibiru.LobbedFire;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Audio.Systems;
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

    private readonly List<PendingLobbedShot> _pending = new();
    private readonly List<FallingProjectile> _falling = new();
    private readonly HashSet<EntityUid> _hitCandidates = new();

    private const float LandingIndicatorLeadTime = 0.55f;
    private const float FallDuration = 0.28f;
    private const float LandingHeight = 1.65f;
    private const float LandingHitRadius = 0.38f;

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
                var indicator = Spawn("LobbedIndicator", new MapCoordinates(pending.TargetPosition, pending.MapId));
                if (TryComp<LobbedIndicatorComponent>(indicator, out var indicatorComp))
                {
                    indicatorComp.FlightDuration = MathF.Max(0.1f, pending.FlightDuration - pending.IndicatorDelay + FallDuration);
                    Dirty(indicator, indicatorComp);
                }

                pending.IndicatorSpawned = true;
            }

            if (pending.TimeAlive >= pending.FlightDuration)
            {
                var spawnPos = new MapCoordinates(pending.TargetPosition + new Vector2(0, LandingHeight), pending.MapId);
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
                    MapId = pending.MapId,
                    Shooter = pending.Shooter,
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
                if (TryComp<PhysicsComponent>(falling.ProjectileUid, out var phys))
                {
                    _physics.SetCanCollide(falling.ProjectileUid, true, body: phys);
                    _physics.SetLinearVelocity(falling.ProjectileUid, Vector2.Zero, body: phys);
                }

                var targetMap = new MapCoordinates(falling.TargetPosition, falling.MapId);
                _transform.SetMapCoordinates(falling.ProjectileUid, targetMap);

                var xform = Transform(falling.ProjectileUid);
                var target = GetLandingTarget(falling, xform);

                if (target != EntityUid.Invalid &&
                    TryComp<ProjectileComponent>(falling.ProjectileUid, out var projectile) &&
                    TryComp<EmbeddableProjectileComponent>(falling.ProjectileUid, out _))
                {
                    DoLandingImpact(falling.ProjectileUid, projectile, target, targetMap);
                }
                else if (phys != null)
                {
                    _physics.SetBodyType(falling.ProjectileUid, BodyType.Static, body: phys);
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
            var dist = Vector2.Distance(startPos, targetMap.Value.Position);
            var flightTime = Math.Clamp(dist / 22f, 0.65f, 2.6f);

            EntityUid? projectileShooter = null;
            EntityUid? weapon = null;
            if (TryComp<ProjectileComponent>(projectile, out var projComp))
            {
                projectileShooter = projComp.Shooter;
                weapon = projComp.Weapon;
            }

            _pending.Add(new PendingLobbedShot
            {
                ProtoId = protoId,
                TargetPosition = targetMap.Value.Position,
                MapId = targetMap.Value.MapId,
                FlightDuration = flightTime,
                IndicatorDelay = MathF.Max(0f, flightTime - LandingIndicatorLeadTime),
                Shooter = projectileShooter,
                Weapon = weapon,
            });

            QueueDel(projectile);
        }
    }

    private bool HasRoofOver(EntityUid uid)
    {
        var coordinates = _transform.GetMapCoordinates(uid);
        if (!_mapManager.TryFindGridAt(coordinates, out var gridUid, out var grid))
            return false;

        if (!TryComp<RoofComponent>(gridUid, out var roof))
            return false;

        var tile = _transform.GetGridOrMapTilePosition(uid);
        return _roof.IsRooved((gridUid, grid, roof), tile);
    }

    private EntityUid GetLandingTarget(FallingProjectile falling, TransformComponent projectileXform)
    {
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

    private void DoLandingImpact(EntityUid uid, ProjectileComponent projectile, EntityUid target, MapCoordinates impactCoordinates)
    {
        var hitEvent = new ProjectileHitEvent(projectile.Damage * _damageable.UniversalProjectileDamageModifier, target, projectile.Shooter);
        RaiseLocalEvent(uid, ref hitEvent);

        if (TryComp<DamageableComponent>(target, out var damageable))
        {
            _damageable.TryChangeDamage((target, damageable), hitEvent.Damage, out _, projectile.IgnoreResistances, origin: projectile.Shooter);
        }

        var impactEntityCoordinates = _transform.ToCoordinates(_map.GetMap(impactCoordinates.MapId), impactCoordinates);

        if (projectile.SoundHit != null)
            _audio.PlayPvs(projectile.SoundHit, impactEntityCoordinates);

        if (projectile.ImpactEffect != null)
            RaiseNetworkEvent(new ImpactEffectEvent(projectile.ImpactEffect, GetNetCoordinates(impactEntityCoordinates)), Filter.Pvs(impactEntityCoordinates, entityMan: EntityManager));
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
    }
}
