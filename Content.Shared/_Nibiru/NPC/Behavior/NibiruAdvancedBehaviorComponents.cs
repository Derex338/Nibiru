using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Nibiru.NPC.Behavior;

/// <summary>
/// Компонент территориальности: NPC привязан к своему логову/гнезду.
/// Становится намного агрессивнее при приближении чужаков к потомству.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NibiruTerritorialComponent : Component
{
    /// <summary>
    /// Радиус территории вокруг логова.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float TerritoryRadius = 10f;

    /// <summary>
    /// Множитель агрессии при нахождении чужака на территории.
    /// Увеличивает AggroRange и снижает порог атаки.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float TerritoryAggressionMultiplier = 2f;

    /// <summary>
    /// Есть ли у NPC потомство на территории.
    /// Если да — агрессия ещё выше.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public bool HasOffspringNearby;

    /// <summary>
    /// Множитель агрессии при наличии потомства.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float OffspringProtectionMultiplier = 3f;

    /// <summary>
    /// Звук территориального предупреждения (рёв, рычание).
    /// </summary>
    [DataField]
    public SoundSpecifier? WarningSound;
}

/// <summary>
/// Компонент цикла сна/бодрствования.
/// NPC может засыпать ночью или в определённое время, снижая бдительность.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NibiruSleepCycleComponent : Component
{
    /// <summary>
    /// Является ли NPC ночным хищником (активен ночью, спит днём).
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public bool IsNocturnal;

    /// <summary>
    /// Спит ли NPC прямо сейчас.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public bool IsSleeping;

    /// <summary>
    /// Множитель восприятия во сне (0.1 = 10% от нормального зрения/слуха).
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float SleepPerceptionMultiplier = 0.15f;

    /// <summary>
    /// Продолжительность сна в секундах.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float SleepDuration = 300f;

    /// <summary>
    /// Продолжительность бодрствования в секундах.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float WakeDuration = 600f;

    /// <summary>
    /// Текущий таймер цикла.
    /// </summary>
    [ViewVariables]
    public float CycleAccumulator;

    /// <summary>
    /// Звук засыпания.
    /// </summary>
    [DataField]
    public SoundSpecifier? SleepSound;

    /// <summary>
    /// Периодический звук храпа во сне.
    /// </summary>
    [DataField]
    public SoundSpecifier? SleepingSound;

    /// <summary>
    /// Звук пробуждения.
    /// </summary>
    [DataField]
    public SoundSpecifier? WakeSound;
}

/// <summary>
/// Компонент боязни огня и света.
/// Дикие животные избегают источников огня.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class NibiruFireFearComponent : Component
{
    /// <summary>
    /// Радиус, на котором NPC обнаруживает огонь и начинает его избегать.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float FireDetectionRange = 6f;

    /// <summary>
    /// Множитель дистанции бегства от огня.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float FireFleeMultiplier = 1.5f;

    /// <summary>
    /// Теги, которые считаются источниками огня.
    /// </summary>
    [DataField, ViewVariables]
    public List<string> FireTags = new() { "Torch", "Campfire", "Bonfire", "Lit" };

    /// <summary>
    /// Звук страха перед огнём.
    /// </summary>
    [DataField]
    public SoundSpecifier? FireFearSound;
}
