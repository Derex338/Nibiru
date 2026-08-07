using Content.Shared._Nibiru.NPC.Behavior.Components;
using Content.Shared._Nibiru.NPC.Commands;
using Content.Shared._Nibiru.NPC.Training;
using Content.Shared._Nibiru.NPC.Livestock;
using Content.Shared._Nibiru.NPC.Utility;
using Content.Server._Nibiru.NPC.Systems.Training;
using Content.Server._Nibiru.NPC.Systems.Behavior;
using Content.Server._Nibiru.NPC.Systems.Utility;
using Content.Shared.Actions;
using Content.Shared.Popups;
using Content.Shared.Chat;
using Content.Server.Chat.Systems;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Timing;
using Content.Shared._Nibiru.NPC.Behavior;
using Content.Shared.Interaction;

namespace Content.Server._Nibiru.NPC.Systems.Commands;

public sealed partial class NibiruAnimalCommanderSystem : EntitySystem
{
    private const float SearchTargetRange = 60f;

[Dependency] private SharedActionsSystem _actions = default!;
[Dependency] private NibiruTamingSystem _taming = default!;
[Dependency] private SharedPopupSystem _popup = default!;
[Dependency] private ChatSystem _chat = default!;
[Dependency] private SharedTransformSystem _transform = default!;
[Dependency] private MobStateSystem _mobState = default!;
[Dependency] private NibiruAnimalSoundSystem _sounds = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NibiruAnimalCommanderComponent, ComponentShutdown>(OnShutdown);

        SubscribeLocalEvent<NibiruAnimalCommanderComponent, NibiruAnimalFollowActionEvent>(OnFollowAction);
        SubscribeLocalEvent<NibiruAnimalCommanderComponent, NibiruAnimalStayActionEvent>(OnStayAction);
        SubscribeLocalEvent<NibiruAnimalCommanderComponent, NibiruAnimalAttackActionEvent>(OnAttackAction);
        SubscribeLocalEvent<NibiruAnimalCommanderComponent, NibiruAnimalGrabActionEvent>(OnGrabAction);
        SubscribeLocalEvent<NibiruAnimalCommanderComponent, NibiruAnimalSearchActionEvent>(OnSearchAction);
        SubscribeLocalEvent<NibiruAnimalCommanderComponent, NibiruAnimalDeliverActionEvent>(OnDeliverAction);
        SubscribeLocalEvent<NibiruAnimalCommanderComponent, NibiruAnimalCommandLearnedEvent>(OnCommandLearned);
        SubscribeLocalEvent<NibiruAnimalCommanderComponent, Content.Shared.Damage.Systems.DamageChangedEvent>(OnOwnerDamaged);
        SubscribeLocalEvent<NibiruAnimalCommanderComponent, Content.Shared.Pointing.AfterPointedAtEvent>(OnPointedAt);

        // Перехватываем использование предмета на животном пока оно ждёт команду Search
        SubscribeLocalEvent<NibiruAnimalSearchWaitingComponent, InteractUsingEvent>(OnSearchItemPresented);
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Ивенты обучения и управления списком животных
    // ──────────────────────────────────────────────────────────────────────────

    private void OnCommandLearned(EntityUid uid, NibiruAnimalCommanderComponent comp, NibiruAnimalCommandLearnedEvent args)
    {
        var animal = args.Animal;
        if (comp.Animals.Contains(animal))
        {
            RefreshActions(uid, comp);
        }
    }

    /// <summary>
    /// Обновляет кнопки действий на основе union выученных команд всех животных в группе.
    /// Показывает кнопку, если хотя бы одно животное умеет эту команду.
    /// </summary>
    public void RefreshActions(EntityUid owner, NibiruAnimalCommanderComponent comp)
    {
        // Если нет животных - ничего не делаем (RemoveAnimal/RemoveAllActions должны убирать кнопки)
        if (comp.Animals.Count == 0)
            return;

        // Собираем объединение всех выученных команд у животных в группе
        var available = new HashSet<NibiruAnimalCommand>();
        foreach (var animal in comp.Animals)
        {
            if (!TryComp<NibiruTamableComponent>(animal, out var tamable))
                continue;

            foreach (var cmd in tamable.LearnedCommands)
                available.Add(cmd);
        }

        void ApplyActionForCommand(NibiruAnimalCommand command, EntityUid? actionEntityRef, string actionId, ref EntityUid? actionEntityField)
        {
            if (available.Contains(command))
                _actions.AddAction(owner, ref actionEntityField, actionId);
            else
            {
                _actions.RemoveAction(owner, actionEntityRef);
                actionEntityField = null;
            }
        }

        ApplyActionForCommand(NibiruAnimalCommand.Follow,  comp.FollowActionEntity,  comp.FollowActionId,  ref comp.FollowActionEntity);
        ApplyActionForCommand(NibiruAnimalCommand.Stay,    comp.StayActionEntity,    comp.StayActionId,    ref comp.StayActionEntity);
        ApplyActionForCommand(NibiruAnimalCommand.Attack,  comp.AttackActionEntity,  comp.AttackActionId,  ref comp.AttackActionEntity);
        ApplyActionForCommand(NibiruAnimalCommand.Grab,    comp.GrabActionEntity,    comp.GrabActionId,    ref comp.GrabActionEntity);
        ApplyActionForCommand(NibiruAnimalCommand.Search,  comp.SearchActionEntity,  comp.SearchActionId,  ref comp.SearchActionEntity);
        ApplyActionForCommand(NibiruAnimalCommand.Deliver, comp.DeliverActionEntity, comp.DeliverActionId, ref comp.DeliverActionEntity);

        UpdateActionToggles(owner, comp);
    }

    private void UpdateActionToggles(EntityUid owner, NibiruAnimalCommanderComponent comp)
    {
        // Так как только некоторые команды имеют состояние переключателя (toggle) — обновляем их явно.
        _actions.SetToggled(comp.AttackActionEntity, comp.CurrentMode == NibiruAnimalCommand.Attack);
        _actions.SetToggled(comp.GrabActionEntity,   comp.CurrentMode == NibiruAnimalCommand.Grab);
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Public API — добавление / удаление животных из группы
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Добавляет животное в группу командующего. Если уже есть — ничего не делает.
    /// </summary>
    public void AddAnimal(EntityUid owner, EntityUid animal)
    {
        var comp = EnsureComp<NibiruAnimalCommanderComponent>(owner);

        if (comp.Animals.Contains(animal))
            return;

        comp.Animals.Add(animal);
        RefreshActions(owner, comp);

        // Сразу даём команду следовать
        _taming.GiveCommand(animal, owner, NibiruAnimalCommand.Follow);
        _popup.PopupEntity(Loc.GetString("nibiru-animal-command-follow-start"), owner, owner);
    }

    /// <summary>
    /// Убирает животное из группы. Если больше нет животных — снимает действия.
    /// </summary>
    public void RemoveAnimal(EntityUid owner, EntityUid animal)
    {
        if (!TryComp<NibiruAnimalCommanderComponent>(owner, out var comp))
            return;

        comp.Animals.Remove(animal);

        if (comp.Animals.Count == 0)
            RemoveAllActions(owner, comp);
        else
            RefreshActions(owner, comp);
    }

    /// <summary>
    /// Устаревший метод для обратной совместимости — теперь добавляет животное в список.
    /// </summary>
    public void AssignAnimal(EntityUid owner, EntityUid animal)
    {
        AddAnimal(owner, animal);
    }

    public void RemoveAllActions(EntityUid uid, NibiruAnimalCommanderComponent component)
    {
        _actions.RemoveAction(uid, component.FollowActionEntity);
        _actions.RemoveAction(uid, component.StayActionEntity);
        _actions.RemoveAction(uid, component.AttackActionEntity);
        _actions.RemoveAction(uid, component.GrabActionEntity);
        _actions.RemoveAction(uid, component.SearchActionEntity);
        _actions.RemoveAction(uid, component.DeliverActionEntity);
        component.FollowActionEntity  = null;
        component.StayActionEntity    = null;
        component.AttackActionEntity  = null;
        component.GrabActionEntity    = null;
        component.SearchActionEntity  = null;
        component.DeliverActionEntity = null;
        component.Animals.Clear();
    }

    private void OnShutdown(EntityUid uid, NibiruAnimalCommanderComponent component, ComponentShutdown args)
    {
        RemoveAllActions(uid, component);
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Update: очистка мёртвых / слишком далёких животных
    // ──────────────────────────────────────────────────────────────────────────

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<NibiruAnimalCommanderComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (comp.Animals.Count == 0)
                continue;

            var toRemove = new List<EntityUid>();
            foreach (var animal in comp.Animals)
            {
                // Животное больше не существует или мертво
                if (!Exists(animal) || _mobState.IsDead(animal))
                {
                    toRemove.Add(animal);
                    continue;
                }

                // Животное слишком далеко
                if (!TryComp(animal, out TransformComponent? animalXform))
                {
                    toRemove.Add(animal);
                    continue;
                }

                if (xform.Coordinates.TryDistance(EntityManager, animalXform.Coordinates, out var dist) && dist > 15f)
                {
                    toRemove.Add(animal);
                    _popup.PopupEntity(Loc.GetString("nibiru-animal-commander-too-far"), uid, uid);
                }
            }

            foreach (var dead in toRemove)
            {
                comp.Animals.Remove(dead);
            }

            if (comp.Animals.Count == 0)
                RemoveAllActions(uid, comp);
        }

        // Таймаут ожидания обнюхивания
        var searchQuery = EntityQueryEnumerator<NibiruAnimalSearchWaitingComponent>();
        while (searchQuery.MoveNext(out var animalUid, out var searchWait))
        {
            searchWait.Accumulator += frameTime;
            if (searchWait.Accumulator >= searchWait.Timeout)
            {
                _popup.PopupEntity(Loc.GetString("nibiru-animal-command-search-timeout"), animalUid, searchWait.Commander);
                RemComp<NibiruAnimalSearchWaitingComponent>(animalUid);
            }
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Обработчики действий
    // ──────────────────────────────────────────────────────────────────────────

    private void OnFollowAction(EntityUid uid, NibiruAnimalCommanderComponent component, NibiruAnimalFollowActionEvent args)
    {
        if (args.Handled || component.Animals.Count == 0) return;
        args.Handled = true;

        var count = HandleCommandAll(uid, component, NibiruAnimalCommand.Follow, args.Speech);
        if (count > 0)
            _popup.PopupEntity(Loc.GetString("nibiru-animal-command-follow-single"), uid, uid);
    }

    private void OnStayAction(EntityUid uid, NibiruAnimalCommanderComponent component, NibiruAnimalStayActionEvent args)
    {
        if (args.Handled || component.Animals.Count == 0) return;
        args.Handled = true;

        var count = HandleCommandAll(uid, component, NibiruAnimalCommand.Stay, args.Speech);
        if (count > 0)
            _popup.PopupEntity(Loc.GetString("nibiru-animal-command-stay-single"), uid, uid);
    }

    private void OnAttackAction(EntityUid uid, NibiruAnimalCommanderComponent component, NibiruAnimalAttackActionEvent args)
    {
        if (args.Handled || component.Animals.Count == 0) return;

        if (component.CurrentMode == NibiruAnimalCommand.Attack)
        {
            component.CurrentMode = null;
            UpdateActionToggles(uid, component);
            _popup.PopupEntity(Loc.GetString("nibiru-animal-command-mode-cancel"), uid, uid);
        }
        else
        {
            component.CurrentMode = NibiruAnimalCommand.Attack;
            UpdateActionToggles(uid, component);
            _popup.PopupEntity(Loc.GetString("nibiru-animal-command-attack-mode"), uid, uid);

            if (!string.IsNullOrEmpty(args.Speech))
                _chat.TrySendInGameICMessage(uid, args.Speech, InGameICChatType.Speak, false);
        }

        args.Handled = true;
    }

    private void OnGrabAction(EntityUid uid, NibiruAnimalCommanderComponent component, NibiruAnimalGrabActionEvent args)
    {
        if (args.Handled || component.Animals.Count == 0) return;

        if (component.CurrentMode == NibiruAnimalCommand.Grab)
        {
            component.CurrentMode = null;
            UpdateActionToggles(uid, component);
            _popup.PopupEntity(Loc.GetString("nibiru-animal-command-mode-cancel"), uid, uid);
        }
        else
        {
            component.CurrentMode = NibiruAnimalCommand.Grab;
            UpdateActionToggles(uid, component);
            _popup.PopupEntity(Loc.GetString("nibiru-animal-command-grab-mode"), uid, uid);

            if (!string.IsNullOrEmpty(args.Speech))
                _chat.TrySendInGameICMessage(uid, args.Speech, InGameICChatType.Speak, false);
        }

        args.Handled = true;
    }

    /// <summary>
    /// Команда Search: произносим фразу и переводим животных в режим ожидания обнюхивания.
    /// Они ждут, пока хозяин поднесёт предмет.
    /// </summary>
    private void OnSearchAction(EntityUid uid, NibiruAnimalCommanderComponent component, NibiruAnimalSearchActionEvent args)
    {
        if (args.Handled || component.Animals.Count == 0) return;
        args.Handled = true;

        // Произносим команду
        if (!string.IsNullOrEmpty(args.Speech))
            _chat.TrySendInGameICMessage(uid, args.Speech, InGameICChatType.Speak, false);

        // Добавляем всем животным с выученным Search компонент ожидания
        var activated = 0;
        foreach (var animal in component.Animals)
        {
            if (!TryComp<NibiruTamableComponent>(animal, out var tamable)) continue;
            if (!tamable.LearnedCommands.Contains(NibiruAnimalCommand.Search)) continue;

            // Проверяем дистанцию до животного
            if (!TryComp(animal, out TransformComponent? animalXform) ||
                !TryComp(uid, out TransformComponent? ownerXform)) continue;

            if (!ownerXform.Coordinates.TryDistance(EntityManager, animalXform.Coordinates, out var dist) || dist > 50f)
            {
                _popup.PopupEntity(Loc.GetString("nibiru-animal-command-too-far-to-hear"), uid, uid);
                continue;
            }

            var waiting = EnsureComp<NibiruAnimalSearchWaitingComponent>(animal);
            waiting.Commander = uid;
            waiting.Accumulator = 0f;

            // Воспроизводим звук нюхания
            if (TryComp<NibiruNpcAudioComponent>(animal, out var audio))
                _sounds.PlayAggroSound(animal, audio);

            activated++;
        }

        if (activated > 0)
            _popup.PopupEntity(Loc.GetString("nibiru-animal-command-search-waiting"), uid, uid);
        else
            _popup.PopupEntity(Loc.GetString("nibiru-animal-command-search-fail"), uid, uid);
    }

    /// <summary>
    /// Игрок поднёс предмет животному в режиме ожидания обнюхивания.
    /// Анализируем ДНК/отпечатки и запускаем поиск.
    /// </summary>
    private void OnSearchItemPresented(EntityUid animalUid, NibiruAnimalSearchWaitingComponent waiting, InteractUsingEvent args)
    {
        if (args.Handled) return;

        // Проверяем что взаимодействует именно хозяин
        if (args.User != waiting.Commander) return;

        args.Handled = true;
        RemComp<NibiruAnimalSearchWaitingComponent>(animalUid);

        var uid = waiting.Commander;

        EntityUid? foundTarget = null;
        var foundTooFar = false;

        // Ищем ДНК или отпечатки на предмете
        if (TryComp<Content.Server.Forensics.ForensicsComponent>(args.Used, out var forensics))
        {
            string? targetBio = null;
            foreach (var dna in forensics.DNAs) { targetBio = dna; break; }
            if (targetBio == null)
                foreach (var fp in forensics.Fingerprints) { targetBio = fp; break; }

            if (targetBio != null)
            {
                var animalPos = _transform.GetMapCoordinates(animalUid);

                // Поиск по ДНК
                var dnaQuery = EntityQueryEnumerator<Content.Shared.Forensics.Components.DnaComponent, TransformComponent>();
                while (dnaQuery.MoveNext(out var targetUid, out var dnaComp, out var targetXform))
                {
                    if (dnaComp.DNA != targetBio || targetXform.MapID != animalPos.MapId) continue;
                    var dist = (targetXform.WorldPosition - animalPos.Position).Length();
                    if (dist <= SearchTargetRange) { foundTarget = targetUid; break; }
                    foundTooFar = true;
                }

                // Если не нашли — поиск по отпечаткам
                if (foundTarget == null)
                {
                    var fpQuery = EntityQueryEnumerator<Content.Shared.Forensics.Components.FingerprintComponent, TransformComponent>();
                    while (fpQuery.MoveNext(out var targetUid, out var fpComp, out var targetXform))
                    {
                        if (fpComp.Fingerprint != targetBio || targetXform.MapID != animalPos.MapId) continue;
                        var dist = (targetXform.WorldPosition - animalPos.Position).Length();
                        if (dist <= SearchTargetRange) { foundTarget = targetUid; break; }
                        foundTooFar = true;
                    }
                }
            }
        }

        if (foundTarget != null)
        {
            if (_taming.GiveCommand(animalUid, uid, NibiruAnimalCommand.Search, foundTarget))
            {
                if (TryComp<NibiruNpcAudioComponent>(animalUid, out var audio))
                    _sounds.PlayAggroSound(animalUid, audio);
                _popup.PopupEntity(Loc.GetString("nibiru-animal-command-search-success"), uid, uid);
            }
        }
        else
        {
            if (foundTooFar && TryComp<NibiruNpcAudioComponent>(animalUid, out var audio))
                _sounds.PlayFleeSound(animalUid, audio);
            _popup.PopupEntity(Loc.GetString("nibiru-animal-command-search-fail"), uid, uid);
        }
    }

    private void OnDeliverAction(EntityUid uid, NibiruAnimalCommanderComponent component, NibiruAnimalDeliverActionEvent args)
    {
        if (args.Handled || component.Animals.Count == 0) return;
        args.Handled = true;

        var count = HandleCommandAll(uid, component, NibiruAnimalCommand.Deliver, args.Speech);
        if (count > 0)
            _popup.PopupEntity(Loc.GetString("nibiru-animal-command-deliver-start"), uid, uid);
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Реакция на урон: все животные в Following защищают хозяина
    // ──────────────────────────────────────────────────────────────────────────

    private void OnOwnerDamaged(EntityUid uid, NibiruAnimalCommanderComponent component, Content.Shared.Damage.Systems.DamageChangedEvent args)
    {
        if (!args.DamageIncreased || args.Origin == null || component.Animals.Count == 0)
            return;

        foreach (var animal in component.Animals)
        {
            if (!TryComp<NibiruNpcStateMachineComponent>(animal, out var state) ||
                state.CurrentState != NibiruNpcState.Following)
                continue;

            _taming.GiveCommand(animal, uid, NibiruAnimalCommand.Attack, args.Origin);
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Указание пальцем: команда в режиме Attack/Grab отправляется всем
    // ──────────────────────────────────────────────────────────────────────────

    private void OnPointedAt(EntityUid uid, NibiruAnimalCommanderComponent component, ref Content.Shared.Pointing.AfterPointedAtEvent args)
    {
        if (component.CurrentMode == null || component.Animals.Count == 0)
            return;

        var target = args.Pointed;
        var command = component.CurrentMode.Value;

        var anySuccess = false;
        foreach (var animal in component.Animals)
        {
            if (!TryComp(animal, out TransformComponent? animalXform) ||
                !TryComp(uid, out TransformComponent? ownerXform)) continue;

            if (!ownerXform.Coordinates.TryDistance(EntityManager, animalXform.Coordinates, out var dist) || dist > 10f)
                continue;

            if (_taming.GiveCommand(animal, uid, command, target))
                anySuccess = true;
        }

        if (anySuccess)
        {
            _popup.PopupEntity(Loc.GetString($"nibiru-animal-command-{command.ToString().ToLower()}-single"), uid, uid);
            component.CurrentMode = null;
            UpdateActionToggles(uid, component);
        }
        else
        {
            _popup.PopupEntity(Loc.GetString("nibiru-animal-command-too-far-to-hear"), uid, uid);
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Вспомогательные методы
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Отправляет команду всем животным в группе у которых она выучена.
    /// Возвращает количество животных которые приняли команду.
    /// </summary>
    private int HandleCommandAll(EntityUid owner, NibiruAnimalCommanderComponent component, NibiruAnimalCommand command, string? speech, EntityUid? target = null)
    {
        if (component.Animals.Count == 0) return 0;

        if (!TryComp(owner, out TransformComponent? ownerXform))
            return 0;

        // Произносим речь один раз (не для каждого животного)
        var speechSpoken = false;
        var successCount = 0;

        foreach (var animal in component.Animals)
        {
            if (!Exists(animal)) continue;

            if (!TryComp(animal, out TransformComponent? animalXform)) continue;

            if (!ownerXform.Coordinates.TryDistance(EntityManager, animalXform.Coordinates, out var dist) || dist > 10f)
                continue;

            if (!speechSpoken && !string.IsNullOrEmpty(speech))
            {
                _chat.TrySendInGameICMessage(owner, speech, InGameICChatType.Speak, false);
                speechSpoken = true;
            }

            if (_taming.GiveCommand(animal, owner, command, target))
                successCount++;
        }

        if (successCount == 0 && component.Animals.Count > 0)
            _popup.PopupEntity(Loc.GetString("nibiru-animal-command-too-far-to-hear"), owner, owner);

        return successCount;
    }
}
