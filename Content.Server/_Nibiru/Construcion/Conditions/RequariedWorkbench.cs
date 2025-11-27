using Content.Shared._Nibiru.Workbench;
using Content.Shared.Construction;
using Content.Shared.Examine;
using JetBrains.Annotations;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server._Nibiru.Construction.Conditions
{
    [UsedImplicitly]
    [DataDefinition]
    public sealed partial class RequariedWorkbench : IGraphCondition
    {
        [DataField("id")]
        public ProtoId<EntityPrototype> WorkbenchId { get; set; }

        public SpriteSpecifier? WorkbenchIcon;

        public bool Condition(EntityUid uid, IEntityManager entManager)
        {
            var _map = entManager.System<SharedMapSystem>();
            var _mapManager = IoCManager.Resolve<IMapManager>();

            // get position
            var transformSystem = entManager.System<SharedTransformSystem>();
            var mapUid = entManager.GetComponent<TransformComponent>(uid).MapID;
            var location = entManager.GetComponent<TransformComponent>(uid).Coordinates;
            var objWorldPosition = transformSystem.ToMapCoordinates(location);

            if (!_mapManager.TryFindGridAt(mapUid, objWorldPosition.Position, out var GridUid, out var Grid))
                return false;

            foreach (var entity in _map.GetAnchoredEntities(GridUid, Grid, objWorldPosition))
            {
                if (entManager.HasComponent<WorkbenchComponent>(entity) &&
                    entManager.TryGetComponent<MetaDataComponent>(entity, out var meta) &&
                    meta.EntityPrototype != null)
                {
                    if (meta.EntityPrototype.ID == WorkbenchId || WorkbenchId == "any")
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public bool DoExamine(ExaminedEvent args)
        {
            //var entity = args.Examined;

            //var anchored = IoCManager.Resolve<IEntityManager>().GetComponent<TransformComponent>(entity).Anchored;

            if (Condition(args.Examined, IoCManager.Resolve<IEntityManager>()))
            {
                args.PushMarkup(Loc.GetString("лежит на станке"));
                return true;
            }
            else
            {
                args.PushMarkup(Loc.GetString("должен лежать на станке"));
                return true;
            }

                //switch (Anchored)
                //{
                //    case true when !anchored:
                //        args.PushMarkup(Loc.GetString("construction-examine-condition-entity-anchored"));
                //        return true;
                //    case false when anchored:
                //        args.PushMarkup(Loc.GetString("construction-examine-condition-entity-unanchored"));
                //        return true;
                //}

            return false;
        }

        public IEnumerable<ConstructionGuideEntry> GenerateGuideEntry()
        {
            yield return new ConstructionGuideEntry()
            {
                Localization = "вина графа",
                //Arguments = new (string, object)[] { ("workbench", IoCManager.Resolve<IPrototypeManager>().Index<EntityPrototype>(WorkbenchId).Name) },
                //Icon = WorkbenchIcon,
            };
        }
    }
}
