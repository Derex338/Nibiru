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
    /// Целевая температура горения топлива (°C)
    /// </summary>
    [DataField]
    public float TargetBurnTemperature = 800f;

    /// <summary>
    /// Минимальная температура для работы (°C)
    /// </summary>
    [DataField]
    public float MinOperatingTemperature = 200f;

    /// <summary>
    /// Скорость нагрева (°C/сек)
    /// </summary>
    [DataField]
    public float HeatingRate = 100f;

    /// <summary>
    /// Скорость остывания (°C/сек)
    /// </summary>
    [DataField]
    public float CoolingRate = 50f;

    /// <summary>
    /// Скорость потребления топлива (единиц/сек)
    /// </summary>
    [DataField]
    public float FuelConsumptionRate = 1f;

    /// <summary>
    /// Звук горения
    /// </summary>
    [DataField]
    public SoundSpecifier? LoopedSound = new SoundPathSpecifier("/Audio/Items/Flare/flare_burn.ogg");

    /// <summary>
    /// Звук поджигания
    /// </summary>
    [DataField]
    public SoundSpecifier? LitSound;

    /// <summary>
    /// Звук затухания
    /// </summary>
    [DataField]
    public SoundSpecifier? DieSound;

    /// <summary>
    /// Whitelist для топлива
    /// </summary>
    [DataField]
    public EntityWhitelist? FuelWhitelist;

    /// <summary>
    /// Можно ли вообще потушить этот источник огня
    /// </summary>
    [DataField]
    public bool CanBeExtinguished = true;

    /// <summary>
    /// Можно ли потушить голыми руками (без инструмента)
    /// </summary>
    [DataField]
    public bool CanExtinguishByHand = false;

    /// <summary>
    /// Whitelist инструментов для тушения. Null — любой предмет подходит
    /// </summary>
    [DataField]
    public EntityWhitelist? ExtinguisherWhitelist;

    /// <summary>
    /// Качество инструмента, которое требуется для тушения (например, Digging для лопаты)
    /// </summary>
    [DataField]
    public string? ExtinguisherQuality;

    /// <summary>
    /// Задержка тушения инструментом
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
/// Событие изменения состояния топлива
/// </summary>
[ByRefEvent]
public record struct FuelStateChangedEvent(
    bool IsLit,
    float RemainingFuel,
    float CurrentTemperature
);

/// <summary>
/// Событие изменения температуры
/// </summary>
[ByRefEvent]
public record struct TemperatureChangedEvent(
    float OldTemperature,
    float NewTemperature,
    bool IsOperational
);
