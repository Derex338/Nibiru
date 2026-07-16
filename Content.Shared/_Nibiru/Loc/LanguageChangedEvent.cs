using System.Globalization;
using Robust.Shared.Serialization;

namespace Content.Shared.Localizations;

/// <summary>
/// Оповещение клиентских систем о смене языка.
/// Шлётся через EventBus (EventSource.Local) при переключении культуры.
/// </summary>
[Serializable, NetSerializable]
public sealed class LanguageChangedEvent : EntityEventArgs
{
    public readonly string OldCultureCode;
    public readonly string NewCultureCode;

    public LanguageChangedEvent(string oldCultureCode, string newCultureCode)
    {
        OldCultureCode = oldCultureCode;
        NewCultureCode = newCultureCode;
    }

    public LanguageChangedEvent(CultureInfo oldCulture, CultureInfo newCulture)
    {
        OldCultureCode = oldCulture.Name;
        NewCultureCode = newCulture.Name;
    }
}
