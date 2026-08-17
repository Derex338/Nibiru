using Content.Shared.Damage;

namespace Content.Shared._Nibiru.Weapon.Components;

[RegisterComponent]
public sealed partial class ArmorPenetrationComponent : Component
{
    [DataField("penetration")]
    public Dictionary<string, float> Penetration = new();
}
