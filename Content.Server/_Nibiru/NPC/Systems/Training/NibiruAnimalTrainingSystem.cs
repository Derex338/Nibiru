// Obsolete root using removed
using Content.Shared._Nibiru.NPC.Training;
using Content.Shared._Nibiru.NPC.Behavior;
using Content.Server._Nibiru.NPC.Systems.Commands;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Player;

namespace Content.Server._Nibiru.NPC.Systems.Training;

public sealed class NibiruAnimalTrainingSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NibiruTamableComponent, GetVerbsEvent<ActivationVerb>>(AddTrainingVerb);
        SubscribeLocalEvent<NibiruTamableComponent, GetVerbsEvent<Verb>>(AddCommandVerbs);
        SubscribeLocalEvent<NibiruTamableComponent, NibiruAnimalTrainCommandMessage>(OnTrainCommand);
        SubscribeLocalEvent<NibiruTamableComponent, NibiruAnimalTrainStressMessage>(OnTrainStress);
    }

    private void AddTrainingVerb(EntityUid uid, NibiruTamableComponent component, GetVerbsEvent<ActivationVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !component.IsTamed)
            return;

        // Только хозяин может открыть окно тренировки
        if (component.OwnerUid != args.User)
            return;

        args.Verbs.Add(new ActivationVerb
        {
            Text = Loc.GetString("nibiru-animal-training-verb"),
            Act = () => OpenTrainingUi(uid, args.User, component),
            Icon = new Robust.Shared.Utility.SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/settings.svg.192dpi.png"))
        });
    }

    private void AddCommandVerbs(EntityUid uid, NibiruTamableComponent component, GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !component.IsTamed)
            return;

        if (component.OwnerUid != args.User)
            return;

        // Только команда "Следовать" доступна в контекстном меню
        // Она же активирует панель действий
        if (!component.LearnedCommands.Contains(NibiruAnimalCommand.Follow))
            return;

        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("nibiru-animal-command-follow"),
            Act = () =>
            {
                var commanderSys = EntityManager.System<NibiruAnimalCommanderSystem>();
                commanderSys.AssignAnimal(args.User, uid);
            },
            Icon = new Robust.Shared.Utility.SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/follow.svg.192dpi.png"))
        });
    }

    private void OpenTrainingUi(EntityUid uid, EntityUid user, NibiruTamableComponent component)
    {
        if (!TryComp<ActorComponent>(user, out var actor))
            return;

        _ui.OpenUi(uid, NibiruAnimalTrainingUiKey.Key, actor.PlayerSession);
        UpdateUiState(uid, component);
    }

    private void UpdateUiState(EntityUid uid, NibiruTamableComponent tamable)
    {
        var hasMountFear = TryComp<NibiruMountFearComponent>(uid, out var mountFear);
        var stressTraining = mountFear?.StressTraining ?? 0f;
        var maxStressTraining = mountFear?.MaxStressTraining ?? 0f;

        var state = new NibiruAnimalTrainingBuiState(
            tamable.TrustLevel,
            tamable.MaxTrust,
            tamable.IsTamed,
            hasMountFear,
            stressTraining,
            maxStressTraining,
            tamable.Trainable,
            tamable.PossibleCommands,
            tamable.LearnedCommands
        );

        _ui.SetUiState(uid, NibiruAnimalTrainingUiKey.Key, state);
    }

    private void OnTrainCommand(EntityUid uid, NibiruTamableComponent component, NibiruAnimalTrainCommandMessage args)
    {
        var user = args.Actor;
        Log.Debug($"Training: Requesting command {args.Command} for {ToPrettyString(uid)} by {ToPrettyString(user)}");

        if (component.OwnerUid != user || !component.Trainable)
        {
            Log.Debug($"Training: Request denied. Owner: {component.OwnerUid == user}, Trainable: {component.Trainable}");
            return;
        }

        if (!component.PossibleCommands.Contains(args.Command))
        {
            Log.Debug($"Training: Animal {ToPrettyString(uid)} cannot learn command {args.Command}.");
            return;
        }

        if (component.LearnedCommands.Contains(args.Command))
        {
            Log.Debug($"Training: Command {args.Command} already learned.");
            return;
        }

        // Требования к доверию для команд
        float requiredTrust = args.Command switch
        {
            NibiruAnimalCommand.Follow => 50f,
            NibiruAnimalCommand.Stay => 75f,
            NibiruAnimalCommand.Attack => 100f,
            NibiruAnimalCommand.Guard => 120f,
            _ => 50f
        };

        if (component.TrustLevel < requiredTrust)
        {
            Log.Debug($"Training: Not enough trust ({component.TrustLevel}/{requiredTrust}).");
            _popup.PopupEntity(Loc.GetString("nibiru-animal-training-trust-low"), uid, user);
            return;
        }

        component.LearnedCommands.Add(args.Command);
        component.TrustLevel -= 30f; // Обучение командам "тратит" доверие
        Log.Debug($"Training: Command {args.Command} learned successfully. Remaining trust: {component.TrustLevel}");
        
        RaiseLocalEvent(uid, new NibiruAnimalCommandLearnedEvent(uid, args.Command));
        
        UpdateUiState(uid, component);
        _popup.PopupEntity(Loc.GetString("nibiru-animal-training-success", ("command", args.Command.ToString())), uid, user);
    }

    private void OnTrainStress(EntityUid uid, NibiruTamableComponent component, NibiruAnimalTrainStressMessage args)
    {
        var user = args.Actor;
        Log.Debug($"Stress Training: Requesting stress reduction for {ToPrettyString(uid)} by {ToPrettyString(user)}");

        if (component.OwnerUid != user)
        {
            Log.Debug($"Stress Training: Request denied. Owner mismatch.");
            return;
        }

        if (!TryComp<NibiruMountFearComponent>(uid, out var mountFear))
            return;

        if (mountFear.StressTraining >= mountFear.MaxStressTraining)
        {
            Log.Debug($"Stress Training: Stress already at max.");
            return;
        }

        if (component.TrustLevel < 30f)
        {
            Log.Debug($"Stress Training: Not enough trust ({component.TrustLevel}/30).");
            _popup.PopupEntity(Loc.GetString("nibiru-animal-training-trust-low"), uid, user);
            return;
        }

        component.TrustLevel -= 15f;
        mountFear.StressTraining = MathF.Min(mountFear.StressTraining + 10f, mountFear.MaxStressTraining);
        Log.Debug($"Stress Training: Success. Current stress: {mountFear.StressTraining}, Remaining trust: {component.TrustLevel}");
        
        _popup.PopupEntity(Loc.GetString("nibiru-animal-training-stress-success"), uid, user);
        
        UpdateUiState(uid, component);
    }
}
