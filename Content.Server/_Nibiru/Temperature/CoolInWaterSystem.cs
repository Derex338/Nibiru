using Content.Server.Atmos.EntitySystems;
using Content.Server.DoAfter;
using Content.Server.Temperature.Systems;
using Content.Shared._Nibiru.Temperature;
using Content.Shared.Atmos;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Components;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Temperature.Components;
using Microsoft.CodeAnalysis;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using System;
using System.Linq;

namespace Content.Server._Nibiru.Temperature;

public sealed partial class CoolInWaterSystem : EntitySystem
{
[Dependency] private SharedSolutionContainerSystem _solution = default!;
[Dependency] private DoAfterSystem _doAfter = default!;
[Dependency] private TemperatureSystem _temp = default!;
[Dependency] private IPrototypeManager _prototype = default!;
[Dependency] private SharedAudioSystem _audio = default!;
[Dependency] private AtmosphereSystem _atmosphereSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SolutionComponent, InteractUsingEvent>(OnCoolInWater);
        SubscribeLocalEvent<CoolInWaterComponent, CoolDoAfterEvent>(OnCoolDoAfter);
    }

    private void OnCoolInWater(EntityUid uid, SolutionComponent component, InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<CoolInWaterComponent>(args.Used, out var comp))
            return;

        //var name = _solution.EnumerateSolutions((uid, component));

        //foreach (var (solName, _) in name)
        //{
        //if (solName == null)
        //    return;

        //if (!_solution.TryGetSolution(uid, solName, out _, out var sol) || sol is null || sol.Volume < 10)
        //    return;

        if (!TryComp<SolutionComponent>(args.Used, out var usedSolution) || component.Solution.Volume < 10)
            return;

        if (!IsWater(component.Solution))
            return;

        if (comp.Solution is not null)
        {
            //if (!_solution.TryGetSolution(args.Used, comp.Solution, out var solution, out var soln))
            //    return;

            if (comp.MinTemperature >= usedSolution.Solution.Temperature)
                return;

            _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, comp.CoolingDelay, new CoolDoAfterEvent(GetNetEntity(args.Used), usedSolution.Solution), args.Used)
            {
                BreakOnMove = true,
                BreakOnDropItem = true,
                NeedHand = true,
            });
        }
        else if (TryComp<TemperatureComponent>(args.Used, out var temperatureComp))
        {
            if (comp.MinTemperature >= temperatureComp.CurrentTemperature)
                return;

            _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, comp.CoolingDelay, new CoolDoAfterEvent(GetNetEntity(args.Used), usedSolution.Solution), args.Used)
            {
                BreakOnMove = true,
                BreakOnDropItem = true,
                NeedHand = true,
            });
        }

        //break;
        //}


        args.Handled = true;
    }

    private void OnCoolDoAfter(EntityUid uid, CoolInWaterComponent comp, CoolDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        EntityUid target = GetEntity(args.TargetUid);

        if (!Exists(target) || !TryComp<SolutionComponent>(target, out var targetSolution))
            return;

        if (comp.Solution is not null)
        {
            //if (!_solution.TryGetSolution(target, comp.Solution, out var solution, out _))
            //    return;

            _solution.SetTemperature((target, targetSolution), comp.MinTemperature);
            Effect(target, comp, args.solution);
            args.Handled = true;
        }
        else if (TryComp<TemperatureComponent>(target, out var temp))
        {
            _temp.ForceChangeTemperature(target, comp.MinTemperature, temp);
            Effect(target, comp, args.solution);
            args.Handled = true;
        }
    }

    // psh psh sound and steam
    private void Effect(EntityUid uid, CoolInWaterComponent comp, Shared.Chemistry.Components.Solution sol)
    {
        if (comp.CoolingSound != null)
            _audio.PlayPvs(comp.CoolingSound, uid);

        sol.RemoveReagent("Water", 10);

        var environment = _atmosphereSystem.GetContainingMixture((uid, Transform(uid)));
        if (environment == null)
            return;

        var merger = new GasMixture(1) { Temperature = 340f };
        merger.SetMoles(5, 10);
        _atmosphereSystem.Merge(environment, merger);
    }

    private bool IsWater(Solution sol)
    {
        foreach (var (reagent, quantity) in sol.GetReagentPrototypes(_prototype))
        {
            if (reagent.ID == "Water")
                return true;
        }

        return false;
    }
}
