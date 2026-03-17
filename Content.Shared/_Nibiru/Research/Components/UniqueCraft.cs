using Content.Shared.Construction;
using Robust.Shared.Serialization;

namespace Content.Server.Construction.Completions;

/// <summary>
///     Помечает рецепт как уникальный для фракции. 
///     В списке доступных крафтов он будет отображаться только если такого объекта еще нет у фракции.
/// </summary>
[DataDefinition]
public sealed partial class UniqueCraft : IGraphAction
{
    public void PerformAction(EntityUid uid, EntityUid? userUid, IEntityManager entityManager)
    {
        // Этот экшен служит маркером для фильтрации в ConstructionRecipeCheck
    }
}
