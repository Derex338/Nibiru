using Content.Server.Atmos.EntitySystems;
using Content.Server.DoAfter;
using Content.Server.Temperature.Systems;
using Content.Shared._Nibiru.Temperature;
using Content.Shared.Atmos;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Temperature.Components;
using Microsoft.CodeAnalysis;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using System;
using System.Linq;

namespace Content.Server._Nibiru.Temperature;

public sealed class CoolInWaterSystem : EntitySystem
{
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;
    [Dependency] private readonly DoAfterSystem _doAfter = default!;
    [Dependency] private readonly TemperatureSystem _temp = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly AtmosphereSystem _atmosphereSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SolutionContainerManagerComponent, InteractUsingEvent>(OnCoolInWater);
        SubscribeLocalEvent<CoolInWaterComponent, CoolDoAfterEvent>(OnCoolDoAfter);
    }

    private void OnCoolInWater(EntityUid uid, SolutionContainerManagerComponent component, InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<CoolInWaterComponent>(args.Used, out var comp))
            return;

        var name = component.Containers.FirstOrDefault();
        if (!_solution.TryGetSolution(uid, name, out var _, out var sol) || sol.Volume < 10)
            return;

        if (!IsWater(sol))
            return;

        if (comp.Solution is not null)
        {
            if (!_solution.TryGetSolution(args.Used, comp.Solution, out var solution, out var soln))
                return;

            if (comp.MinTemperature >= soln.Temperature)
                return;

            _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, comp.CoolingDelay, new CoolDoAfterEvent(GetNetEntity(args.Used), sol), args.Used)
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

            _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, comp.CoolingDelay, new CoolDoAfterEvent(GetNetEntity(args.Used), sol), args.Used)
            {
                BreakOnMove = true,
                BreakOnDropItem = true,
                NeedHand = true,
            });
        }

        args.Handled = true;
    }

    private void OnCoolDoAfter(EntityUid uid, CoolInWaterComponent comp, CoolDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        EntityUid target = GetEntity(args.TargetUid);

        if (!Exists(target))
            return;

        if (comp.Solution is not null)
        {
            if (!_solution.TryGetSolution(target, comp.Solution, out var solution, out _))
                return;

            _solution.SetTemperature(solution.Value, comp.MinTemperature);
            Effect(target, comp, args.solution);
        }
        else if (TryComp<TemperatureComponent>(target, out var temp))
        {
            _temp.ForceChangeTemperature(target, comp.MinTemperature, temp);
            Effect(target, comp, args.solution);
        }

        args.Handled = true;
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

    private bool IsWater(Shared.Chemistry.Components.Solution sol)
    {
        foreach (var (reagent, quantity) in sol.GetReagentPrototypes(_prototype))
        {
            if (reagent.ID == "Water")
                return true;
        }

        return false;
    }
}
