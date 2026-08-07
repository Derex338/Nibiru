using Robust.Shared.Timing;
using Robust.Client.Player;

namespace Content.Client._Nibiru.Faction;

public sealed partial class FactionSystem : EntitySystem
{

    public override void Initialize()
    {
        base.Initialize();

        //SubscribeNetworkEvent<FactionUpdateMessage>(OnFactionUpdate);
    }
}
