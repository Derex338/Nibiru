using Content.Shared.DoAfter;
using Content.Shared.Stacks;
using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Nibiru.Fuel;

[NetworkedComponent]
public abstract partial class SharedFuelConsumptionComponent : Component
{
    [ViewVariables(VVAccess.ReadOnly)]
    public FuelLightState CurrentState;

    [DataField]
    public string TurnOnBehaviourID = string.Empty;

    [DataField]
    public string LitBehaviourID = string.Empty;

    [DataField]
    public string FadeOutBehaviourID = string.Empty;

    [DataField]
    public TimeSpan GlowDuration = TimeSpan.FromSeconds(15f);

    [DataField]
    public TimeSpan FadeOutDuration = TimeSpan.FromSeconds(5f);

    [DataField]
    public float MaxFuelAmount = 500f;

    /// <summary>
    /// Final temperature (°C)
    /// </summary>
    [DataField]
    public float TargetBurnTemperature = 800f;

    /// <summary>
    /// Minimum temperature to work (°C)
    /// </summary>
    [DataField]
    public float MinOperatingTemperature = 200f;

    /// <summary>
    /// Heating rate (°C/sec)
    /// </summary>
    [DataField]
    public float HeatingRate = 100f;

    /// <summary>
    /// Cooling rate (°C/sec)
    /// </summary>
    [DataField]
    public float CoolingRate = 50f;

    /// <summary>
    /// Fuel consumption rate (units/sec)
    /// </summary>
    [DataField]
    public float FuelConsumptionRate = 1f;

    /// <summary>
    /// Sound of burning fuel
    /// </summary>
    [DataField]
    public SoundSpecifier? LoopedSound = new SoundPathSpecifier("/Audio/Items/Flare/flare_burn.ogg");

    /// <summary>
    /// Sound of igniting fuel
    /// </summary>
    [DataField]
    public SoundSpecifier? LitSound;

    /// <summary>
    /// Sound of dying fuel
    /// </summary>
    [DataField]
    public SoundSpecifier? DieSound;

    /// <summary>
    /// Whitelist for fuel
    /// </summary>
    [DataField]
    public EntityWhitelist? FuelWhitelist;

    /// <summary>
    /// Can the fire be extinguished at all
    /// </summary>
    [DataField]
    public bool CanBeExtinguished = true;

    /// <summary>
    /// Can the fire be extinguished by hand (without tools)
    /// </summary>
    [DataField]
    public bool CanExtinguishByHand = false;

    /// <summary>
    /// Whitelist of tools for extinguishing. Null - any item is suitable
    /// </summary>
    [DataField]
    public EntityWhitelist? ExtinguisherWhitelist;

    /// <summary>
    /// The quality of the tool required for extinguishing (e.g., Digging for a shovel)
    /// </summary>
    [DataField]
    public string? ExtinguisherQuality;

    /// <summary>
    /// Delay to extinguish with tool
    /// </summary>
    [DataField]
    public float ExtinguishToolDuration = 2f;
}

[Serializable, NetSerializable]
public enum FuelLightState : byte
{
    BrandNew = 0,
    Lit = 1,
    Fading = 2,
    Dead = 3,
}

[Serializable, NetSerializable]
public enum FuelLightVisuals : byte
{
    State,
    Behavior,
}

[Serializable, NetSerializable]
public sealed partial class ExtinguishDoAfterEvent : SimpleDoAfterEvent
{
}

/// <summary>
/// Event of fuel state change
/// </summary>
[ByRefEvent]
public record struct FuelStateChangedEvent(
    bool IsLit,
    float RemainingFuel,
    float CurrentTemperature
);

/// <summary>
/// Event of temperature change
/// </summary>
[ByRefEvent]
public record struct TemperatureChangedEvent(
    float OldTemperature,
    float NewTemperature,
    bool IsOperational
);
