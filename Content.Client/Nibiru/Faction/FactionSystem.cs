using Robust.Shared.Timing;
using Robust.Client.Player;

namespace Content.Client.Nibiru.Faction;

public sealed class FactionSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        //SubscribeNetworkEvent<FactionUpdateMessage>(OnFactionUpdate);
    }
}
