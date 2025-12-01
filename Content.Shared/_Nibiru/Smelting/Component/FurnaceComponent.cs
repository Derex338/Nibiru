using Content.Shared.Tag;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Nibiru.Smelting;

/// <summary>
/// Компонент печи для плавки руд
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SmeltingFurnaceComponent : Component
{
    [DataField]
    public string ContainerId = "ore_container";

    [DataField]
    public string Solution = "smelted_metal";

    /// <summary>
    /// Контейнер для руд
    /// </summary>
    [ViewVariables]
    public Container? OreContainer = default!;

    /// <summary>
    /// Максимальное количество предметов в печи
    /// </summary>
    [DataField]
    public int MaxOreCapacity = 10;

    /// <summary>
    /// Текущая температура внутри печи
    /// </summary>
    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public float CurrentTemperature = 0f;

    /// <summary>
    /// Скорость нагрева в градусах в секунду
    /// </summary>
    [DataField]
    public float HeatingRate = 50f;

    /// <summary>
    /// Скорость остывания в градусах в секунду
    /// </summary>
    [DataField]
    public float CoolingRate = 20f;

    [DataField]
    public List<ProtoId<TagPrototype>>? Tags;

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
}
