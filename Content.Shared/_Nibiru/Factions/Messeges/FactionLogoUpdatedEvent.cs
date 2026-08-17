using Robust.Shared.GameObjects;

namespace Content.Shared._Nibiru.Factions;

/// <summary>
/// Called locally (on the client) when the faction logo texture has been regenerated.
/// </summary>
public sealed class FactionLogoUpdatedEvent : EntityEventArgs
{
    public string FactionName { get; }

    public FactionLogoUpdatedEvent(string factionName)
    {
        FactionName = factionName;
    }
}
