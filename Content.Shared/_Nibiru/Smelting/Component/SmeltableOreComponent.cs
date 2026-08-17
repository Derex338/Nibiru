using Content.Shared.Chemistry.Reagent;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Nibiru.Smelting;

/// <summary>
/// Component for ore that can be melted
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SmeltableOreComponent : Component
{
    /// <summary>
    /// Melting temperature in degrees
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float MeltingPoint = 1000f;

    /// <summary>
    /// Melting progress (0.0 - 1.0)
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float MeltingProgress = 0f;

    /// <summary>
    /// Melting speed (how quickly melts when temperature is reached)
    /// </summary>
    [DataField]
    public float MeltingSpeed = 0.1f;

    /// <summary>
    /// Reagent obtained from melting
    /// </summary>
    [DataField(required: true)]
    public string ResultReagent = default!;

    /// <summary>
    /// Amount of reagent obtained
    /// </summary>
    [DataField]
    public float ResultAmount = 50f;

    /// <summary>
    /// Temperature of the obtained reagent
    /// </summary>
    [DataField]
    public float ResultTemperature = 1500f;
}
