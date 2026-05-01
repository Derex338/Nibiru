using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Nibiru.NPC.Training;

[Serializable, NetSerializable]
public enum NibiruAnimalDiet : byte
{
    Herbivore,
    Carnivore,
    Omnivore
}

/// <summary>
/// Компонент приручения. Позволяет игрокам приручать животных с помощью еды.
/// Прирученное животное перестаёт бояться/нападать на хозяина и его фракцию.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NibiruTamableComponent : Component
{
    /// <summary>
    /// Диета животного (влияет на то, что оно может есть).
    /// </summary>
    [DataField("diet"), ViewVariables(VVAccess.ReadWrite)]
    public NibiruAnimalDiet Diet = NibiruAnimalDiet.Omnivore;

    /// <summary>
    /// Список любимых ID еды (дают больше доверия).
    /// </summary>
    [DataField("favoriteFoods"), ViewVariables(VVAccess.ReadWrite)]
    public List<string> FavoriteFoods = new();

    /// <summary>
    /// Список любимых тегов еды.
    /// </summary>
    [DataField("favoriteFoodTags"), ViewVariables(VVAccess.ReadWrite)]
    public List<string> FavoriteFoodTags = new();
    #region Sounds

    /// <summary>
    /// Звук кормления / принятия еды.
    /// </summary>
    [DataField]
    public SoundSpecifier? FeedingSound;

    /// <summary>
    /// Звук приручения (счастливый звук).
    /// </summary>
    [DataField]
    public SoundSpecifier? TamedSound;

    /// <summary>
    /// Звук следования за хозяином (довольное мурлыканье для кошек, виляние хвостом для собак).
    /// </summary>
    [DataField]
    public SoundSpecifier? FollowSound;

    #endregion
    /// <summary>
    /// Текущий уровень доверия (0..MaxTrust).
    /// При достижении TrustThreshold животное считается прирученным.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public float TrustLevel;

    /// <summary>
    /// Порог доверия, при котором животное считается прирученным.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float TrustThreshold = 100f;

    /// <summary>
    /// Максимальное значение доверия.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float MaxTrust = 150f;

    /// <summary>
    /// Сколько доверия добавляет одна единица подходящей еды.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float TrustPerFeeding = 15f;

    /// <summary>
    /// Скорость убывания доверия в секунду, если хозяин не кормит.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float TrustDecayRate = 0.01f;

    /// <summary>
    /// Штраф за агрессию хозяина по отношению к питомцу.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float TrustPenaltyOnHit = 25f;

    /// <summary>
    /// Список прототипов приемлемой еды.
    /// Если пустой — животное ест любую еду.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public List<string>? AcceptedFood;

    /// <summary>
    /// Было ли животное приручено хотя бы раз.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public bool IsTamed;

    /// <summary>
    /// EntityUid хозяина.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public EntityUid? OwnerUid;

    /// <summary>
    /// Можно ли обучать это животное командам (собаки и т.п.).
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public bool Trainable;

    /// <summary>
    /// Список команд, которым МОЖНО обучить это конкретное животное.
    /// Например, кошка не может обучиться Deliver, а собака может Search.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public HashSet<NibiruAnimalCommand> PossibleCommands = new() { NibiruAnimalCommand.Follow, NibiruAnimalCommand.Stay };

    /// <summary>
    /// Выученные команды.
    /// </summary>
    [DataField, ViewVariables]
    public HashSet<NibiruAnimalCommand> LearnedCommands = new();
}

/// <summary>
/// Команды, которым можно обучить прирученное животное.
/// </summary>
[Serializable, NetSerializable]
public enum NibiruAnimalCommand : byte
{
    /// <summary>
    /// Следовать за хозяином.
    /// </summary>
    Follow,

    /// <summary>
    /// Оставаться на месте.
    /// </summary>
    Stay,

    /// <summary>
    /// Атаковать указанную цель.
    /// </summary>
    Attack,

    /// <summary>
    /// Рычать/предупреждать при приближении чужаков.
    /// </summary>
    Guard,

    /// <summary>
    /// Искать предметы по запаху (для собак).
    /// </summary>
    Search,

    /// <summary>
    /// Доставлять предметы (для птиц — письма).
    /// </summary>
    Deliver
}
