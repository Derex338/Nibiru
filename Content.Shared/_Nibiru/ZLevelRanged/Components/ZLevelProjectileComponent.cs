using Robust.Shared.GameStates;
using Robust.Shared.Map;
using System.Numerics;

namespace Content.Shared._Nibiru.ZLevelRanged.Components;

/// <summary>
/// Позволяет снаряду пересекать Z-уровни во время полёта.
/// На 70% пути проверяет наличие тайла внизу и падает если его нет.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ZLevelProjectileComponent : Component
{
    /// <summary>
    /// Может ли снаряд пролетать сквозь пустые тайлы вниз
    /// </summary>
    [DataField]
    public bool CanFallThrough = true;

    /// <summary>
    /// На каком % пройденного пути проверять падение (0.7 = 70%)
    /// </summary>
    [DataField]
    public float FallCheckDistance = 0.7f;

    /// <summary>
    /// Начальная позиция снаряда (мировая)
    /// </summary>
    public Vector2? StartPosition;

    /// <summary>
    /// Изначальная скорость снаряда (для расчёта пройденного пути)
    /// </summary>
    public float InitialSpeed;

    /// <summary>
    /// Уже проверяли падение?
    /// </summary>
    public bool FallChecked = false;

    /// <summary>
    /// Может ли стрелять прямой наводкой между уровнями (игнорирует препятствия между Z-уровнями)
    /// </summary>
    [DataField]
    public bool DirectFire = false;

    /// <summary>
    /// Начальный MapId для отслеживания смены уровня
    /// </summary>
    public MapId? OriginalMapId;

    /// <summary>
    /// Время с момента создания снаряда (для точной проверки 70% пути)
    /// </summary>
    public float TimeAlive;

    /// <summary>
    /// Примерное время полета снаряда до цели (вычисляется при создании)
    /// </summary>
    public float EstimatedFlightTime = 1.0f;
}
