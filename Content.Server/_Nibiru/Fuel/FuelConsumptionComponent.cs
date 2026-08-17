using Content.Shared._Nibiru.Fuel;

namespace Content.Server._Nibiru.Fuel
{
    [RegisterComponent]
    public sealed partial class FuelConsumptionComponent : SharedFuelConsumptionComponent
    {
        [ViewVariables]
        public bool IsOperational => CurrentState == FuelLightState.Lit &&
                                     CurrentTemperature >= MinOperatingTemperature;

        [ViewVariables] public float StateExpiryTime = 100f;
        [DataField] public float CurrentTemperature = 1000f;

        [ViewVariables]
        public EntityUid? PlayingStream;
    }
}
