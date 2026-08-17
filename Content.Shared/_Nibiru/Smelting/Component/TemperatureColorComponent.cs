using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Nibiru.Smelting;

/// <summary>
/// Component for displaying the color and glow of an object depending on temperature
/// </summary>
[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class TemperatureColorComponent : Component
{
    /// <summary>
    /// Minimum temperature for glow to start (K)
    /// For metals, usually ~800K (dim red glow)
    /// </summary>
    [DataField("glowThreshold")]
    public float GlowThreshold = 800f;

    /// <summary>
    /// Maximum glow temperature (K)
    /// </summary>
    [DataField("maxGlowTemperature")]
    public float MaxGlowTemperature = 6000f;

    /// <summary>
    /// Minimum glow intensity
    /// </summary>
    [DataField("minIntensity")]
    public float MinIntensity = 0.5f;

    /// <summary>
    /// Maximum glow intensity
    /// </summary>
    [DataField("maxIntensity")]
    public float MaxIntensity = 4.0f;

    /// <summary>
    /// Power exponent for intensity curve
    /// (1.0 = linear, >1 = fast rise, <1 = slow rise)
    /// </summary>
    [DataField("intensityExponent")]
    public float IntensityExponent = 1.5f;

    /// <summary>
    /// Minimum glow radius
    /// </summary>
    [DataField("minRadius")]
    public float MinRadius = 0.3f;

    /// <summary>
    /// Maximum glow radius
    /// </summary>
    [DataField("maxRadius")]
    public float MaxRadius = 2.0f;

    /// <summary>
    /// Cast shadows from glow
    /// </summary>
    [DataField("castShadows")]
    public bool CastShadows = true;

    /// <summary>
    /// Apply color modulation to object's sprite
    /// </summary>
    [DataField("modulateSprite")]
    public bool ModulateSprite = true;

    /// <summary>
    /// Current temperature (for synchronization)
    /// </summary>
    [DataField("currentTemperature"), ViewVariables(VVAccess.ReadWrite)]
    [AutoNetworkedField]
    public float CurrentTemperature = 293f;

    /// <summary>
    /// Current color (for synchronization)
    /// </summary>
    [DataField("currentColor"), ViewVariables(VVAccess.ReadWrite)]
    [AutoNetworkedField]
    public Color CurrentColor = Color.White;
}

/// <summary>
/// Visual states for temperature
/// </summary>
[Serializable, NetSerializable]
public enum TemperatureColorVisuals : byte
{
    Temperature,
    Color
}
