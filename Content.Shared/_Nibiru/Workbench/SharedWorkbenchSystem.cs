using System.Linq;
using Content.Shared.Construction.Components;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using static Content.Shared.Interaction.SharedInteractionSystem;

namespace Content.Shared._Nibiru.Workbench;

public sealed class SharedWorkbenchSystem : EntitySystem
{
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] protected readonly IPrototypeManager PrototypeManager = default!;
    [Dependency] protected readonly SharedTransformSystem TransformSystem = default!;

    public string GetExamineName(GenericPartInfo info)
    {
        if (info.ExamineName is not null)
            return Loc.GetString(info.ExamineName.Value);

        return PrototypeManager.Index(info.DefaultPrototype).Name;
    }
}

