using Content.Shared.Construction;
using Robust.Shared.Serialization;

namespace Content.Server.Construction.Completions;

/// <summary>
///     Marks a recipe as unique for a faction.
///     In the list of available crafts, it will be displayed only if the faction does not yet have such an object.
/// </summary>
[DataDefinition]
public sealed partial class UniqueCraft : IGraphAction
{
    public void PerformAction(EntityUid uid, EntityUid? userUid, IEntityManager entityManager)
    {
        // This action serves as a marker for filtering in ConstructionRecipeCheck
    }
}
