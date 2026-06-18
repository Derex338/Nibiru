using Robust.Shared.Serialization;
using Content.Shared._Nibiru.ModularCraft.Prototypes;
using Content.Shared._Nibiru.ModularCraft.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared._Nibiru.ModularCraft;

[Serializable, NetSerializable]
public enum ModularCraftUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class ModularCraftBUIState : BoundUserInterfaceState
{
    public string? ItemType;
    public Dictionary<string, ModularSlotConfigNet> SlotConfigs;

    public ModularCraftBUIState(string? itemType, Dictionary<string, ModularSlotConfigNet> slotConfigs)
    {
        ItemType = itemType;
        SlotConfigs = slotConfigs;
    }
}

[Serializable, NetSerializable]
public record struct ModularSlotConfigNet(string? ModuleId, string? MaterialId);

[Serializable, NetSerializable]
public sealed class ModularCraftSelectTypeMessage : BoundUserInterfaceMessage
{
    public string ItemTypeId;
    public ModularCraftSelectTypeMessage(string id) { ItemTypeId = id; }
}

[Serializable, NetSerializable]
public sealed class ModularCraftSelectSlotMessage : BoundUserInterfaceMessage
{
    public string PartId;
    public string? ModuleId;
    public string? MaterialId;

    public ModularCraftSelectSlotMessage(string part, string? mod, string? mat)
    {
        PartId = part;
        ModuleId = mod;
        MaterialId = mat;
    }
}

[Serializable, NetSerializable]
public sealed class ModularCraftDoCraftMessage : BoundUserInterfaceMessage
{
}
