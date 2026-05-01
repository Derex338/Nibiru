using System.Numerics;
using Content.Server.NPC.Systems;
// Obsolete root using removed
using Content.Shared._Nibiru.NPC.Behavior;
using Content.Shared._Nibiru.NPC.Commands;
using Content.Shared._Nibiru.NPC.Training;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Nibiru.NPC.Systems.Behavior;

/// <summary>
/// Обрабатывает специальные способности животных:
/// охрана (рычание), поиск по запаху, доставка писем, охота на вредителей.
/// </summary>
public sealed class NibiruAnimalAbilitySystem : EntitySystem
{
    [Dependency] private readonly NPCSteeringSystem _steering = default!;
    [Dependency] private readonly NpcFactionSystem _faction = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<NibiruAnimalAbilityComponent, NibiruTamableComponent, ActiveNPCComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var ability, out var tamable, out _, out var xform))
        {
            if (!tamable.IsTamed)
                continue;

            // Кулдаун способностей
            if (ability.CooldownAccumulator > 0)
            {
                ability.CooldownAccumulator -= frameTime;
                continue;
            }

            foreach (var abilityType in ability.Abilities)
            {
                switch (abilityType)
                {
                    case AnimalAbilityType.Guard:
                        ProcessGuard(uid, ability, tamable, xform);
                        break;
                    case AnimalAbilityType.PestControl:
                        ProcessPestControl(uid, ability, xform);
                        break;
                    case AnimalAbilityType.Deliver:
                        ProcessDelivery(uid, ability, xform);
                        break;
                }
            }
        }
    }

    /// <summary>
    /// Охранная способность: рычит при приближении чужаков к хозяину.
    /// </summary>
    private void ProcessGuard(EntityUid uid, NibiruAnimalAbilityComponent ability,
        NibiruTamableComponent tamable, TransformComponent xform)
    {
        if (tamable.OwnerUid == null || !EntityManager.EntityExists(tamable.OwnerUid.Value))
            return;

        var ownerXform = Transform(tamable.OwnerUid.Value);
        var myPos = _xform.GetMapCoordinates((uid, xform));

        foreach (var entity in _lookup.GetEntitiesInRange<NpcFactionMemberComponent>(myPos, ability.GuardRadius))
        {
            if (entity.Owner == uid || entity.Owner == tamable.OwnerUid)
                continue;

            // Рычим на не-дружественных
            if (!_faction.IsEntityFriendly(uid, entity.Owner))
            {
                // Воспроизводим звук рычания
                // (в будущем можно подставить конкретный звуковой ассет)
                ability.CooldownAccumulator = ability.AbilityCooldown;
                RaiseLocalEvent(uid, new AnimalGrowlEvent(entity.Owner));
                return;
            }
        }
    }

    /// <summary>
    /// Охота на вредителей: кошки ловят мышей и тараканов.
    /// </summary>
    private void ProcessPestControl(EntityUid uid, NibiruAnimalAbilityComponent ability, TransformComponent xform)
    {
        var myPos = _xform.GetMapCoordinates((uid, xform));

        // Ищем вредителей (NPC с тегом Pest или маленьких существ)
        var nearbyEntities = new HashSet<EntityUid>();
        _lookup.GetEntitiesInRange(uid, ability.SearchRadius * 0.5f, nearbyEntities);

        foreach (var nearby in nearbyEntities)
        {
            if (nearby == uid)
                continue;

            // Проверяем, что это вредитель (по мета-данным)
            if (!TryComp<MetaDataComponent>(nearby, out var meta) || meta.EntityPrototype == null)
                continue;

            var protoId = meta.EntityPrototype.ID;
            if (protoId.Contains("Mouse") || protoId.Contains("Cockroach") || protoId.Contains("Pest"))
            {
                // Преследуем и атакуем вредителя через поведенческую систему
                if (TryComp<NibiruNpcBehaviorComponent>(uid, out var behavior))
                {
                    behavior.CurrentTarget = nearby;
                    behavior.CurrentState = NibiruNpcState.Chasing;
                    ability.CooldownAccumulator = ability.AbilityCooldown;
                    return;
                }
            }
        }
    }

    /// <summary>
    /// Доставка предметов: птица несёт предмет к указанной цели.
    /// </summary>
    private void ProcessDelivery(EntityUid uid, NibiruAnimalAbilityComponent ability, TransformComponent xform)
    {
        if (ability.CarriedItem == null || !EntityManager.EntityExists(ability.CarriedItem.Value))
            return;

        // Доставка управляется командой Deliver через NibiruTamingSystem.GiveCommand
        // Здесь проверяем только прибытие к цели
        if (TryComp<NibiruNpcBehaviorComponent>(uid, out var behavior) &&
            behavior.CurrentTarget != null &&
            behavior.CurrentState == NibiruNpcState.Following)
        {
            var targetXform = Transform(behavior.CurrentTarget.Value);
            if (xform.Coordinates.TryDistance(EntityManager, targetXform.Coordinates, out var dist) && dist < 1.5f)
            {
                // Дропаем предмет рядом с целью
                var itemXform = Transform(ability.CarriedItem.Value);
                _xform.SetCoordinates(ability.CarriedItem.Value, targetXform.Coordinates);
                ability.CarriedItem = null;
                ability.CooldownAccumulator = ability.AbilityCooldown;

                behavior.CurrentState = NibiruNpcState.Returning;
            }
        }
    }

    /// <summary>
    /// Даёт птице предмет для доставки к указанной цели.
    /// </summary>
    public bool StartDelivery(EntityUid bird, EntityUid item, EntityUid target)
    {
        if (!TryComp<NibiruAnimalAbilityComponent>(bird, out var ability))
            return false;

        if (!ability.Abilities.Contains(AnimalAbilityType.Deliver))
            return false;

        if (!ability.CanCarryItem || ability.CarriedItem != null)
            return false;

        var birdXform = Transform(bird);
        var targetXform = Transform(target);

        if (!birdXform.Coordinates.TryDistance(EntityManager, targetXform.Coordinates, out var dist))
            return false;

        if (dist > ability.DeliveryRange)
            return false;

        ability.CarriedItem = item;

        // Прячем предмет "в лапках" (перемещаем в контейнер или привязываем)
        _xform.SetParent(item, bird);

        if (TryComp<NibiruNpcBehaviorComponent>(bird, out var behavior))
        {
            behavior.CurrentTarget = target;
            behavior.CurrentState = NibiruNpcState.Following;
        }

        return true;
    }
}

/// <summary>
/// Событие рычания/предупреждения животным при приближении чужака.
/// </summary>
public sealed class AnimalGrowlEvent : EntityEventArgs
{
    public EntityUid IntruderUid;

    public AnimalGrowlEvent(EntityUid intruder)
    {
        IntruderUid = intruder;
    }
}
