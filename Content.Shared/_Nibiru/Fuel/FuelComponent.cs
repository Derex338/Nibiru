using Robust.Shared.GameStates;

namespace Content.Shared._Nibiru.Fuel;

/// <summary>
/// Компонент топлива, которое можно добавить в объект с FuelConsumptionComponent
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class FuelComponent : Component
{
    /// <summary>
    /// Количество топлива (в секундах горения)
    /// </summary>
    [DataField]
    public float Value = 100f;

    /// <summary>
    /// Максимальная температура горения этого топлива (°C)
    /// </summary>
    [DataField]
    public float TemperatureMax = 800f;

    /// <summary>
    /// Минимальная температура горения (°C)
    /// </summary>
    [DataField]
    public float TemperatureMin = 400f;
}
