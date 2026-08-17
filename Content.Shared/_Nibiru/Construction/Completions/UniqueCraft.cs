using Content.Shared.Construction;
using Robust.Shared.Serialization;

namespace Content.Shared._Nibiru.Construction.Completions;

[DataDefinition]
public sealed partial class UniqueCraft : IGraphAction
{
    public void PerformAction(EntityUid uid, EntityUid? userUid, IEntityManager entityManager)
    {
    }
}
