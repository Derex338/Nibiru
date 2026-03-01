using Robust.Shared.Serialization.Manager;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Doors.Components;
using Content.Shared.Doors;
using Content.Shared._Nibiru.Lock;
using Content.Shared._Nibiru.Key;
using Content.Server.Administration.Logs;
using Content.Shared.Database;
using Robust.Server.Audio;
using Content.Server.Instruments;
using Content.Shared.Tools.Components;
using Content.Server.Tools;
using Content.Server.EUI;
using Robust.Shared.Player;
using Content.Server.Mind;
using Content.Shared._Nibiru.Factions;
using Content.Server._Nibiru.Factions.UI;
using Content.Server.Popups;
using Content.Server._Nibiru.Key.UI;
using Content.Server.DoAfter;

namespace Content.Server._Nibiru.Key;

public sealed class DoorLockSystem : EntitySystem
{
    [Dependency] private readonly SharedDoAfterSystem _doAfterSystem = default!;
    [Dependency] private readonly ISerializationManager _serMan = default!;
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly ToolSystem _tool = default!;
    [Dependency] private readonly EuiManager _eui = default!;
    [Dependency] private readonly ISharedPlayerManager _player = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly PopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<InteractUsingEvent>(CreateLock);
        SubscribeLocalEvent<DoorLockComponent, InteractUsingEvent>(TryUnlock);
        SubscribeLocalEvent<KeyComponent, InteractUsingEvent>(TryKey);

        SubscribeLocalEvent<DoorLockComponent, BeforeDoorOpenedEvent>(OnBeforeDoorOpen);
        SubscribeLocalEvent<DoorLockComponent, BeforeDoorClosedEvent>(OnBeforeDoorClose);

        SubscribeLocalEvent<DoorLockComponent, LockPickDoAfter>(OnDoAfterLock);
        SubscribeLocalEvent<DoorLockComponent, KeyCodeSetEvent>(SetCodeDoor);
        SubscribeLocalEvent<KeyComponent, KeyCodeSetEvent>(SetCodeKey);
    }

    private void CreateLock(InteractUsingEvent args)
    {
        if (args.Handled
        || TryComp<DoorLockComponent>(args.Target, out var comp)
        || !TryComp<DoorComponent>(args.Target, out var door))
            return;

        if (TryComp<DoorLockComponent>(args.Used, out var DoorLock) && !TryComp<DoorLockComponent>(args.Target, out var target))
        {
            target = AddComp<DoorLockComponent>(args.Target);
            _serMan.CopyTo(DoorLock, ref target, notNullableOverride: true);

            QueueDel(args.Used);
            _adminLog.Add(LogType.Action, LogImpact.Low, $"{ToPrettyString(args.User):player} повесил замок на {ToPrettyString(args.Target)}");
            Dirty(args.Target, target);

            if (target.LockSound != null)
                _audio.PlayPvs(target.LockSound, args.Target);

            args.Handled = true;
        }
    }

    private void TryUnlock(EntityUid uid, DoorLockComponent component, InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (component.LockCode == 0 && TryComp<ToolComponent>(args.Used, out var tool) && _tool.HasQuality(args.Used, "Rasp", tool))
        {
            SetCode(args.Target, args.User);
            return;
        }

        if (TryComp<KeyComponent>(args.Used, out var Key)
        && Key.LockCode == component.LockCode)
        {
            component.Locked = !component.Locked;
            args.Handled = true;

            if (component.UnlockSound != null)
                _audio.PlayPvs(component.UnlockSound, uid);

            return;
        }

        if (TryComp<LockPickComponent>(args.Used, out var LockPick))
        {
            _doAfterSystem.TryStartDoAfter(
                new DoAfterArgs(EntityManager, args.User, component.CrackDuration, new LockPickDoAfter(), uid, uid)
                {
                    BreakOnDamage = true,
                    BreakOnMove = true,
                    BreakOnDropItem = false,
                });

            args.Handled = true;
        }
    }

    private void OnBeforeDoorOpen(EntityUid uid, DoorLockComponent comp, BeforeDoorOpenedEvent args)
    {
        if (comp.Locked)
        {
            args.Cancel();

            if (comp.CantOpenSound != null)
                _audio.PlayPvs(comp.CantOpenSound, uid);
        }
    }

    private void OnBeforeDoorClose(EntityUid uid, DoorLockComponent comp, BeforeDoorClosedEvent args)
    {
        if (comp.Locked)
        {
            args.Cancel();

            if (comp.CantOpenSound != null)
                _audio.PlayPvs(comp.CantOpenSound, uid);
        }
    }

    private void OnDoAfterLock(EntityUid uid, DoorLockComponent component, LockPickDoAfter args)
    {
        if (args.Cancelled)
            return;

        component.Locked = !component.Locked;
    }

    private void TryKey(EntityUid uid, KeyComponent component, InteractUsingEvent args)
    {
        if (args.Handled || component.LockCode != 0)
            return;

        if (TryComp<ToolComponent>(args.Used, out var tool) && _tool.HasQuality(args.Used, "Rasp", tool))
            SetCode(args.Target, args.User);
    }

    private void SetCode(EntityUid code, EntityUid user)
    {
        if (_mind.TryGetMind(user, out var consentMindId, out var mind) &&
                _player.TryGetSessionById(mind.UserId, out var session))
        {
            var window = new KeyCodeSetEui(code, user, this, _popup, EntityManager);

            _eui.OpenEui(window, session);
        }
    }

    public void OnAccept(EntityUid target, EntityUid user, int code)
    {
        _doAfterSystem.TryStartDoAfter(
            new DoAfterArgs(EntityManager, user, 3, new KeyCodeSetEvent(code), target)
            {
                BreakOnDamage = true,
                BreakOnMove = true,
                NeedHand = true,
            });
    }

    private void SetCodeKey(EntityUid uid, KeyComponent comp, KeyCodeSetEvent args)
    {
        if (args.Cancelled)
            return;

        comp.LockCode = args.Code;
        Dirty(uid, comp);
    }
    private void SetCodeDoor(EntityUid uid, DoorLockComponent comp, KeyCodeSetEvent args)
    {
        if (args.Cancelled)
            return;

        comp.LockCode = args.Code;
        Dirty(uid, comp);
    }
}
