using Content.Shared._Nibiru.Construction;
using Content.Shared._Nibiru.GameTicking.Rules;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Content.Shared.Tag;
using Content.Shared.Teleportation.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using System.Numerics;

namespace Content.Server._Nibiru.Construcion;

public sealed class CaveEnterSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly TagSystem _tagSystem = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly LinkedEntitySystem _link = default!;
    [Dependency] private readonly TurfSystem Turf = default!;
    [Dependency] private readonly TransformSystem _transform = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CaveEnterComponent, ComponentStartup>(OnStartup);
    }
    private void OnStartup(Entity<CaveEnterComponent> ent, ref ComponentStartup args)
    {
        if (ent.Comp.SecondCaveEnterPrototype == null)
            return;

        ent.Comp.FirstCaveEnter = ent.Owner;

        var query = EntityQueryEnumerator<NibiruSurvivalRuleComponent>();
        while (query.MoveNext(out var uid, out var rule))
        {
            if (rule == null)
                continue;

            var firstXform = Transform(ent);
            var localPos = firstXform.Coordinates.Position;

            if (!_mapManager.TryFindGridAt(rule.CaveMap, localPos, out var caveGridUid, out var caveGrid))
                return;

            var caveCoords = new EntityCoordinates(caveGridUid, localPos);

            var box = Box2.CenteredAround(localPos, new Vector2(2, 2));

            foreach (var entity in _lookup.GetEntitiesIntersecting(caveGridUid, box))
            {
                if (_tagSystem.HasAllTags(entity, "Rock") && TryComp<MetaDataComponent>(rule.CaveMap, out var comp))
                {
                    EntityManager.RunMapInit(rule.CaveMap, comp);
                    Del(entity);
                }
            }

            //foreach (var entity in _map.GetAnchoredEntities(caveGridUid, caveGrid, caveCoords))
            //{
            //    if (_tagSystem.HasAllTags(entity, "Rock"))
            //        QueueDel(entity);
            //}

            ent.Comp.SecondCaveEnter = Spawn(ent.Comp.SecondCaveEnterPrototype, caveCoords);
            if (!_link.TryLink(ent.Comp.FirstCaveEnter!.Value, ent.Comp.SecondCaveEnter.Value, true))
                QueueDel(ent.Owner);

            //var tileEnumerator = _map.GetTilesEnumerator(caveGridUid, caveGrid, box, ignoreEmpty: false);

            //while (tileEnumerator.MoveNext(out var tileRef))
            //{
            //    if (Turf.IsSpace(tileRef) || Turf.IsTileBlocked(tileRef, CollisionGroup.Impassable ^ CollisionGroup.HighImpassable))
            //    {
            //        continue;
            //    }



            //    break;
            //}

            Dirty(ent);

            return;
        }
    }
}

