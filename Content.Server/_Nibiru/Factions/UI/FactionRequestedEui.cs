using Content.Server.EUI;
using Content.Server.GameTicking.Rules;
using Content.Server.Popups;
using Content.Shared.Eui;
using Content.Shared.IdentityManagement;
using Content.Shared.Popups;
using Content.Shared._Nibiru.Factions;
using Content.Shared._Nibiru.Factions.Messages;

namespace Content.Server._Nibiru.Factions.UI;

//Код взят с Reserv'а и изменён под фракции
public sealed class FactionRequestedEui(EntityUid target, EntityUid converter, AddFactionVerb factionVerbSystem, PopupSystem popup, EntityManager entManager) : BaseEui
{
    public override EuiStateBase GetNewState()
    {
        return new FactionJoinState(Identity.Name(converter, entManager));
    }

    public override void Opened()
    {
        base.Opened();
        StateDirty();
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        if (msg is FactionJoinRequestMessage consent)
        {
            if (entManager.TryGetComponent<FactionComponent>(target, out var targetConsFact)
                || !entManager.TryGetComponent<FactionComponent>(converter, out var consFact))
            {
                return;
            }

            if (consent.IsAccepted)
            {
                // Announce that convert was successful
                popup.PopupEntity(
                    Loc.GetString("Запрос принят", ("target", Identity.Entity(target, entManager))),
                    target,
                    converter);

                factionVerbSystem.OnAccept(target, converter);
            }
            else
            {
                // Announce that convert failed
                popup.PopupEntity(
                    Loc.GetString("Запрос отклонили", ("target", Identity.Entity(target, entManager))),
                    target,
                    converter,
                    PopupType.SmallCaution);
            }
        }

        Close();
    }
}
