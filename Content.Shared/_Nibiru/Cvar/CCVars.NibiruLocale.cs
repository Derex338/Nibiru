using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    public static readonly CVarDef<string> ClientLanguage =
        CVarDef.Create("loc.language", "en-US", CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Did user already select language
    /// </summary>
    public static readonly CVarDef<bool> NibiruLanguageSelected =
        CVarDef.Create("loc.language_selected", false, CVar.CLIENTONLY | CVar.ARCHIVE);
}
