using Robust.Shared.GameObjects;

namespace Content.Shared._Nibiru.Factions;

/// <summary>
/// Вызывается локально (на клиенте), когда текстура логотипа фракции была перегенерирована.
/// </summary>
public sealed class FactionLogoUpdatedEvent : EntityEventArgs
{
    public string FactionName { get; }

    public FactionLogoUpdatedEvent(string factionName)
    {
        FactionName = factionName;
    }
}
