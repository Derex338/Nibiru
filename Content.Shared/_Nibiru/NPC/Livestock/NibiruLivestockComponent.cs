using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._Nibiru.NPC.Livestock;

/// <summary>
/// Компонент животноводства: определяет ресурсы, которые можно собирать с животного,
/// и параметры разведения.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NibiruLivestockComponent : Component
{
    #region Sounds

    /// <summary>
    /// Звук стрижки шерсти.
    /// </summary>
    [DataField]
    public SoundSpecifier? ShearingSound;

    /// <summary>
    /// Звук дойки.
    /// </summary>
    [DataField]
    public SoundSpecifier? MilkingSound;

    /// <summary>
    /// Звук рождения потомства.
    /// </summary>
    [DataField]
    public SoundSpecifier? BirthSound;

    #endregion
    /// <summary>
    /// Ресурсы, которые можно периодически собирать (шерсть, молоко и т.п.).
    /// </summary>
    [DataField, ViewVariables]
    public List<LivestockResource> HarvestableResources = new();

    /// <summary>
    /// Может ли это животное размножаться.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public bool CanBreed = true;

    /// <summary>
    /// Пол существа для разведения.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public LivestockSex Sex = LivestockSex.Female;

    /// <summary>
    /// Протопайп потомства.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public string? OffspringPrototype;

    /// <summary>
    /// Время вынашивания/инкубации в секундах.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float GestationTime = 300f;

    /// <summary>
    /// Текущий таймер вынашивания (если беременна).
    /// </summary>
    [ViewVariables]
    public float GestationAccumulator;

    /// <summary>
    /// Сколько потомков появляется за раз.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public int OffspringCount = 1;

    /// <summary>
    /// Максимальное количество потомков за раз.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public int MaxOffspringCount = 3;

    /// <summary>
    /// Беременна ли сейчас.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public bool IsPregnant;

    /// <summary>
    /// Кулдаун после рождения потомства (в секундах).
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float BreedingCooldown = 600f;

    /// <summary>
    /// Таймер кулдауна.
    /// </summary>
    [ViewVariables]
    public float BreedingCooldownAccumulator;

    /// <summary>
    /// Готово ли к размножению.
    /// </summary>
    [ViewVariables]
    public bool ReadyToBreed => !IsPregnant && BreedingCooldownAccumulator <= 0f;

    /// <summary>
    /// Спрайт для самца.
    /// </summary>
    [DataField]
    public SpriteSpecifier? MaleSprite;

    /// <summary>
    /// Спрайт для самки.
    /// </summary>
    [DataField]
    public SpriteSpecifier? FemaleSprite;
}

/// <summary>
/// Описание собираемого ресурса с животного.
/// </summary>
[DataDefinition]
public sealed partial class LivestockResource
{
    /// <summary>
    /// Прототип предмета, который выдается при сборе.
    /// </summary>
    [DataField(required: true), ViewVariables(VVAccess.ReadWrite)]
    public string ItemPrototype = string.Empty;

    /// <summary>
    /// Время накопления ресурса в секундах.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float GrowthTime = 120f;

    /// <summary>
    /// Текущий прогресс роста.
    /// </summary>
    [ViewVariables]
    public float GrowthAccumulator;

    /// <summary>
    /// Готов ли ресурс к сбору.
    /// </summary>
    [ViewVariables]
    public bool ReadyToHarvest => GrowthAccumulator >= GrowthTime;

    /// <summary>
    /// Количество предметов за один сбор.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public int Yield = 1;

    /// <summary>
    /// Нужен ли инструмент для сбора (ножницы для стрижки и т.п.).
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public string? RequiredTool;
}

[Serializable, NetSerializable]
public enum LivestockSex : byte
{
    Male,
    Female
}

[Serializable, NetSerializable]
public enum LivestockVisuals : byte
{
    Sex,
    IsLeashed,
    BabyStage
}
