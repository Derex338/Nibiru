using Content.Server.EUI;
using Content.Server.GameTicking.Rules;
using Content.Server.Popups;
using Content.Shared.Eui;
using Content.Shared.IdentityManagement;
using Content.Shared.Popups;
using Content.Shared._Nibiru.Factions;
using Content.Shared._Nibiru.Factions.Messages;
using Content.Shared._Nibiru.Lock;
using Content.Shared._Nibiru.Key;

namespace Content.Server._Nibiru.Key;

public sealed class SetKeyEui(KeyComponent comp, DoorLockSystem lockSystem, EntityManager entManager) : BaseEui
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

        if (msg is KeyCodeMessage consent)
        {
            //lockSystem.OnAccept(target, converter);

            comp.LockCode = consent.Code;
        }

        Close();
    }
}
