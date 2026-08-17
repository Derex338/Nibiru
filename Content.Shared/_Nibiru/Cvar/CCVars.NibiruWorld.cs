using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    public static readonly CVarDef<int> ZLevelsCount =
        CVarDef.Create("world.z_levels_count", 2, CVar.SERVER);
}
