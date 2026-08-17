using System.Globalization;
using Robust.Shared.Serialization;

namespace Content.Shared.Localizations;

/// <summary>
/// Notification to client systems about language change.
/// Sends via EventBus (EventSource.Local) when switching culture.
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
