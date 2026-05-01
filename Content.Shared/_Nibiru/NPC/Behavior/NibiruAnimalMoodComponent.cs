using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Nibiru.NPC.Behavior;

/// <summary>
/// Настроение и преданность прирученного животного.
/// Если не кормить или обижать — животное теряет настроение и может одичать.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NibiruAnimalMoodComponent : Component
{
    /// <summary>
    /// Текущее настроение (0..MaxMood).
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public float Mood = 75f;

    /// <summary>
    /// Максимальное значение настроения.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float MaxMood = 100f;

    /// <summary>
    /// Скорость убывания настроения в секунду.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float MoodDecayRate = 0.02f;

    /// <summary>
    /// Прибавка настроения при кормлении.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float MoodPerFeeding = 20f;

    /// <summary>
    /// Прибавка настроения при взаимодействии (поглаживание).
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float MoodPerPetting = 10f;

    /// <summary>
    /// Штраф настроения при ударе хозяином.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float MoodPenaltyOnHit = 30f;

    /// <summary>
    /// Порог настроения, ниже которого животное перестаёт слушаться команд.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float ObedienceThreshold = 25f;

    /// <summary>
    /// Порог настроения, ниже которого животное может одичать.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float WildThreshold = 10f;

    /// <summary>
    /// Текущее качественное состояние.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public AnimalMoodState MoodState = AnimalMoodState.Content;
}

[Serializable, NetSerializable]
public enum AnimalMoodState : byte
{
    /// <summary>
    /// Очень счастливо. Повышенная послушность.
    /// </summary>
    Happy,

    /// <summary>
    /// Нормальное состояние.
    /// </summary>
    Content,

    /// <summary>
    /// Грустное. Начинает игнорировать некоторые команды.
    /// </summary>
    Sad,

    /// <summary>
    /// Злое. Не слушается команд, может укусить.
    /// </summary>
    Angry,

    /// <summary>
    /// Одичавшее. Полностью утрачена прирученность.
    /// </summary>
    Wild
}
