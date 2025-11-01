using Content.Server.EUI;
using Content.Server.GameTicking.Rules;
using Content.Server.Popups;
using Content.Shared.Eui;
using Content.Shared.IdentityManagement;
using Content.Shared.Popups;
using Content.Shared._Nibiru.Factions;
using Content.Shared._Nibiru.Factions.Messages;

namespace Content.Server._Nibiru.Factions.UI;

public sealed class FactionRequestedEui(EntityUid target, EntityUid converter, AddFactionVerb consRevSystem, PopupSystem popup, EntityManager entManager) : BaseEui
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

        if (msg is FactionJoinRequestMessage consent) //&& revRuleSystem.IsConvertable(target))
        {
            if (entManager.TryGetComponent<FactionComponent>(target, out var targetConsFact)
                || !entManager.TryGetComponent<FactionComponent>(converter, out var consFact))
            {
                return;
            }

            if (consent.IsAccepted)
            {
                // Make target a revolutionary
                //revRuleSystem.ConvertEntityToRevolution(target, converter);

                // Remove request
                //consRevSystem.CancelRequest((target, targetConsRev), (converter, consRev));

                // Apply cooldown to convertor
                //consRevSystem.ApplyConversionCooldown((converter, consRev));
				
				var targetComp = entManager.AddComponent<FactionComponent>(target);
				targetComp.FactionName = consFact.FactionName;
				if (consFact.ResearchServer is not null)
					targetComp.ResearchServer = consFact.ResearchServer;

                // Announce that convert was successful
                popup.PopupEntity(
                    Loc.GetString("ЗАПРОС ПРИНЯТ", ("target", Identity.Entity(target, entManager))),
                    target,
                    converter);
            }
            else
            {
                // Cancel request with cooldown
                //consRevSystem.CancelRequest((target, targetConsRev), (converter, consRev));

                // Apply conversion block to target
                //consRevSystem.ApplyConversionDeny((target, targetConsRev));

                // Announce that convert failed
                popup.PopupEntity(
                    Loc.GetString("ПОШЁЛ НАХУЙ", ("target", Identity.Entity(target, entManager))),
                    target,
                    converter,
                    PopupType.SmallCaution);
            }
        }

        Close();
    }
}