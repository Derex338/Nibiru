using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Temperature.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;
using Robust.Shared.Prototypes;

namespace Content.Server._Nibiru.Chemestry;

public sealed class SolutionCoolingSystem : EntitySystem
{
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;
    [Dependency] private readonly AtmosphereSystem _atmos = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    public float HeatTransferRate = 0.3f;

    // Throttle: обновляем не чаще раза в секунду,
    // т.к. SolutionComponent есть у огромного числа объектов на планете
    private float _updateTimer = 0f;
    private const float UpdateInterval = 1f;
    private readonly List<(EntityUid Uid, SolutionComponent Solution, TransformComponent Transform, GasMixture Air)> _toProcess = new();

    public override void Initialize()
    {
        base.Initialize();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _updateTimer += frameTime;
        if (_updateTimer < UpdateInterval)
            return;
        _updateTimer = 0f;

        _toProcess.Clear();

        var query = EntityQueryEnumerator<SolutionComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var solution, out var transform))
        {
            var air = _atmos.GetContainingMixture((uid, transform));

            if (air == null || solution.Solution.Temperature <= air.Temperature + 10)
                continue;

            _toProcess.Add((uid, solution, transform, air));
        }

        foreach (var (uid, solution, transform, air) in _toProcess)
        {
            if (Deleted(uid))
                continue;

            var temperatureDelta = air.Temperature - solution.Solution.Temperature;
            var k = HeatTransferRate * frameTime;

            var heatCapacityLiquid = GetHeatCapacity(uid, solution);
            var heatTransfer = temperatureDelta * heatCapacityLiquid * k;

            _solution.AddThermalEnergy((uid, solution), heatTransfer / heatCapacityLiquid);

            if (TryComp<TemperatureComponent>(transform.ParentUid, out var temp) && temp.CurrentTemperature < solution.Solution.Temperature)
            {
                temp.CurrentTemperature += solution.Solution.Temperature / 10 * frameTime;
            }

            foreach (var (reagent, quantity) in solution.Solution.GetReagentPrototypes(_prototype))
            {
                if (reagent.MeltingPoint is not null && solution.Solution.Volume != 0)
                {
                    var ev = new MoltenPointChange(solution.Solution.Temperature, transform.ParentUid, reagent);
                    RaiseLocalEvent(transform.ParentUid, ev);
                }
            }
        }
    }

    public float GetHeatCapacity(EntityUid uid, SolutionComponent? comp = null, PhysicsComponent? physics = null)
    {
        if (!Resolve(uid, ref comp) || !Resolve(uid, ref physics, false) || physics.FixturesMass <= 0)
        {
            return Atmospherics.MinimumHeatCapacity;
        }

        return 20 * physics.FixturesMass;
    }
}

public sealed class MoltenPointChange : EventArgs
{
    public float CurrentTemperature;
    public EntityUid uid;
    public ReagentPrototype reagent;

    public MoltenPointChange(float temp, EntityUid Uid, ReagentPrototype Reagent)
    {
        CurrentTemperature = temp;
        uid = Uid;
        reagent = Reagent;
    }
}
