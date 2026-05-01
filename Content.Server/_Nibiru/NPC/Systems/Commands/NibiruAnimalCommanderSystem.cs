// Obsolete root using removed
using Content.Shared._Nibiru.NPC.Behavior;
using Content.Shared._Nibiru.NPC.Commands;
using Content.Shared._Nibiru.NPC.Training;
using Content.Shared._Nibiru.NPC.Livestock;
using Content.Shared._Nibiru.NPC.Utility;
using Content.Server._Nibiru.NPC.Systems.Training;
using Content.Server._Nibiru.NPC.Systems.Behavior;
using Content.Shared.Actions;
using Content.Shared.Popups;
using Content.Shared._Nibiru.NPC.Training;
using Content.Shared.Chat;
using Content.Server.Chat.Systems;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Timing;

namespace Content.Server._Nibiru.NPC.Systems.Commands;

public sealed class NibiruAnimalCommanderSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly NibiruTamingSystem _taming = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NibiruAnimalCommanderComponent, ComponentShutdown>(OnShutdown);

        SubscribeLocalEvent<NibiruAnimalCommanderComponent, NibiruAnimalFollowActionEvent>(OnFollowAction);
        SubscribeLocalEvent<NibiruAnimalCommanderComponent, NibiruAnimalStayActionEvent>(OnStayAction);
        SubscribeLocalEvent<NibiruAnimalCommanderComponent, NibiruAnimalAttackActionEvent>(OnAttackAction);
        SubscribeLocalEvent<NibiruAnimalCommanderComponent, NibiruAnimalSearchActionEvent>(OnSearchAction);
        SubscribeLocalEvent<NibiruAnimalCommanderComponent, NibiruAnimalDeliverActionEvent>(OnDeliverAction);
        SubscribeLocalEvent<NibiruAnimalCommandLearnedEvent>(OnCommandLearned);
        SubscribeLocalEvent<NibiruAnimalCommanderComponent, Content.Shared.Damage.Systems.DamageChangedEvent>(OnOwnerDamaged);
    }

    private void OnCommandLearned(NibiruAnimalCommandLearnedEvent args)
    {
        var animal = args.Animal;
        var query = EntityQueryEnumerator<NibiruAnimalCommanderComponent>();
        while (query.MoveNext(out var owner, out var comp))
        {
            if (comp.CurrentAnimal == animal)
            {
                RefreshActions(owner, comp);
                break;
            }
        }
    }

    public void RefreshActions(EntityUid owner, NibiruAnimalCommanderComponent comp)
    {
        if (comp.CurrentAnimal == null) return;
        var animal = comp.CurrentAnimal.Value;

        if (TryComp<NibiruTamableComponent>(animal, out var tamable))
        {
            if (tamable.LearnedCommands.Contains(NibiruAnimalCommand.Follow))
                _actions.AddAction(owner, ref comp.FollowActionEntity, comp.FollowActionId);
            if (tamable.LearnedCommands.Contains(NibiruAnimalCommand.Stay))
                _actions.AddAction(owner, ref comp.StayActionEntity, comp.StayActionId);
            if (tamable.LearnedCommands.Contains(NibiruAnimalCommand.Attack))
                _actions.AddAction(owner, ref comp.AttackActionEntity, comp.AttackActionId);
            if (tamable.LearnedCommands.Contains(NibiruAnimalCommand.Search))
                _actions.AddAction(owner, ref comp.SearchActionEntity, comp.SearchActionId);
            if (tamable.LearnedCommands.Contains(NibiruAnimalCommand.Deliver))
                _actions.AddAction(owner, ref comp.DeliverActionEntity, comp.DeliverActionId);
        }
    }

    public void AssignAnimal(EntityUid owner, EntityUid animal)
    {
        var comp = EnsureComp<NibiruAnimalCommanderComponent>(owner);

        // If already has an animal, remove old actions
        RemoveActions(owner, comp);

        comp.CurrentAnimal = animal;
        RefreshActions(owner, comp);

        // Give initial follow command
        _taming.GiveCommand(animal, owner, NibiruAnimalCommand.Follow);
        _popup.PopupEntity(Loc.GetString("nibiru-animal-command-follow-start"), owner, owner);
    }

    public void RemoveActions(EntityUid uid, NibiruAnimalCommanderComponent component)
    {
        _actions.RemoveAction(uid, component.FollowActionEntity);
        _actions.RemoveAction(uid, component.StayActionEntity);
        _actions.RemoveAction(uid, component.AttackActionEntity);
        _actions.RemoveAction(uid, component.SearchActionEntity);
        _actions.RemoveAction(uid, component.DeliverActionEntity);
        component.FollowActionEntity = null;
        component.StayActionEntity = null;
        component.AttackActionEntity = null;
        component.SearchActionEntity = null;
        component.DeliverActionEntity = null;
        component.CurrentAnimal = null;
    }

    private void OnShutdown(EntityUid uid, NibiruAnimalCommanderComponent component, ComponentShutdown args)
    {
        RemoveActions(uid, component);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<NibiruAnimalCommanderComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (comp.CurrentAnimal == null)
                continue;

            var animal = comp.CurrentAnimal.Value;

            if (!EntityManager.EntityExists(animal) || _mobState.IsDead(animal))
            {
                RemoveActions(uid, comp);
                continue;
            }

            if (!TryComp<TransformComponent>(animal, out var animalXform))
            {
                RemoveActions(uid, comp);
                continue;
            }

            if (xform.Coordinates.TryDistance(EntityManager, animalXform.Coordinates, out var dist))
            {
                if (dist > 15f)
                {
                    RemoveActions(uid, comp);
                    _popup.PopupEntity(Loc.GetString("nibiru-animal-commander-too-far"), uid, uid);
                }
            }
        }
    }

    private void OnFollowAction(EntityUid uid, NibiruAnimalCommanderComponent component, NibiruAnimalFollowActionEvent args)
    {
        if (args.Handled || component.CurrentAnimal == null) return;
        args.Handled = true;

        if (HandleCommand(uid, component, NibiruAnimalCommand.Follow, args.Speech))
            _popup.PopupEntity(Loc.GetString("nibiru-animal-command-follow-single"), uid, uid);
    }

    private void OnStayAction(EntityUid uid, NibiruAnimalCommanderComponent component, NibiruAnimalStayActionEvent args)
    {
        if (args.Handled || component.CurrentAnimal == null) return;
        args.Handled = true;

        if (HandleCommand(uid, component, NibiruAnimalCommand.Stay, args.Speech))
            _popup.PopupEntity(Loc.GetString("nibiru-animal-command-stay-single"), uid, uid);
    }

    private void OnAttackAction(EntityUid uid, NibiruAnimalCommanderComponent component, NibiruAnimalAttackActionEvent args)
    {
        if (args.Handled || component.CurrentAnimal == null) return;
        args.Handled = true;

        if (HandleCommand(uid, component, NibiruAnimalCommand.Attack, args.Speech, args.Target))
            _popup.PopupEntity(Loc.GetString("nibiru-animal-command-attack-single"), uid, uid);
    }

    private void OnSearchAction(EntityUid uid, NibiruAnimalCommanderComponent component, NibiruAnimalSearchActionEvent args)
    {
        if (args.Handled || component.CurrentAnimal == null) return;
        args.Handled = true;

        EntityUid? foundTarget = null;

        // Ищем ДНК или отпечатки на предмете
        if (TryComp<Content.Server.Forensics.ForensicsComponent>(args.Target, out var forensics))
        {
            // Берем первый попавшийся отпечаток или ДНК
            string? targetBio = null;
            foreach (var dna in forensics.DNAs) { targetBio = dna; break; }
            if (targetBio == null)
            {
                foreach (var fp in forensics.Fingerprints) { targetBio = fp; break; }
            }

            if (targetBio != null)
            {
                // Ищем владельца в радиусе 30 тайлов
                var animalPos = _transform.GetMapCoordinates(component.CurrentAnimal.Value);
                var query = EntityQueryEnumerator<Content.Shared.Forensics.Components.DnaComponent, TransformComponent>();
                while (query.MoveNext(out var targetUid, out var dnaComp, out var targetXform))
                {
                    if (dnaComp.DNA == targetBio && targetXform.MapID == animalPos.MapId)
                    {
                        var dist = (targetXform.WorldPosition - animalPos.Position).Length();
                        if (dist <= 30f)
                        {
                            foundTarget = targetUid;
                            break;
                        }
                    }
                }

                // Если по ДНК не нашли, ищем по отпечаткам
                if (foundTarget == null)
                {
                    var fpQuery = EntityQueryEnumerator<Content.Shared.Forensics.Components.FingerprintComponent, TransformComponent>();
                    while (fpQuery.MoveNext(out var targetUid, out var fpComp, out var targetXform))
                    {
                        if (fpComp.Fingerprint == targetBio && targetXform.MapID == animalPos.MapId)
                        {
                            var dist = (targetXform.WorldPosition - animalPos.Position).Length();
                            if (dist <= 30f)
                            {
                                foundTarget = targetUid;
                                break;
                            }
                        }
                    }
                }
            }
        }

        if (foundTarget != null)
        {
            if (HandleCommand(uid, component, NibiruAnimalCommand.Search, args.Speech, foundTarget))
                _popup.PopupEntity(Loc.GetString("nibiru-animal-command-search-success"), uid, uid);
        }
        else
        {
            _popup.PopupEntity(Loc.GetString("nibiru-animal-command-search-fail"), uid, uid);
        }
    }

    private void OnDeliverAction(EntityUid uid, NibiruAnimalCommanderComponent component, NibiruAnimalDeliverActionEvent args)
    {
        if (args.Handled || component.CurrentAnimal == null) return;
        args.Handled = true;

        if (HandleCommand(uid, component, NibiruAnimalCommand.Deliver, args.Speech))
            _popup.PopupEntity(Loc.GetString("nibiru-animal-command-deliver-start"), uid, uid);
    }

    private bool HandleCommand(EntityUid owner, NibiruAnimalCommanderComponent component, NibiruAnimalCommand command, string? speech, EntityUid? target = null)
    {
        if (component.CurrentAnimal == null) return false;
        var animal = component.CurrentAnimal.Value;

        // Check range to animal (10m)
        if (!TryComp<TransformComponent>(animal, out var animalXform) || !TryComp<TransformComponent>(owner, out var ownerXform))
            return false;

        if (!ownerXform.Coordinates.TryDistance(EntityManager, animalXform.Coordinates, out var dist) || dist > 10f)
        {
            _popup.PopupEntity(Loc.GetString("nibiru-animal-command-too-far-to-hear"), owner, owner);
            return false;
        }

        // Owner speaks
        if (!string.IsNullOrEmpty(speech))
        {
            _chat.TrySendInGameICMessage(owner, speech, InGameICChatType.Speak, false);
        }

        return _taming.GiveCommand(animal, owner, command, target);
    }

    private void OnOwnerDamaged(EntityUid uid, NibiruAnimalCommanderComponent component, Content.Shared.Damage.Systems.DamageChangedEvent args)
    {
        if (!args.DamageIncreased || args.Origin == null || component.CurrentAnimal == null)
            return;

        var animal = component.CurrentAnimal.Value;

        // Животное должно быть в режиме следования, чтобы защищать
        if (!TryComp<NibiruNpcBehaviorComponent>(animal, out var behavior) || behavior.CurrentState != NibiruNpcState.Following)
            return;

        // Отправляем команду атаки без произнесения вслух
        _taming.GiveCommand(animal, uid, NibiruAnimalCommand.Attack, args.Origin);
    }
}
