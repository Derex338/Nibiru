using Content.Server.Power.Components;
using Content.Shared.Placeable;
using Content.Shared.Temperature;
using Content.Shared.Temperature.Components;
using Content.Shared.Temperature.Systems;
using Content.Server.Temperature.Systems;
using Content.Shared._Nibiru.FireHeater;
using Content.Server._Nibiru.Fuel;

namespace Content.Server._Nibiru.FireHeater;

public sealed class FireHeaterSystem : SharedFireHeaterSystem
{
    [Dependency] private readonly TemperatureSystem _temperature = default!;

    public override void Initialize()
    {
        base.Initialize();

        //SubscribeLocalEvent<FireHeaterComponent, MapInitEvent>(OnMapInit);
    }

    public override void Update(float deltaTime)
    {
        var query = EntityQueryEnumerator<FireHeaterComponent, ItemPlacerComponent, FuelConsumptionComponent>();
        while (query.MoveNext(out _, out _, out var placer, out var fuel))
        {
            if (!fuel.Activated)
                continue;

            var energy = fuel.Temperature * deltaTime;
            foreach (var ent in placer.PlacedEntities)
            {
                _temperature.ChangeHeat(ent, energy);
            }
        }
    }
}
