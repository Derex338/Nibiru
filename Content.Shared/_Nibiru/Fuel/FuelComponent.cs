using Robust.Shared.GameStates;

namespace Content.Shared._Nibiru.Fuel;

/// <summary>
/// Fuel component that can be added to an object with FuelConsumptionComponent
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class FuelComponent : Component
{
    /// <summary>
    /// Fuel amount (in seconds of burn)
    /// </summary>
    [DataField]
    public float Value = 100f;

    /// <summary>
    /// Maximum burn temperature of this fuel (°C)
    /// </summary>
    [DataField]
    public float TemperatureMax = 800f;

    /// <summary>
    /// Minimum burn temperature of this fuel (°C)
    /// </summary>
    [DataField]
    public float TemperatureMin = 400f;
}
