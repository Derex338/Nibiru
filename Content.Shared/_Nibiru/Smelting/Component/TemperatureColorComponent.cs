using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Nibiru.Smelting;

/// <summary>
/// Компонент для отображения цвета и свечения объекта в зависимости от температуры
/// </summary>
[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class TemperatureColorComponent : Component
{
    /// <summary>
    /// Минимальная температура для начала свечения (K)
    /// Для металлов обычно ~800K (тускло-красное свечение)
    /// </summary>
    [DataField("glowThreshold")]
    public float GlowThreshold = 800f;

    /// <summary>
    /// Температура максимального свечения (K)
    /// </summary>
    [DataField("maxGlowTemperature")]
    public float MaxGlowTemperature = 6000f;

    /// <summary>
    /// Минимальная интенсивность свечения
    /// </summary>
    [DataField("minIntensity")]
    public float MinIntensity = 0.5f;

    /// <summary>
    /// Максимальная интенсивность свечения
    /// </summary>
    [DataField("maxIntensity")]
    public float MaxIntensity = 4.0f;

    /// <summary>
    /// Показатель степени для кривой интенсивности
    /// (1.0 = линейная, >1 = быстрый рост, <1 = медленный рост)
    /// </summary>
    [DataField("intensityExponent")]
    public float IntensityExponent = 1.5f;

    /// <summary>
    /// Минимальный радиус свечения
    /// </summary>
    [DataField("minRadius")]
    public float MinRadius = 0.3f;

    /// <summary>
    /// Максимальный радиус свечения
    /// </summary>
    [DataField("maxRadius")]
    public float MaxRadius = 2.0f;

    /// <summary>
    /// Отбрасывать ли тени от свечения
    /// </summary>
    [DataField("castShadows")]
    public bool CastShadows = true;

    /// <summary>
    /// Применять ли цветовую модуляцию к спрайту объекта
    /// </summary>
    [DataField("modulateSprite")]
    public bool ModulateSprite = true;

    /// <summary>
    /// Текущая температура (для синхронизации)
    /// </summary>
    [DataField("currentTemperature"), ViewVariables(VVAccess.ReadWrite)]
    [AutoNetworkedField]
    public float CurrentTemperature = 293f;

    /// <summary>
    /// Текущий цвет (для синхронизации)
    /// </summary>
    [DataField("currentColor"), ViewVariables(VVAccess.ReadWrite)]
    [AutoNetworkedField]
    public Color CurrentColor = Color.White;
}

/// <summary>
/// Визуальные состояния для температуры
/// </summary>
[Serializable, NetSerializable]
public enum TemperatureColorVisuals : byte
{
    Temperature,
    Color
}
