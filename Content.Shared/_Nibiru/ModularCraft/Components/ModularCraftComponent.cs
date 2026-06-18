using Content.Shared._Nibiru.ModularCraft.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared._Nibiru.ModularCraft.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class ModularCraftComponent : Component
{
    [DataField]
    public ProtoId<ModularItemPrototype>? CurrentItemType;

    /// <summary>
    /// ID базовой сущности, на основе которой создаётся модульное оружие/инструмент
    /// </summary>
    [DataField]
    public string? BaseEntityPrototype;
}

[DataRecord]
[Serializable, NetSerializable]
public partial record struct ModularSlotConfig(ProtoId<ModularModulePrototype>? ModuleId, ProtoId<ModularMaterialPrototype>? MaterialId);

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ModularItemComponent : Component
{
    [DataField, AutoNetworkedField]
    public Dictionary<ProtoId<ModularPartPrototype>, ModularSlotConfig> SlotConfigs = new();

    // Итоговые кешированные статы
    [ViewVariables, AutoNetworkedField] public FixedPoint2 TotalDamage;
    [ViewVariables, AutoNetworkedField] public FixedPoint2 TotalReach;
    [ViewVariables, AutoNetworkedField] public FixedPoint2 TotalPenetration;
    [ViewVariables, AutoNetworkedField] public FixedPoint2 TotalWeight;
    [ViewVariables, AutoNetworkedField] public FixedPoint2 AttackSpeed;
}
