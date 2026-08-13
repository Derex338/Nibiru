using Content.Shared.Construction.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared._Nibiru.Workbench;

public sealed partial class SharedWorkbenchSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototypeManager = default!;

    public string GetExamineName(GenericPartInfo info)
    {
        if (info.ExamineName is not null)
            return Loc.GetString(info.ExamineName.Value);

        return _prototypeManager.Index(info.DefaultPrototype).Name;
    }
}
