using Content.Shared._Nibiru.Workbench;
using JetBrains.Annotations;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Construction.Conditions;

/// <summary>
///   Condition for construction of an item only on a workbench with a specific ID.
/// </summary>
[UsedImplicitly]
[DataDefinition]
public sealed partial class WorkbenchCondition : IConstructionCondition
{
    /// <summary>
    /// Prototype ID of the workbench on which the item can be built.
    /// If not specified, any workbench will work.
    /// </summary>
    [DataField("workbench")]
    public ProtoId<EntityPrototype>? Workbench;

    /// <summary>
    /// Icon for display in construction guide.
    /// </summary>
    [DataField("guideIcon")]
    public SpriteSpecifier? GuideIcon;

    /// <summary>
    /// Text for display in construction guide.
    /// </summary>
    [DataField("guideText")]
    public string GuideText = "construction-step-condition-workbench";

    public bool Condition(EntityUid user, EntityCoordinates location, Direction direction)
    {
        var entManager = IoCManager.Resolve<IEntityManager>();
        var lookupSystem = entManager.System<EntityLookupSystem>();

        // Get all entities at this position
        foreach (var entity in lookupSystem.GetEntitiesIntersecting(location, LookupFlags.Static | LookupFlags.Approximate))
        {
            // Check for workbench component
            if (!entManager.HasComponent<WorkbenchComponent>(entity))
                continue;

            // If workbench is not specified, any workbench will work
            if (Workbench == null)
                return true;

            // Check for prototype ID match
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
