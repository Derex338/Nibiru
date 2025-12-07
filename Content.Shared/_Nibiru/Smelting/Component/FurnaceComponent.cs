using Content.Shared.Tag;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Nibiru.Smelting;

/// <summary>
/// Компонент печи для плавки руд и нагрева предметов
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SmeltingFurnaceComponent : Component
{
    [DataField]
    public string ContainerId = "ore_container";

    [DataField]
    public string Solution = "smelted_metal";

    /// <summary>
    /// Контейнер для руд и предметов
    /// </summary>
    [ViewVariables]
    public Container? OreContainer = default!;

    [ViewVariables]
    public Container? SolutionContainer = default!;

    /// <summary>
    /// Максимальное количество предметов в печи
    /// </summary>
    [DataField]
    public int MaxOreCapacity = 10;

    /// <summary>
    /// Белый список тегов для предметов которые можно класть в печь
    /// </summary>
    [DataField]
    public List<ProtoId<TagPrototype>>? Tags;

    /// <summary>
    /// Температура при которой предметы начинают гореть
    /// </summary>
    [DataField]
    public float BurnTemperature = 500f;

    /// <summary>
    /// Звук плавления
    /// </summary>
    [DataField]
    public SoundSpecifier? SmeltingSound;

    /// <summary>
    /// Звук когда руда полностью расплавилась
    /// </summary>
    [DataField]
    public SoundSpecifier? MeltCompleteSound;

    /// <summary>
    /// Звук когда предмет сгорает
    /// </summary>
    [DataField]
    public SoundSpecifier? BurnSound;
}

[Serializable, NetSerializable]
public enum SmeltingFurnaceVisuals : byte
{
    ContainsOre,
    IsSmelting,
    Temperature
}
