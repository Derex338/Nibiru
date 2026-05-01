using Robust.Shared.GameStates;
using Robust.Shared.Audio;

namespace Content.Shared._Nibiru.NPC.Behavior;

/// <summary>
/// Система страха для верховых животных.
/// При накоплении страха до максимума животное сбрасывает наездника и убегает.
/// Страх накапливается от урона, количества агрессивных сущностей и игроков рядом.
/// Можно тренировать устойчивость к стрессу.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NibiruMountFearComponent : Component
{
    /// <summary>
    /// Текущий уровень страха (0..MaxFear).
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public float FearLevel;

    /// <summary>
    /// Максимальный уровень страха. При достижении — паника.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float MaxFear = 100f;

    /// <summary>
    /// Сколько страха добавляется за каждую единицу урона.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float FearPerDamage = 5f;

    /// <summary>
    /// Сколько страха добавляется за каждого агрессивного NPC/игрока в радиусе.
    /// Проверяется периодически.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float FearPerNearbyThreat = 2f;

    /// <summary>
    /// Радиус проверки угроз вокруг (для подсчёта количества агрессоров).
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float ThreatScanRadius = 8f;

    /// <summary>
    /// Скорость убывания страха в секунду (когда нет угроз).
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float FearDecayRate = 3f;

    /// <summary>
    /// Интервал проверки угроз в секундах.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float ThreatCheckInterval = 1f;

    /// <summary>
    /// Таймер проверки угроз.
    /// </summary>
    [ViewVariables]
    public float ThreatCheckAccumulator;

    /// <summary>
    /// Уровень тренированности стрессоустойчивости (0..MaxTraining).
    /// Чем выше, тем меньше страха накапливается.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public float StressTraining;

    /// <summary>
    /// Максимальный уровень тренированности.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float MaxStressTraining = 100f;

    /// <summary>
    /// Сколько опыта стрессоустойчивости набирается за каждый стрессовый тик.
    /// Животное привыкает к стрессу при регулярном воздействии.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float TrainingPerStressTick = 0.1f;

    /// <summary>
    /// Множитель снижения страха от тренировки (0..1).
    /// Вычисляется как 1 - (StressTraining / MaxStressTraining * MaxReduction).
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float MaxFearReduction = 0.7f;

    /// <summary>
    /// Страх от огня/факелов поблизости.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float FearFromFire = 8f;

    /// <summary>
    /// Находится ли животное в состоянии паники.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public bool IsPanicking;

    /// <summary>
    /// Длительность паники после сброса наездника (секунды).
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float PanicDuration = 10f;

    /// <summary>
    /// Оставшееся время паники.
    /// </summary>
    [ViewVariables]
    public float PanicTimer;

    /// <summary>
    /// Звук, когда животное паникует.
    /// </summary>
    [DataField]
    public SoundSpecifier? PanicSound;

    /// <summary>
    /// Звук, когда животное нервничает (страх выше 50%).
    /// </summary>
    [DataField]
    public SoundSpecifier? NervousSound;
}
