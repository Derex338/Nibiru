using Content.Shared.Damage;

namespace Content.Shared._Nibiru.Armor.Components;

[RegisterComponent]
public sealed partial class ArmorProtectionComponent : Component
{
    /// <summary>
    /// Словарь со значениями защиты для каждого типа урона
    /// Ключ - тип урона, значение - величина защиты
    /// </summary>
    [DataField("protection")]
    public Dictionary<string, float> Protection = new();
}
