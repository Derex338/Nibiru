using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Nibiru.Heating;

/// <summary>
/// Компонент для поверхностей которые нагревают предметы (костёр, жаровня и т.д.)
/// Работает с PlaceableSurfaceComponent для определения предметов на поверхности
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class HeatingSurfaceComponent : Component
{
    /// <summary>
    /// Скорость нагрева предметов в градусах в секунду
    /// </summary>
    [DataField]
    public float HeatingRate = 50f;

    /// <summary>
    /// Температура при которой предметы начинают гореть
    /// </summary>
    [DataField]
    public float BurnTemperature = 500f;

    /// <summary>
    /// Радиус нагрева (если не используется PlaceableSurface)
    /// </summary>
    [DataField]
    public float HeatingRadius = 0.5f;

    /// <summary>
    /// Минимальная температура источника для работы
    /// </summary>
    [DataField]
    public float MinSourceTemperature = 3f;

    /// <summary>
    /// Только предметы на PlaceableSurface нагреваются
    /// </summary>
    [DataField]
    public bool RequirePlacedOnSurface = true;

    /// <summary>
    /// Звук когда предмет сгорает
    /// </summary>
    [DataField]
    public SoundSpecifier? BurnSound;

    /// <summary>
    /// Звук готовки/нагрева
    /// </summary>
    [DataField]
    public SoundSpecifier? CookingSound;

    /// <summary>
    /// Интервал звука готовки (секунды)
    /// </summary>
    [DataField]
    public float CookingSoundInterval = 3f;

    [DataField]
    public float CookingSoundTimer = 0f;
}

[Serializable, NetSerializable]
public enum HeatingSurfaceVisuals : byte
{
    IsHeating,
    HasItems
}
