

using Robust.Shared.Player;

namespace Content.Shared._Nibiru.Factions;

public sealed class SharedFactionSystem : EntitySystem
{

    public bool OnFactionStateRequest(ICommonSession session, bool CreatorCheck)
    {
        var player = session.AttachedEntity;

        if (!player.HasValue)
            return false;

        if (EntityManager.TryGetComponent<FactionComponent>(player, out var factionComponent))
        {
            if(CreatorCheck)
                return factionComponent.IsCreator;
            else if (!CreatorCheck)
                return !factionComponent.IsCreator;
        }

        return false;
    }
}
