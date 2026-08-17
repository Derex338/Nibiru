using Content.Shared.Damage;

namespace Content.Shared._Nibiru.Armor.Components;

[RegisterComponent]
public sealed partial class ArmorProtectionComponent : Component
{
    [DataField("protection")]
    public Dictionary<string, float> Protection = new();
}
