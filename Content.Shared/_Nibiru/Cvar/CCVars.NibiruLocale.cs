using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    /// Код культуры интерфейса
    /// </summary>
    public static readonly CVarDef<string> ClientLanguage =
        CVarDef.Create("loc.language", "en-US", CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Флаг, выбран ли уже язык пользователем (чтобы не показывать диалог выбора при каждом входе в лобби)
    /// </summary>
    public static readonly CVarDef<bool> NibiruLanguageSelected =
        CVarDef.Create("loc.language_selected", false, CVar.CLIENTONLY | CVar.ARCHIVE);
}
