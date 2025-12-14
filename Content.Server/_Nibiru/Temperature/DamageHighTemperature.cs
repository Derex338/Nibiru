using Content.Server.Mind;
using Content.Server.Popups;
using Content.Shared.Administration.Logs;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.Hands.Components;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Temperature.Components;
using Content.Shared.Throwing;
using NetCord;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Nibiru.Temperature;

public sealed class DamageHighTemperature : EntitySystem
{
    [Dependency] private readonly DamageOnInteractSystem _damage = default!;
    [Dependency] private readonly InventorySystem _inventorySystem = default!;
    [Dependency] private readonly DamageableSystem _damageableSystem = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private readonly SharedAudioSystem _audioSystem = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TemperatureComponent, InteractHandEvent>(OnHighTempInteract);
        SubscribeLocalEvent<TemperatureComponent, ContainerGettingInsertedAttemptEvent>(CheckTemperature);
        //SubscribeLocalEvent<TemperatureComponent, ContainerIsRemovingAttemptEvent>(OnContainerRemove);
    }

    private void OnHighTempInteract(EntityUid uid, TemperatureComponent comp, InteractHandEvent args)
    {
        if (TryComp<DamageOnInteractComponent>(uid, out var damageComp))
        {
            if (comp.CurrentTemperature > 300)
                _damage.SetIsDamageActiveTo((uid, damageComp), true);
            else
                _damage.SetIsDamageActiveTo((uid, damageComp), false);
        }
    }

    //private void OnContainerRemove(EntityUid uid, TemperatureComponent comp, ContainerIsRemovingAttemptEvent args)
    //{
    //    if (args.Cancelled)
    //        return;

    //    if (_mind.TryGetMind(args.Container.Owner, out var mind, out var _))
    //        return;

    //    if (TryComp<TemperatureComponent>(args.Container.Owner, out var tempComp))
    //    {
    //        if (tempComp.CurrentTemperature > 300)
    //        {
    //            args.Cancel();
    //            _popup.PopupEntity(Loc.GetString("powered-light-component-burn-hand"), args.Container.Owner);
    //        }
    //    }
    //    else if (TryComp<TemperatureComponent>(args.EntityUid, out var itemTempComp))
    //    {
    //        if (itemTempComp.CurrentTemperature > 300)
    //        {
    //            args.Cancel();
    //            _popup.PopupEntity(Loc.GetString("powered-light-component-burn-hand"), args.Container.Owner);
    //        }
    //    }
    //}

    private void CheckTemperature(EntityUid uid, TemperatureComponent comp, ContainerGettingInsertedAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        var User = args.Container.Owner;

        if (comp.CurrentTemperature > 300)
        {
            if (!_proto.TryIndex<DamageTypePrototype>("Heat", out var damageProto))
                return;
            var totalDamage = new DamageSpecifier(damageProto, comp.CurrentTemperature / 100);

            // try to get damage on interact protection from either the inventory slots of the entity
            if (_inventorySystem.TryGetInventoryEntity<DamageOnInteractProtectionComponent>(User, out var protectiveEntity))
                return;

            // or checking the entity for  the comp itself if the inventory didn't work
            if (protectiveEntity.Comp == null && TryComp<DamageOnInteractProtectionComponent>(User, out var protectiveComp))
                protectiveEntity = (User, protectiveComp);


            // if protectiveComp isn't null after all that, it means the user has protection,
            // so let's calculate how much they resist
            if (protectiveEntity.Comp != null)
            {
                totalDamage = DamageSpecifier.ApplyModifierSet(totalDamage, protectiveEntity.Comp.DamageProtection);
            }

            totalDamage = _damageableSystem.TryChangeDamage(User, totalDamage);

            if (totalDamage != null && totalDamage.AnyPositive())
            {
                args.Cancel();

                _transform.SetCoordinates(uid, Transform(User).Coordinates);
                _transform.AttachToGridOrMap(uid);
                _throwing.TryThrow(uid, _random.NextVector2(), baseThrowSpeed: 5f);

                var sound = new SoundPathSpecifier("/Audio/Effects/lightburn.ogg");
                _audioSystem.PlayPredicted(sound, User, User);
                _popup.PopupEntity(Loc.GetString("powered-light-component-burn-hand"), User, User);

                _adminLogger.Add(LogType.Damaged, $"{ToPrettyString(User):user} injured their hand by interacting with {ToPrettyString(uid):target} and received {totalDamage.GetTotal():damage} damage");
            }
        }
    }
}
