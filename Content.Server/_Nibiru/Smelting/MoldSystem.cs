using Content.Server._Nibiru.Chemestry;
using Content.Server.Temperature.Systems;
using Content.Shared._Nibiru.Smelting;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Temperature.Systems;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis;
using Robust.Client.GameObjects;
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
            foreach (var (reagent, entity) in comp.ResultEntities)
            {
                var reagentAmount = _solution.GetTotalPrototypeQuantity(args.uid, reagent);
                var containers = component.Containers;

                foreach (var name in containers)
                {
                    _solution.TryGetSolution((args.uid, component), name, out var solutionEnt, out var solution);

                    if (solution is not null && reagentAmount < solution.MaxVolume || solutionEnt is null)
                    {
                        ScrapSpawn((args.uid, component), args);
                        continue;
                    }

                    var pos = _transform.GetMapCoordinates(args.uid);

                    var container = _container.EnsureContainer<ContainerSlot>(args.uid, comp.Slot, out var hasContainer);
                    var spawn = Spawn(entity, pos);
                    _container.Insert(spawn, container);
                    if (args.reagent.MeltingPoint is not null)
                        _temp.ForceChangeTemperature(spawn, args.reagent.MeltingPoint.Value);

                    _solution.RemoveReagent(solutionEnt.Value, reagent, reagentAmount);

                    if (comp.DeleteAfterUse)
                    {
                        QueueDel(args.uid);
                    }
                }
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

        var reagentAmount = _solution.GetTotalPrototypeQuantity(args.uid, args.reagent.ID);
        comp.ResultAmount = (float)reagentAmount * 0.8f;
        comp.ResultReagent = args.reagent.ID;

        _solution.TryGetSolution(ent.Owner, ent.Comp.Containers.FirstOrDefault(), out var solutionEnt, out var solution);
        if (solutionEnt is not null)
            _solution.RemoveReagent(solutionEnt.Value, args.reagent.ID, reagentAmount);
    }
}
