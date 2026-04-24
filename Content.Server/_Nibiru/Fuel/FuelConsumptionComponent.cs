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

        /// <summary>
        /// EntityUid активного зациклённого звука горения, null если не играет
        /// </summary>
        [ViewVariables]
        public EntityUid? PlayingStream;
    }
}
