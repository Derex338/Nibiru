using Content.Server._Nibiru.NPC.Systems.Behavior;
using Content.Server._Nibiru.NPC.Systems.Commands;
using Content.Server._Nibiru.NPC.Systems.Utility;
using Content.Server.NPC.Systems;
using Content.Shared._Nibiru.NPC.Behavior;
using Content.Shared._Nibiru.NPC.Behavior.Components;
using Content.Shared._Nibiru.NPC.Commands;
using Content.Shared._Nibiru.NPC.Livestock;
using Content.Shared._Nibiru.NPC.Training;
using Content.Shared._Nibiru.NPC.Utility;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.NPC;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Robust.Shared.Timing;
using Content.Shared.Interaction.Events;

namespace Content.Server._Nibiru.NPC.Systems.Training;

/// <summary>
/// Управляет приручением животных через кормление.
/// Обрабатывает рост/убывание доверия, привязку к хозяину,
/// переключение NPC в режим следования после приручения.
/// </summary>
public sealed class NibiruTamingSystem : EntitySystem
{
    [Dependency] private readonly NpcFactionSystem _faction = default!;
    [Dependency] private readonly NibiruAnimalSoundSystem _sounds = default!;
    [Dependency] private readonly NibiruAnimalMoodSystem _mood = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly NPCSteeringSystem _steering = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NibiruTamableComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<NibiruTamableComponent, NibiruAnimalFeedingDoAfterEvent>(OnFeedingDoAfter);
        SubscribeLocalEvent<NibiruTamableComponent, DamageChangedEvent>(OnDamaged);
        // Поглаживание рукой: небольшой прирост доверия с красивыми эффектами
        SubscribeLocalEvent<NibiruTamableComponent, InteractHandEvent>(OnInteractHand);
    }

    /// <summary>
    /// Поглаживание животного (UseInHand / Z) — небольшой прирост доверия.
    /// Совместимо с PettableFriendSystem: если на животном есть PettableFriendComponent,
    /// то PettableSystem обрабатывает дружбу, мы добавляем лишь прирост доверия.
    /// </summary>
    private void OnInteractHand(EntityUid uid, NibiruTamableComponent component, InteractHandEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<MobStateComponent>(uid, out var mobState) || mobState.CurrentState != MobState.Alive)
            return;

        // Гладить можно только прирученных или частично доверяющих животных
        if (!component.IsTamed && component.TrustLevel < component.TrustThreshold * 0.3f)
            return;

        // Прирост доверия за поглаживание (10% от стандартной кормёжки)
        var trustGain = component.TrustPerFeeding * 0.1f;
        component.TrustLevel = MathF.Min(component.TrustLevel + trustGain, component.MaxTrust);

        // Спавним сердечки как визуальный эффект
        Spawn("EffectHearts", Transform(uid).Coordinates);
    }

    /// <summary>
    /// Обработка кормления: игрок использует еду на животном.
    /// </summary>
    private void OnInteractUsing(EntityUid uid, NibiruTamableComponent component, InteractUsingEvent args)
    {

        if (args.Handled)
        {
            return;
        }

        if (!TryComp<MobStateComponent>(uid, out var mobState) || mobState.CurrentState != MobState.Alive)
        {
            return;
        }

        // Проверяем, является ли предмет подходящей едой
        if (!IsAcceptableFood(args.Used, component))
        {
            return;
        }

        args.Handled = true;

        // Запускаем DoAfter для кормления
        var doAfterArgs = new DoAfterArgs(EntityManager, args.User, TimeSpan.FromSeconds(2), new NibiruAnimalFeedingDoAfterEvent(), uid, target: uid, used: args.Used)
        {
            BreakOnMove = false,
            BreakOnDamage = true,
            NeedHand = true,
            DistanceThreshold = 2f
        };

        _doAfter.TryStartDoAfter(doAfterArgs);
    }

    private void OnFeedingDoAfter(EntityUid uid, NibiruTamableComponent component, NibiruAnimalFeedingDoAfterEvent args)
    {
        if (args.Cancelled)
        {
            return;
        }

        if (args.Handled || args.Used == null)
            return;

        // Повторная проверка еды в конце действия
        if (!IsAcceptableFood(args.Used.Value, component))
        {
            return;
        }

        var trustGain = component.TrustPerFeeding;

        // Удвоенное доверие за любимую еду
        if (IsFavoriteFood(args.Used.Value, component))
            trustGain *= 2f;

        // Начисляем доверие
        component.TrustLevel = MathF.Min(component.TrustLevel + trustGain, component.MaxTrust);

        // Звук кормления
        _sounds.PlayFeedingSound(uid);

        // Повышаем настроение
        _mood.OnFed(uid);

        // Проверяем порог приручения
        if (!component.IsTamed && component.TrustLevel >= component.TrustThreshold)
        {
            TameAnimal(uid, args.User, component);
        }

        // Потребляем еду
        QueueDel(args.Used.Value);
        args.Handled = true;
    }

    /// <summary>
    /// Штраф за агрессию хозяина.
    /// </summary>
    private void OnDamaged(EntityUid uid, NibiruTamableComponent component, DamageChangedEvent args)
    {
        if (!args.DamageIncreased || args.Origin == null)
            return;

        // Если бьёт хозяин — теряем доверие
        if (component.IsTamed && component.OwnerUid == args.Origin)
        {
            component.TrustLevel = MathF.Max(0, component.TrustLevel - component.TrustPenaltyOnHit);

            // Если доверие упало ниже половины порога — животное дичает
            if (component.TrustLevel < component.TrustThreshold * 0.5f)
            {
                UntameAnimal(uid, component);
            }
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<NibiruTamableComponent, NibiruNpcBehaviorComponent>();
        while (query.MoveNext(out var uid, out var tamable, out var behavior))
        {
            // Плавное убывание доверия
            if (tamable.IsTamed && tamable.TrustLevel > 0)
            {
                tamable.TrustLevel = MathF.Max(0, tamable.TrustLevel - tamable.TrustDecayRate * frameTime);

                // Одичание при полной потере доверия
                if (tamable.TrustLevel <= 0)
                    UntameAnimal(uid, tamable);
            }

        }
    }

    private void TameAnimal(EntityUid uid, EntityUid owner, NibiruTamableComponent component)
    {
        component.IsTamed = true;
        component.OwnerUid = owner;

        // Делаем животное дружественным к хозяину через систему фракций
        _faction.IgnoreEntity(uid, owner);

        // Звук приручения
        _sounds.PlayTamedSound(uid);

        // Визуальный эффект — сердечки при приручении!
        Spawn("EffectHearts", Transform(uid).Coordinates);

        // Добавляем базовые команды
        LearnCommand(uid, component, NibiruAnimalCommand.Follow);
        LearnCommand(uid, component, NibiruAnimalCommand.Stay);

        // Переводим в режим следования
        if (TryComp<NibiruNpcStateMachineComponent>(uid, out var behavior))
        {
            behavior.CurrentTarget = owner;
            behavior.CurrentState = NibiruNpcState.Following;
        }

        // Автоматически добавляем животное в группу командующего
        IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<NibiruAnimalCommanderSystem>().AddAnimal(owner, uid);
    }

    private void UntameAnimal(EntityUid uid, NibiruTamableComponent component)
    {
        var prevOwner = component.OwnerUid;
        component.IsTamed = false;
        component.OwnerUid = null;
        component.TrustLevel = 0;

        // Убираем исключения из фракции
        if (prevOwner != null)
            _faction.DeAggroEntity(uid, prevOwner.Value);

        if (TryComp<NibiruNpcStateMachineComponent>(uid, out var behavior))
        {
            behavior.CurrentTarget = null;
            behavior.CurrentState = NibiruNpcState.Idle;
        }
    }

    private void LearnCommand(EntityUid uid, NibiruTamableComponent component, NibiruAnimalCommand command)
    {
        if (!component.PossibleCommands.Contains(command) || !component.LearnedCommands.Add(command) || component.OwnerUid == null)
            return;

        RaiseLocalEvent(component.OwnerUid.Value, new NibiruAnimalCommandLearnedEvent(uid, command));
    }

    private bool IsFavoriteFood(EntityUid item, NibiruTamableComponent component)
    {
        if (!TryComp<MetaDataComponent>(item, out var meta) || meta.EntityPrototype == null)
            return false;

        if (component.FavoriteFoods.Contains(meta.EntityPrototype.ID))
            return true;

        foreach (var tag in component.FavoriteFoodTags)
        {
            if (_tag.HasTag(item, tag))
                return true;
        }

        return false;
    }

    private bool IsAcceptableFood(EntityUid item, NibiruTamableComponent component)
    {
        if (!_tag.HasTag(item, "Food"))
            return false;

        // Если задан конкретный список — ест только это
        if (component.AcceptedFood != null && component.AcceptedFood.Count > 0)
        {
            if (!TryComp<MetaDataComponent>(item, out var meta) || meta.EntityPrototype == null)
                return false;
            return component.AcceptedFood.Contains(meta.EntityPrototype.ID);
        }

        // Проверка по типу диеты
        switch (component.Diet)
        {
            case NibiruAnimalDiet.Carnivore:
                return _tag.HasTag(item, "Meat"); // Плотоядные едят мясо

            case NibiruAnimalDiet.Herbivore:
                // Травоядные не едят мясо
                return !_tag.HasTag(item, "Meat") && (_tag.HasTag(item, "Plant") || _tag.HasTag(item, "Fruit") || _tag.HasTag(item, "Vegetable"));

            case NibiruAnimalDiet.Omnivore:
            default:
                return true; // Всеядные едят всё, что имеет тег Food
        }
    }

    /// <summary>
    /// Даёт животному команду. Только для обученных команд.
    /// </summary>
    public bool GiveCommand(EntityUid animal, EntityUid commander, NibiruAnimalCommand command, EntityUid? target = null)
    {
        if (!TryComp<NibiruTamableComponent>(animal, out var tamable))
            return false;

        if (!tamable.IsTamed || tamable.OwnerUid != commander)
            return false;

        if (!tamable.LearnedCommands.Contains(command))
            return false;

        if (!TryComp<NibiruNpcStateMachineComponent>(animal, out var behavior))
            return false;

        _steering.Unregister(animal);

        switch (command)
        {
            case NibiruAnimalCommand.Follow:
                behavior.CurrentTarget = commander;
                behavior.CurrentCommand = command;
                behavior.CurrentState = NibiruNpcState.Following;
                return true;

            case NibiruAnimalCommand.Stay:
                behavior.CurrentTarget = null;
                behavior.CurrentCommand = command;
                behavior.CurrentState = NibiruNpcState.Idle;
                behavior.HomePosition = Transform(animal).Coordinates;
                return true;

            case NibiruAnimalCommand.Attack:
            case NibiruAnimalCommand.Grab:
                if (target == null)
                    return false;

                if (!TryComp<TransformComponent>(target.Value, out _))
                    return false;

                if (command == NibiruAnimalCommand.Grab && !TryComp<PullableComponent>(target.Value, out _))
                    return false;

                // Check faction. If target is friendly, check mood.
                if (_faction.IsEntityFriendly(animal, target.Value))
                {
                    // If tamer commanded it, we might still attack if trust is high enough
                    // or if it's just a general command.
                    // Let's make it ALWAYS work if commanded by owner, unless very unhappy.
                    if (TryComp<NibiruAnimalMoodComponent>(animal, out var moodComp) && moodComp.MoodState == AnimalMoodState.Sad)
                    {
                         _popup.PopupEntity(Loc.GetString("nibiru-animal-command-refuse"), animal, commander);
                         return false;
                    }
                }

                behavior.CurrentTarget = target;
                behavior.CurrentCommand = command;
                behavior.CurrentState = NibiruNpcState.Chasing;
                return true;

            case NibiruAnimalCommand.Search:
                if (target == null)
                    return false;
                behavior.CurrentTarget = target;
                behavior.CurrentCommand = command;
                // Chasing: животное активно преследует цель по запаху,
                // но не атакует (обрабатывается в ProcessChasing через команду Search)
                behavior.CurrentState = NibiruNpcState.Chasing;
                return true;

            case NibiruAnimalCommand.Deliver:
                // Открываем UI выбора цели для игрока
                IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<NibiruBirdDeliverySystem>().OpenUi(commander, animal);
                return true;

            case NibiruAnimalCommand.Guard:
                behavior.HomePosition = Transform(animal).Coordinates;
                behavior.CurrentState = NibiruNpcState.Patrolling;
                return true;

            default:
                return false;
        }
    }
}
