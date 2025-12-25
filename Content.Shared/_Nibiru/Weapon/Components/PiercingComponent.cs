using Content.Shared.Damage;

namespace Content.Shared._Nibiru.Weapon.Components;

[RegisterComponent]
public sealed partial class ArmorPenetrationComponent : Component
{
    /// <summary>
    /// Словарь со значениями пробития для каждого типа урона
    /// Ключ - тип урона, значение - величина пробития
    /// </summary>
    [DataField("penetration")]
    public Dictionary<string, float> Penetration = new();
}
