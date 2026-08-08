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

public sealed partial class NibiruAnimalTrainingSystem : EntitySystem
{
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

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

        // Only the "Follow" command is available in the context menu
        // It also activates the action panel
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
        if (component.OwnerUid != user || !component.Trainable)
        {
            return;
        }

        if (!component.PossibleCommands.Contains(args.Command))
        {
            return;
        }

        if (component.LearnedCommands.Contains(args.Command))
        {
            return;
        }

        // Требования к доверию для команд
        float requiredTrust = args.Command switch
        {
            NibiruAnimalCommand.Follow => 50f,
            NibiruAnimalCommand.Stay => 75f,
            NibiruAnimalCommand.Attack => 100f,
            NibiruAnimalCommand.Grab => 110f,
            NibiruAnimalCommand.Guard => 120f,
            NibiruAnimalCommand.Search => 90f,
            _ => 50f
        };

        if (component.TrustLevel < requiredTrust)
        {
            _popup.PopupEntity(Loc.GetString("nibiru-animal-training-trust-low"), uid, user);
            return;
        }

        component.LearnedCommands.Add(args.Command);
        component.TrustLevel -= 30f; // Training commands "spends" trust

        RaiseLocalEvent(component.OwnerUid.Value, new NibiruAnimalCommandLearnedEvent(uid, args.Command));

        UpdateUiState(uid, component);
        _popup.PopupEntity(Loc.GetString("nibiru-animal-training-success", ("command", args.Command.ToString())), uid, user);
    }

    private void OnTrainStress(EntityUid uid, NibiruTamableComponent component, NibiruAnimalTrainStressMessage args)
    {
        var user = args.Actor;
        if (component.OwnerUid != user)
        {
            return;
        }

        if (!TryComp<NibiruMountFearComponent>(uid, out var mountFear))
            return;

        if (mountFear.StressTraining >= mountFear.MaxStressTraining)
        {
            return;
        }

        if (component.TrustLevel < 30f)
        {
            _popup.PopupEntity(Loc.GetString("nibiru-animal-training-trust-low"), uid, user);
            return;
        }

        component.TrustLevel -= 15f;
        mountFear.StressTraining = MathF.Min(mountFear.StressTraining + 10f, mountFear.MaxStressTraining);

        _popup.PopupEntity(Loc.GetString("nibiru-animal-training-stress-success"), uid, user);

        UpdateUiState(uid, component);
    }
}
