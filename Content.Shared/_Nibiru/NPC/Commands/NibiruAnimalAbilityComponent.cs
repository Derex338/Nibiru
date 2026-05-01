using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Nibiru.NPC.Commands;

/// <summary>
/// Способности, специфичные для конкретного вида животного.
/// Одно животное может иметь несколько способностей.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NibiruAnimalAbilityComponent : Component
{
    #region Sounds

    /// <summary>
    /// Звук рычания/предупреждения при охране.
    /// </summary>
    [DataField]
    public SoundSpecifier? GrowlSound;

    #endregion
    /// <summary>
    /// Список доступных способностей данного животного.
    /// </summary>
    [DataField, ViewVariables]
    public HashSet<AnimalAbilityType> Abilities = new();

    /// <summary>
    /// Радиус охраны (для Guard-способности). Рычит при приближении чужаков.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float GuardRadius = 5f;

    /// <summary>
    /// Радиус поиска предметов (для Search-способности собак).
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float SearchRadius = 15f;

    /// <summary>
    /// Максимальная дальность доставки для птиц.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float DeliveryRange = 50f;

    /// <summary>
    /// Может ли животное нести предмет (для доставки писем, переноски добычи).
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public bool CanCarryItem;

    /// <summary>
    /// Текущий предмет, который несёт животное.
    /// </summary>
    [ViewVariables]
    public EntityUid? CarriedItem;

    /// <summary>
    /// Кулдаун способностей в секундах.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float AbilityCooldown = 30f;

    /// <summary>
    /// Таймер текущего кулдауна.
    /// </summary>
    [ViewVariables]
    public float CooldownAccumulator;
}

/// <summary>
/// Типы способностей животных.
/// </summary>
[Serializable, NetSerializable]
public enum AnimalAbilityType : byte
{
    /// <summary>
    /// Охрана: рычит и предупреждает при приближении чужаков.
    /// </summary>
    Guard,

    /// <summary>
    /// Поиск: выслеживает предметы или существ по запаху.
    /// </summary>
    Search,

    /// <summary>
    /// Доставка: птицы переносят письма/предметы на расстояние.
    /// </summary>
    Deliver,

    /// <summary>
    /// Охота на вредителей: кошки ловят мышей и тараканов.
    /// </summary>
    PestControl,

    /// <summary>
    /// Вьючное животное: может нести груз.
    /// </summary>
    PackAnimal
}
