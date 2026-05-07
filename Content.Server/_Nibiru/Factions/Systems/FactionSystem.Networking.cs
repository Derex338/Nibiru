using Content.Shared._Nibiru.Factions;
using Robust.Shared.GameObjects;

namespace Content.Server._Nibiru.Factions;

public sealed partial class FactionSystem
{
    private void InitializeNetworking()
    {
        SubscribeNetworkEvent<FactionCreateRequestMessage>(OnFactionCreateRequest);
        SubscribeNetworkEvent<FactionStateRequestMessage>(OnFactionStateRequest);
        SubscribeNetworkEvent<NibiruFactionLeaderPrefsMessage>(OnFactionLeaderPrefs);

        SubscribeNetworkEvent<HeirChooseMessage>(OnHeirChoose);
        SubscribeNetworkEvent<FactionTitleTransferMessage>(OnTitleTransfer);
        SubscribeNetworkEvent<FactionLeaveMessage>(OnLeaveFaction);
        SubscribeNetworkEvent<FactionDeleteMessage>(OnDeleteFaction);
        SubscribeNetworkEvent<FactionKickMemberMessage>(OnKickMemberFaction);

        SubscribeNetworkEvent<FactionChangeStateMessage>(OnFactionStateChange);
        SubscribeNetworkEvent<FactionChangeMemberRankMessage>(OnChangeMemberRank);
        SubscribeNetworkEvent<FactionMoveMemberMessage>(OnMoveMember);
        SubscribeNetworkEvent<FactionCreateRoleMessage>(OnCreateRole);
        SubscribeNetworkEvent<FactionDeleteRoleMessage>(OnDeleteRole);
    }
}
