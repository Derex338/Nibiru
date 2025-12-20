using Content.Shared.Inventory;
using Robust.Shared.GameStates;

namespace Content.Shared._Nibiru.Vehicle;

[RegisterComponent, NetworkedComponent]
public sealed partial class SaddleComponent : Component, IClothingSlots
{
    [DataField]
    public SlotFlags Slots { get; set; } = SlotFlags.BACK;
}
