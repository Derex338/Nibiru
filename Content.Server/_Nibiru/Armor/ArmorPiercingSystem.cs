using System;
using Content.Shared._Nibiru.Armor.Components;
using Content.Shared._Nibiru.Weapon.Components;
using Content.Shared.Armor;
using Content.Shared.Clothing.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Events;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Inventory;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Weapons.Melee;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Server._Nibiru.Armor;

/// <summary>
/// Система обработки пробития брони перед применением урона
/// </summary>
public sealed partial class ArmorPenetrationSystem : EntitySystem
{
[Dependency] private DamageExamineSystem _damageExamine = default!;
[Dependency] private SharedHandsSystem _hands = default!;
[Dependency] private IPrototypeManager _proto = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ArmorProtectionComponent, InventoryRelayedEvent<DamageModifyEvent>>(OnDamageModify);
        SubscribeLocalEvent<ArmorProtectionComponent, ArmorExamineEvent>(OnArmorExamine);

        SubscribeLocalEvent<ArmorPenetrationComponent, DamageExamineEvent>(OnDamageExamine);
    }

    private void OnDamageModify(EntityUid uid, ArmorProtectionComponent component, InventoryRelayedEvent<DamageModifyEvent> args)
    {
        if (TryComp<MaskComponent>(uid, out var mask) && mask.IsToggled)
            return;

        ArmorPenetrationComponent? penetration = null;

        if (args.Args.Origin != null)
        {
            // Try origin itself first (projectiles, traps)
            if (!TryComp(args.Args.Origin, out penetration))
            {
                // Try hands if it's a mob
                if (HasComp<HandsComponent>(args.Args.Origin) && _hands.TryGetActiveItem(args.Args.Origin.Value, out var held))
                {
                    TryComp(held, out penetration);
                }
            }
        }

        foreach (var (damageType, damageValue) in args.Args.Damage.DamageDict)
        {
            if (!component.Protection.TryGetValue(damageType, out var protValue))
                continue;

            var penValue = 0f;
            if (penetration != null)
                penetration.Penetration.TryGetValue(damageType, out penValue);

            var effectiveProt = Math.Max(0, protValue - penValue);

            if (effectiveProt > damageValue)
            {
                args.Args.Damage.DamageDict[damageType] = damageValue * (damageValue / effectiveProt);
            }
        }
    }

    private void OnArmorExamine(Entity<ArmorProtectionComponent> ent, ref ArmorExamineEvent args)
    {
        foreach (var (damageType, protValue) in ent.Comp.Protection)
        {
            if (protValue <= 0)
                continue;

            args.Msg.PushNewline();
            var armorType = Loc.GetString("armor-damage-type-" + damageType.ToLower());
            args.Msg.AddMarkupOrThrow(Loc.GetString("armor-protect-value",
                ("type", armorType),
                ("value", protValue)
            ));
        }
    }

    private void OnDamageExamine(EntityUid uid, ArmorPenetrationComponent component, ref DamageExamineEvent args)
    {
        DamageSpecifier damageValue = new();
        foreach (var (damageType, penValue) in component.Penetration)
        {
            if (penValue <= 0 || !_proto.TryIndex<DamageTypePrototype>(damageType, out var prot))
                continue;

            damageValue += new DamageSpecifier(prot, penValue);
        }
        _damageExamine.AddDamageExamine(args.Message, damageValue, Loc.GetString("armor-penetration-verb"));
    }
}

