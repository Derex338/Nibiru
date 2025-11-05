using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Player;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Prototypes;
using Content.Server.Mind;
using Content.Server.Popups;
using Content.Server.EUI;
using Content.Shared.IdentityManagement;
using System.Linq;
using Content.Shared.ActionBlocker;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.Doors;
using Content.Shared._Nibiru.Lock;
using Content.Shared._Nibiru.Key;
using Robust.Shared.Serialization.Manager;
using Content.Server.Administration.Logs;
using Content.Shared.Item;
using Content.Shared.Database;

namespace Content.Server._Nibiru.Factions;

    public sealed class DoorLockSystem : EntitySystem
{	
	[Dependency] private readonly IPrototypeManager _prototypeManager = default!;
	[Dependency] private readonly PopupSystem _popup = default!;
	[Dependency] private readonly SharedDoAfterSystem _doAfterSystem = default!;
	[Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;
	[Dependency] private readonly ISerializationManager _serMan = default!;
	[Dependency] private readonly IAdminLogManager _adminLog = default!;
	
    public override void Initialize()
    {
        base.Initialize();
			
        SubscribeLocalEvent<InteractUsingEvent>(CreateLock);
		
		SubscribeLocalEvent<DoorLockComponent, InteractUsingEvent>(TryUnlock);
		
		SubscribeLocalEvent<DoorLockComponent, BeforeDoorOpenedEvent>(OnBeforeDoorOpen);
        SubscribeLocalEvent<DoorLockComponent, BeforeDoorClosedEvent>(OnBeforeDoorClose);
		
		SubscribeLocalEvent<DoorLockComponent, LockPickDoAfter>(OnDoAfterLock);
    }  
	
	private void CreateLock(InteractUsingEvent args)
    {
        if(args.Handled 
		|| TryComp<DoorLockComponent>(args.Target, out var comp)
		|| !TryComp<DoorComponent>(args.Target, out var door))
            return;

        if (TryComp<DoorLockComponent>(args.Used, out var DoorLock) && !TryComp<DoorLockComponent>(args.Target, out var target))
        {
            target = EntityManager.AddComponent<DoorLockComponent>(args.Target);
			
			_serMan.CopyTo(DoorLock, ref target, notNullableOverride: true);
			
			EntityManager.DeleteEntity(args.Used);
			
			_adminLog.Add(LogType.Action, LogImpact.Low, $"{ToPrettyString(args.User):player} повесил замок на {ToPrettyString(args.Target)}");
			
			Dirty(args.Target, target);
			
			args.Handled = true;
        }
    }
	
	private void TryUnlock(EntityUid uid, DoorLockComponent component, InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if(TryComp<KeyComponent>(args.Used, out var Key)
		&& Key.LockCode == component.LockCode)
        {	
			component.Locked = !component.Locked;
			args.Handled = true;
        }
		
		if(TryComp<LockPickComponent>(args.Used, out var LockPick))
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
        }
    }

    private void OnBeforeDoorClose(EntityUid uid, DoorLockComponent comp, BeforeDoorClosedEvent args)
    {
        if (comp.Locked)
        {
            args.Cancel();
        }
    }
	
	private void OnDoAfterLock(EntityUid uid, DoorLockComponent component, LockPickDoAfter args)
    {
        if (args.Cancelled)
            return;

        component.Locked = !component.Locked;
    }
}
