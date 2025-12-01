using Content.Shared.Chemistry.Reagent;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

/// <summary>
/// Компонент для руды которую можно плавить
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SmeltableOreComponent : Component
{
    /// <summary>
    /// Температура плавления в градусах
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float MeltingPoint = 1000f;

    /// <summary>
    /// Прогресс плавления (0.0 - 1.0)
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float MeltingProgress = 0f;

    /// <summary>
    /// Скорость плавления (как быстро плавится при достижении температуры)
    /// </summary>
    [DataField]
    public float MeltingSpeed = 0.1f;

    /// <summary>
    /// Реагент который получается при плавке
    /// </summary>
    [DataField(required: true)]
    public ProtoId<ReagentPrototype> ResultReagent = default!;

    /// <summary>
    /// Количество реагента которое получается
    /// </summary>
    [DataField]
    public float ResultAmount = 50f;

    /// <summary>
    /// Температура получаемого реагента
    /// </summary>
    [DataField]
    public float ResultTemperature = 1500f;
}
