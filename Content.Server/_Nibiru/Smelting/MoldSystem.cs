using Content.Server._Nibiru.Chemestry;
using Content.Server.Temperature.Systems;
using Content.Shared._Nibiru.Smelting;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Temperature.Components;
using Content.Shared.Temperature.Systems;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis;
using Robust.Shared.Containers;
using System.Linq;
using System.Xml.Linq;

namespace Content.Server._Nibiru.Smelting;

[UsedImplicitly]
public sealed partial class MoldSystem : EntitySystem
{
[Dependency] private SharedSolutionContainerSystem _solution = default!;
[Dependency] private SharedTransformSystem _transform = default!;
[Dependency] private SharedContainerSystem _container = default!;
[Dependency] private TemperatureSystem _temp = default!;
[Dependency] private MetaDataSystem _metaData = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MoldComponent, MoltenPointChange>(OnCoolMetal);
    }

    private void OnCoolMetal(EntityUid uid, MoldComponent comp, MoltenPointChange args)
    {
        if (args.reagent.ScrapEntity is null || args.CurrentTemperature >= args.reagent.MeltingPoint)
            return;

        if (!TryComp<SolutionComponent>(uid, out var component))
            return;

        bool success = false;

        if (comp.ResultEntities.TryGetValue(args.reagent.ID, out var entity))
        {
            var pos = _transform.GetMapCoordinates(args.uid);

            var container = _container.EnsureContainer<ContainerSlot>(args.uid, comp.Slot, out var hasContainer);
            var spawn = Spawn(entity, pos);
            _container.Insert(spawn, container);
            if (args.reagent.MeltingPoint is not null && HasComp<TemperatureComponent>(spawn))
                _temp.ForceChangeTemperature(spawn, args.reagent.MeltingPoint.Value);

            _solution.RemoveAllSolution((args.uid, component));
            success = true;
        }

        if (!success)
        {
            ScrapSpawn((args.uid, component), args);
        }
        else if (comp.DeleteAfterUse)
        {
            QueueDel(args.uid);
        }

        //ScrapSpawn((args.uid, component), args);
    }

    private void ScrapSpawn(Entity<SolutionComponent> ent, MoltenPointChange args)
    {
        var pos = _transform.GetMapCoordinates(args.uid);
        var uid = Spawn(args.reagent.ScrapEntity, pos);
        EnsureComp<SmeltableOreComponent>(uid, out var comp);

        var reagentName = args.reagent.LocalizedName;
        _metaData.SetEntityName(uid, Loc.GetString("smelting-metal-scrap-name", ("metal", reagentName)));

        var reagentAmount = ent.Comp.Solution.Volume;
        comp.ResultAmount = (float)reagentAmount * 0.8f;
        comp.ResultReagent = args.reagent.ID;

        _solution.RemoveReagent(ent, args.reagent.ID, reagentAmount);
    }
}
