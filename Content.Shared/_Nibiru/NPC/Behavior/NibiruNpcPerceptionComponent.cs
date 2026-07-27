using Robust.Shared.GameStates;

namespace Content.Shared._Nibiru.NPC.Behavior;

/// <summary>
/// Компонент восприятия NPC: зрение с настраиваемым углом обзора и слух.
/// Позволяет NPC реагировать только на тех, кто находится в поле зрения или производит шум.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NibiruNpcPerceptionComponent : Component
{
    #region Vision

    /// <summary>
    /// Дальность зрения в тайлах.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public float VisionRange = 10f;

    /// <summary>
    /// Угол обзора в градусах (полный конус, от центра в обе стороны).
    /// 360 = видит во все стороны, 120 = стандартное зрение хищника,
    /// 270 = широкое зрение травоядного.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public float VisionAngle = 120f;

    /// <summary>
    /// Интервал проверки восприятия в секундах.
    /// Не нужно проверять каждый тик — это экономит производительность.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float PerceptionInterval = 0.5f;

    /// <summary>
    /// Таймер до следующей проверки.
    /// </summary>
    [ViewVariables]
    public float PerceptionAccumulator;

    #endregion

    #region Hearing

    /// <summary>
    /// Hearing range. Within this radius the NPC can "hear" running or noisy entities.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public float HearingRange = 8f;

    /// <summary>
    /// Минимальная скорость движения цели, при которой NPC услышит её.
    /// Ходьба шагом (~1.5) будет ниже порога, бег (~4.5) — выше.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float HearingSpeedThreshold = 3.0f;

    /// <summary>
    /// Множитель шанса услышать шагающую цель (когда скорость ниже порога бега).
    /// 0.1 = 10% шанс каждый тик проверки.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float WalkDetectionChance = 0.1f;

    #endregion

    /// <summary>
    /// Список обнаруженных целей (видимых или услышанных).
    /// Обновляется каждый тик перцепции.
    /// </summary>
    [ViewVariables]
    public HashSet<EntityUid> DetectedEntities = new();

    /// <summary>
    /// Последнее известное направление взгляда NPC (нормализованный вектор).
    /// Используется для проверки угла обзора.
    /// </summary>
    [ViewVariables]
    public Angle LastFacingAngle;
}
