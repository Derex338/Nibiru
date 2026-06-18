using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Nibiru.ModularCraft.Prototypes;

/// <summary>
/// Тип слота модульной системы (лезвие, рукоять, древко, навершие и т.д.)
/// </summary>
[Prototype]
public sealed partial class ModularPartPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; set; } = default!;

    [DataField("name")]
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// Тип собираемого модульного предмета (меч, топор, кирка, посох)
/// Определяет какие слоты нужны для сборки.
/// </summary>
[Prototype]
public sealed partial class ModularItemPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; set; } = default!;

    [DataField("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Обязательные слоты для этого предмета (например, Blade, Guard, Handle)
    /// </summary>
    [DataField("requiredParts")]
    public List<ProtoId<ModularPartPrototype>> RequiredParts { get; set; } = new();

    /// <summary>
    /// ID базовой сущности (Entity), которая будет заспавнена при крафте
    /// </summary>
    [DataField("baseEntity")]
    public string BaseEntity { get; set; } = default!;
}
