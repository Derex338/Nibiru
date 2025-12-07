using Content.Shared._Nibiru.Fuel;

namespace Content.Server._Nibiru.Fuel
{
    /// <summary>
    ///     Component that represents a handheld expendable light which can be activated and eventually dies over time.
    /// </summary>
    [RegisterComponent]
    public sealed partial class FuelConsumptionComponent : SharedFuelConsumptionComponent
    {
        /// <summary>
        ///     Status of light, whether or not it is emitting light.
        /// </summary>
        [ViewVariables]
        public bool IsOperational => CurrentState == FuelLightState.Lit &&
                                   CurrentTemperature >= MinOperatingTemperature;

        [ViewVariables] public float StateExpiryTime = 100f;
        [DataField] public float CurrentTemperature = 1000f;
    }
}
