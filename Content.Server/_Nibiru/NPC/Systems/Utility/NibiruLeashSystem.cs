using System.Numerics;
using Content.Server.NPC.Systems;
using Content.Server.Stack;
// Obsolete root using removed
using Content.Shared._Nibiru.NPC.Behavior.Components;
using Content.Shared._Nibiru.NPC.Utility;
using Content.Shared._Nibiru.NPC.Training;
using Content.Shared._Nibiru.NPC.Livestock;
using Content.Server._Nibiru.NPC.Systems.Behavior;
using Content.Shared.Alert;
using Content.Shared.Interaction;
using Content.Shared.Movement.Systems;
using Content.Shared.NPC;
using Content.Shared.Popups;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Audio.Systems;
using Content.Shared._Nibiru.NPC.Behavior;

namespace Content.Server._Nibiru.NPC.Systems.Utility;

/// <summary>
/// Обрабатывает привязывание животных верёвкой и ведение за собой.
/// Блокирует стандартное перетаскивание для тяжёлых животных.
/// </summary>
public sealed class NibiruLeashSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SharedJointSystem _joints = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly StackSystem _stack = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly NPCSteeringSystem _steering = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NibiruLeashableComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<NibiruLeashAnchorComponent, InteractUsingEvent>(OnAnchorInteractUsing);
        SubscribeLocalEvent<NibiruLeashHolderComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovementSpeed);
    }

    private void OnAnchorInteractUsing(EntityUid uid, NibiruLeashAnchorComponent component, InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        // Проверяем, что предмет — верёвка
        if (!TryComp<MetaDataComponent>(args.Used, out var meta) || meta.EntityPrototype == null)
            return;

        var protoId = meta.EntityPrototype.ID;
        if (!protoId.Contains("Rope") && !protoId.Contains("Leash"))
            return;

        // Если игрок уже ведёт животное, привязываем его к столбику
        if (TryComp<NibiruLeashHolderComponent>(args.User, out var holder) && holder.LeashedAnimal.Valid)
        {
            if (TryComp<NibiruLeashableComponent>(holder.LeashedAnimal, out var leasable))
            {
                LeashTo(holder.LeashedAnimal, uid, leasable, args.Used);
                _popup.PopupEntity(Loc.GetString("nibiru-leash-attached-to-anchor", ("animal", holder.LeashedAnimal)), uid, args.User);
                args.Handled = true;
            }
        }
    }

    private void OnInteractUsing(EntityUid uid, NibiruLeashableComponent component, InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        // Проверяем, что предмет — верёвка
        if (!TryComp<MetaDataComponent>(args.Used, out var meta) || meta.EntityPrototype == null)
            return;

        var protoId = meta.EntityPrototype.ID;
        if (!protoId.Contains("Rope") && !protoId.Contains("Leash"))
            return;

        // Если животное уже привязано
        if (component.IsLeashed)
        {
            // Если игрок кликает верёвкой по уже привязанному к нему животному — отвязываем
            if (component.LeashedTo == args.User)
            {
                Unleash(uid, component);
                _popup.PopupEntity(Loc.GetString("nibiru-leash-detached", ("animal", uid)), args.User, args.User);
            }
            else
            {
                // Перехватываем верёвку (не тратим её)
                LeashTo(uid, args.User, component, null);
                _popup.PopupEntity(Loc.GetString("nibiru-leash-attached", ("animal", uid)), args.User, args.User);
            }
        }
        else
        {
            // Если не привязано — привязываем к игроку (не тратим верёвку)
            LeashTo(uid, args.User, component, null);
            _popup.PopupEntity(Loc.GetString("nibiru-leash-attached", ("animal", uid)), args.User, args.User);
        }

        args.Handled = true;
    }

    private void OnRefreshMovementSpeed(EntityUid uid, NibiruLeashHolderComponent component, RefreshMovementSpeedModifiersEvent args)
    {
        args.ModifySpeed(component.WalkSpeedModifier, component.SprintSpeedModifier);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<NibiruLeashableComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var leash, out var xform))
        {
            if (!leash.IsLeashed || leash.LeashedTo == null)
                continue;

            if (!EntityManager.EntityExists(leash.LeashedTo.Value))
            {
                Unleash(uid, leash);
                continue;
            }

            if (TryComp<TransformComponent>(leash.LeashedTo.Value, out var targetXform))
            {
                if (xform.Coordinates.TryDistance(EntityManager, targetXform.Coordinates, out var dist))
                {
                    var isAnchor = HasComp<NibiruLeashAnchorComponent>(leash.LeashedTo.Value);

                    // Физическое ограничение: если животное слишком далеко от колышка, принудительно возвращаем его
                    if (isAnchor && dist > leash.LeashLength)
                    {
                        var animalPos = _xform.GetMapCoordinates(uid);
                        var targetPosCoords = _xform.GetMapCoordinates(leash.LeashedTo.Value);
                        var dir = animalPos.Position - targetPosCoords.Position;
                        if (dir.LengthSquared() > 0.01f)
                        {
                            var newPos = targetPosCoords.Position + dir.Normalized() * leash.LeashLength;
                            _xform.SetWorldPosition(uid, newPos);
                            _steering.Unregister(uid); // Сбрасываем текущий путь, так как он вывел за пределы
                        }
                    }

                    // Шанс обрыва
                    if (dist > leash.LeashLength * 1.5f)
                    {
                        var holder = leash.LeashedTo.Value;
                        Unleash(uid, leash, true); // true = broken
                        _popup.PopupEntity(Loc.GetString("nibiru-leash-broke-free", ("animal", uid)), holder, holder, PopupType.LargeCaution);
                        continue;
                    }
                }
            }

            // Попытка вырваться (для неприрученных)
            if (leash.TryingToBreakFree)
            {
                leash.BreakFreeAccumulator += frameTime;
                if (leash.BreakFreeAccumulator >= leash.BreakFreeInterval)
                {
                    leash.BreakFreeAccumulator = 0f;

                    // У столбика вырваться в 2 раза сложнее
                    var chance = leash.BreakFreeChance;
                    if (HasComp<NibiruLeashAnchorComponent>(leash.LeashedTo.Value))
                        chance *= 0.5f;

                    if (_random.Prob(chance))
                    {
                        var holder = leash.LeashedTo.Value;
                        Unleash(uid, leash, true);
                        _popup.PopupEntity(Loc.GetString("nibiru-leash-broke-free", ("animal", uid)), holder, holder, PopupType.LargeCaution);
                    }
                }
            }
        }
    }

    private void LeashTo(EntityUid animal, EntityUid target, NibiruLeashableComponent component, EntityUid? usedItem = null)
    {
        // Расходуем верёвку, если она используется
        if (usedItem != null && TryComp<MetaDataComponent>(usedItem.Value, out var meta))
        {
            if (!_stack.TryUse(usedItem.Value, 1))
                return;

            component.RopePrototype = meta.EntityPrototype?.ID;
        }

        // Отвязываем от предыдущего
        if (component.IsLeashed && component.LeashedTo != null)
        {
            if (TryComp<NibiruLeashHolderComponent>(component.LeashedTo.Value, out var oldHolder))
            {
                RemComp<NibiruLeashHolderComponent>(component.LeashedTo.Value);
                _movementSpeed.RefreshMovementSpeedModifiers(component.LeashedTo.Value);
                _alerts.ClearAlert(component.LeashedTo.Value, "Pulling");
            }
        }

        component.IsLeashed = true;
        component.LeashedTo = target;

        // Если привязываем к игроку (не к столбику)
        if (!HasComp<NibiruLeashAnchorComponent>(target))
        {
            var holder = EnsureComp<NibiruLeashHolderComponent>(target);
            holder.LeashedAnimal = animal;
            _movementSpeed.RefreshMovementSpeedModifiers(target);
            _alerts.ShowAlert(target, "Pulling");

            // Переопределяем поведение — следуем за держателем
            if (TryComp<NibiruNpcStateMachineComponent>(animal, out var state))
            {
                state.CurrentTarget = target;
                state.CurrentState = NibiruNpcState.Following;
            }
        }
        else
        {
            // Если привязываем к столбику, животное остается около столбика (Idle)
            if (TryComp<NibiruNpcStateMachineComponent>(animal, out var state))
            {
                state.CurrentTarget = null;
                state.CurrentState = NibiruNpcState.Idle;
                state.HomePosition = Transform(target).Coordinates;
                _steering.Unregister(animal);
            }
        }

        // Неприрученные животные пытаются вырваться
        if (!TryComp<NibiruTamableComponent>(animal, out var tamable) || !tamable.IsTamed)
        {
            component.TryingToBreakFree = true;
        }

        // Визуал привязи на животном
        _appearance.SetData(animal, LivestockVisuals.IsLeashed, true);
        Dirty(animal, component);

        if (component.LeashSound != null)
            _audio.PlayPvs(component.LeashSound, animal);
    }

    private void Unleash(EntityUid animal, NibiruLeashableComponent component, bool broken = false)
    {
        var holderUid = component.LeashedTo;

        if (holderUid != null && TryComp<NibiruLeashHolderComponent>(holderUid.Value, out var holder))
        {
            RemComp<NibiruLeashHolderComponent>(holderUid.Value);
            _movementSpeed.RefreshMovementSpeedModifiers(holderUid.Value);
            _alerts.ClearAlert(holderUid.Value, "Pulling");
        }

        // Если привязь оборвана — выбрасываем верёвку
        if (broken && component.RopePrototype != null && holderUid != null)
        {
            var animalPos = _xform.GetMapCoordinates(animal);
            var holderPos = _xform.GetMapCoordinates(holderUid.Value);
            var spawnPos = (animalPos.Position + holderPos.Position) / 2;

            var spawnMapPos = new MapCoordinates(spawnPos, animalPos.MapId);
            var spawned = Spawn(component.RopePrototype, spawnMapPos);
        }

        component.IsLeashed = false;
        component.LeashedTo = null;
        component.TryingToBreakFree = false;
        component.RopePrototype = null;
        Dirty(animal, component);
        component.BreakFreeAccumulator = 0f;

        // Возвращаемся к обычному поведению
        if (TryComp<NibiruNpcStateMachineComponent>(animal, out var state))
        {
            state.CurrentTarget = null;
            state.CurrentState = NibiruNpcState.Idle;
        }

        // Визуал привязи на животном
        _appearance.SetData(animal, LivestockVisuals.IsLeashed, false);
    }
}
