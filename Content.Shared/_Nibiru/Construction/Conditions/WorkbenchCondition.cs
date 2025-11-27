using Content.Shared._Nibiru.Workbench;
using JetBrains.Annotations;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Construction.Conditions;

/// <summary>
///   Условие для постройки предмета только на верстаке с определённым ID.
/// </summary>
[UsedImplicitly]
[DataDefinition]
public sealed partial class WorkbenchCondition : IConstructionCondition
{
    /// <summary>
    /// ID прототипа верстака, на котором можно построить предмет.
    /// Если не указан, подойдёт любой верстак.
    /// </summary>
    [DataField("workbench")]
    public ProtoId<EntityPrototype>? Workbench;

    /// <summary>
    /// Иконка для отображения в гайде постройки.
    /// </summary>
    [DataField("guideIcon")]
    public SpriteSpecifier? GuideIcon;

    /// <summary>
    /// Текст для отображения в гайде постройки.
    /// </summary>
    [DataField("guideText")]
    public string GuideText = "construction-step-condition-workbench";

    public bool Condition(EntityUid user, EntityCoordinates location, Direction direction)
    {
        var entManager = IoCManager.Resolve<IEntityManager>();
        var lookupSystem = entManager.System<EntityLookupSystem>();

        // Получаем все сущности на этой позиции
        foreach (var entity in lookupSystem.GetEntitiesIntersecting(location, LookupFlags.Static | LookupFlags.Approximate))
        {
            // Проверяем наличие компонента верстака
            if (!entManager.HasComponent<WorkbenchComponent>(entity))
                continue;

            // Если workbench не указан, подходит любой верстак
            if (Workbench == null)
                return true;

            // Проверяем совпадение ID прототипа
            if (entManager.TryGetComponent<MetaDataComponent>(entity, out var meta) &&
                meta.EntityPrototype != null &&
                meta.EntityPrototype.ID == Workbench)
            {
                return true;
            }
        }

        return false;
    }

    public ConstructionGuideEntry GenerateGuideEntry()
    {
        return new ConstructionGuideEntry
        {
            Localization = GuideText,
            Icon = GuideIcon
        };
    }
}
