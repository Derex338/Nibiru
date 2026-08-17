using Robust.Shared.Timing;
using Robust.Client.Player;

namespace Content.Client._Nibiru.Faction;

public sealed partial class FactionSystem : EntitySystem
{
    [Dependency] private IGameTiming _gameTiming = default!;
    [Dependency] private IPlayerManager _playerManager = default!;

    public override void Initialize()
    {
        base.Initialize();
    }
}
