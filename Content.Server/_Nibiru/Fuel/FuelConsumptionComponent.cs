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
        public bool Activated => CurrentState is FuelLightState.Lit or FuelLightState.Fading;

        [ViewVariables] public float StateExpiryTime = default;
    }
}
