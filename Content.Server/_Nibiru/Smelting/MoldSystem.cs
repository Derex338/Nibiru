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
public sealed class MoldSystem : EntitySystem
{
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly TemperatureSystem _temp = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SolutionContainerManagerComponent, MoltenPointChange>(OnCoolMetal);
    }

    private void OnCoolMetal(EntityUid uid, SolutionContainerManagerComponent component, MoltenPointChange args)
    {
        if (args.reagent.ScrapEntity is null || args.CurrentTemperature >= args.reagent.MeltingPoint)
            return;

        if (TryComp<MoldComponent>(args.uid, out var comp))
        {
            bool success = false;

            foreach (var (reagent, entity) in comp.ResultEntities)
            {
                var reagentAmount = _solution.GetTotalPrototypeQuantity(args.uid, reagent);
                if (reagentAmount <= 0)
                    continue;

                foreach (var name in component.Containers)
                {
                    _solution.TryGetSolution((args.uid, component), name, out var solutionEnt, out var solution);

                    if (solution == null || solutionEnt == null)
                        continue;

                    if (solution.Volume >= solution.MaxVolume && reagentAmount >= solution.MaxVolume * 0.8f)
                    {
                        var pos = _transform.GetMapCoordinates(args.uid);

                        var container = _container.EnsureContainer<ContainerSlot>(args.uid, comp.Slot, out var hasContainer);
                        var spawn = Spawn(entity, pos);
                        _container.Insert(spawn, container);
                        if (args.reagent.MeltingPoint is not null && HasComp<TemperatureComponent>(spawn))
                            _temp.ForceChangeTemperature(spawn, args.reagent.MeltingPoint.Value);

                        _solution.RemoveAllSolution(solutionEnt.Value);
                        success = true;
                        break;
                    }
                }

                if (success)
                    break;
            }

            if (!success)
            {
                ScrapSpawn((args.uid, component), args);
            }
            else if (comp.DeleteAfterUse)
            {
                QueueDel(args.uid);
            }
        }
        else
        {
            ScrapSpawn((args.uid, component), args);
        }
    }

    private void ScrapSpawn(Entity<SolutionContainerManagerComponent> ent, MoltenPointChange args)
    {
        var pos = _transform.GetMapCoordinates(args.uid);
        var uid = Spawn(args.reagent.ScrapEntity, pos);
        EnsureComp<SmeltableOreComponent>(uid, out var comp);

        var reagentName = args.reagent.LocalizedName;
        _metaData.SetEntityName(uid, Loc.GetString("smelting-metal-scrap-name", ("metal", reagentName)));

        var reagentAmount = _solution.GetTotalPrototypeQuantity(args.uid, args.reagent.ID);
        comp.ResultAmount = (float)reagentAmount * 0.8f;
        comp.ResultReagent = args.reagent.ID;

        _solution.TryGetSolution(ent.Owner, ent.Comp.Containers.FirstOrDefault(), out var solutionEnt, out var solution);
        if (solutionEnt is not null)
            _solution.RemoveReagent(solutionEnt.Value, args.reagent.ID, reagentAmount);
    }
}
