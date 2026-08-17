using Content.Server.EUI;
using Content.Shared.Eui;
using Content.Shared._Nibiru.Factions;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Player;

namespace Content.Server._Nibiru.Factions.UI;

public sealed partial class FactionStatueSelectionEui : BaseEui
{
    [Dependency] private IEntityManager _entityManager = default!;

    private readonly EntityUid _statueUid;

    public FactionStatueSelectionEui(EntityUid statueUid)
    {
        _statueUid = statueUid;
    }

    public override EuiStateBase GetNewState()
    {
        if (!_entityManager.TryGetComponent<FactionStatueComponent>(_statueUid, out var statue))
            return new FactionStatueSelectionState();

        return new FactionStatueSelectionState
        {
            StatueEntity = _entityManager.GetNetEntity(_statueUid),
            FactionName = statue.FactionName,
            AllMembers = new List<FactionMemberRecord>(statue.AllMembers)
        };
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        if (msg is FactionStatueSelectMessage select)
        {
            if (!_entityManager.TryGetComponent<FactionStatueComponent>(_statueUid, out var statue))
                return;

            statue.SelectedMember = select.SelectedMember;

            var member = statue.AllMembers.Find(m => m.Entity == select.SelectedMember);
            statue.SelectedMemberName = member.Name;

            _entityManager.Dirty(_statueUid, statue);
        }

        Close();
    }
}
