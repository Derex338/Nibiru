using Content.Server.EUI;
using Content.Server.GameTicking.Rules;
using Content.Server.Popups;
using Content.Shared.Eui;
using Content.Shared.IdentityManagement;
using Content.Shared.Popups;
using Content.Shared._Nibiru.Key;
using Content.Shared._Nibiru.Lock;

namespace Content.Server._Nibiru.Key.UI;

// From Reserve
public sealed class KeyCodeSetEui(EntityUid target, EntityUid user, DoorLockSystem lockSystem, PopupSystem popup, EntityManager entManager) : BaseEui
{
    public override EuiStateBase GetNewState()
    {
        return new KeyCodeState();
    }

    public override void Opened()
    {
        base.Opened();
        StateDirty();
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        if (msg is KeyCodeSetMessage consent)
        {
            if (entManager.TryGetComponent<KeyComponent>(target, out var key) && key.LockCode != 0
                || entManager.TryGetComponent<DoorLockComponent>(target, out var doorLock) && doorLock.LockCode != 0)
            {
                return;
            }

            if (consent.IsCodeSet && consent.Code != 0)
            {
                lockSystem.OnAccept(target, user, consent.Code);
            }
            else
            {
                // Announce that convert failed
                popup.PopupEntity(
                    Loc.GetString("key-lock-popup-code-not-saved", ("target", Identity.Entity(target, entManager))),
                    target,
                    user,
                    PopupType.LargeCaution);
            }
        }

        Close();
    }
}
