using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    /// Код культуры интерфейса
    /// </summary>
    public static readonly CVarDef<string> ClientLanguage =
        CVarDef.Create("loc.language", "en-US", CVar.CLIENTONLY | CVar.ARCHIVE);
}
