using Content.Server._Nibiru.NPC.Systems.Behavior;
using Content.Server._Nibiru.NPC.Systems.Commands;
using Content.Server._Nibiru.NPC.Systems.Utility;
using Content.Server.NPC.Systems;
using Content.Server.Parallax;
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
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Content.Shared.Interaction.Events;

namespace Content.Server._Nibiru.NPC.Systems.Training;

/// <summary>
/// Manages taming animals through feeding.
/// Handles trust increase/decrease, owner binding,
/// and switching NPC to following mode after taming.
/// </summary>
public sealed partial class NibiruTamingSystem : EntitySystem
{
    private static readonly ProtoId<TagPrototype> FoodTag = "Food";
    private static readonly ProtoId<TagPrototype> MeatTag = "Meat";
    private static readonly ProtoId<TagPrototype> PlantTag = "Plant";
    private static readonly ProtoId<TagPrototype> FruitTag = "Fruit";
    private static readonly ProtoId<TagPrototype> VegetableTag = "Vegetable";

    [Dependency] private NpcFactionSystem _faction = default!;
    [Dependency] private NibiruAnimalSoundSystem _sounds = default!;
    [Dependency] private NibiruAnimalMoodSystem _mood = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private NPCSteeringSystem _steering = default!;
    [Dependency] private TagSystem _tag = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private BiomeSystem _biome = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NibiruTamableComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<NibiruTamableComponent, NibiruAnimalFeedingDoAfterEvent>(OnFeedingDoAfter);
        SubscribeLocalEvent<NibiruTamableComponent, DamageChangedEvent>(OnDamaged);
    }

    /// <summary>
    /// Petting the animal (UseInHand / Z) - small trust increase.
    /// Compatible with PettableFriendSystem: if the animal has PettableFriendComponent,
    /// PettableSystem handles the friendship, and we just add trust.
    /// </summary>
    /// слишком имба
    private void OnInteractHand(EntityUid uid, NibiruTamableComponent component, InteractHandEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<MobStateComponent>(uid, out var mobState) || mobState.CurrentState != MobState.Alive)
            return;

        // Can pet only tamed or partially trusted animals
        if (!component.IsTamed && component.TrustLevel < component.TrustThreshold * 0.3f)
            return;

        // Gain trust for petting (10% of standard feeding)
        var trustGain = component.TrustPerFeeding * 0.1f;
        component.TrustLevel = MathF.Min(component.TrustLevel + trustGain, component.MaxTrust);

        Spawn("EffectHearts", Transform(uid).Coordinates);
    }

    /// <summary>
    /// Handles feeding: player uses food on animal.
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

        // Check if item is acceptable food
        if (!IsAcceptableFood(args.Used, component))
        {
            return;
        }

        args.Handled = true;

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

        if (!IsAcceptableFood(args.Used.Value, component))
        {
            return;
        }

        var trustGain = component.TrustPerFeeding;

        // Double trust for favorite food
        if (IsFavoriteFood(args.Used.Value, component))
            trustGain *= 2f;

        // Gain trust
        component.TrustLevel = MathF.Min(component.TrustLevel + trustGain, component.MaxTrust);

        _sounds.PlayFeedingSound(uid);

        _mood.OnFed(uid);

        // Check if animal is tamed
        if (!component.IsTamed && component.TrustLevel >= component.TrustThreshold)
        {
            TameAnimal(uid, args.User, component);
        }

        // Consume food
        QueueDel(args.Used.Value);
        args.Handled = true;
    }

    /// <summary>
    /// Penalty for owner aggression.
    /// </summary>
    private void OnDamaged(EntityUid uid, NibiruTamableComponent component, DamageChangedEvent args)
    {
        if (!args.DamageIncreased || args.Origin == null)
            return;

        // If owner hits the animal - lose trust
        if (component.IsTamed && component.OwnerUid == args.Origin)
        {
            component.TrustLevel = MathF.Max(0, component.TrustLevel - component.TrustPenaltyOnHit);

            // If trust drops below half threshold - animal becomes wild
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
            // Smooth trust decay
            if (tamable.IsTamed && tamable.TrustLevel > 0)
            {
                tamable.TrustLevel = MathF.Max(0, tamable.TrustLevel - tamable.TrustDecayRate * frameTime);

                // Wilding when trust is completely lost
                if (tamable.TrustLevel <= 0)
                    UntameAnimal(uid, tamable);
            }

        }
    }

    private void TameAnimal(EntityUid uid, EntityUid owner, NibiruTamableComponent component)
    {
        component.IsTamed = true;
        component.OwnerUid = owner;
        _biome.ClaimBiomeMob(uid);

        _faction.IgnoreEntity(uid, owner);

        _sounds.PlayTamedSound(uid);

        // Heart effect for taming!
        Spawn("EffectHearts", Transform(uid).Coordinates);

        // Add base commands
        LearnCommand(uid, component, NibiruAnimalCommand.Follow);
        LearnCommand(uid, component, NibiruAnimalCommand.Stay);

        // Switch to following mode
        if (TryComp<NibiruNpcStateMachineComponent>(uid, out var behavior))
        {
            behavior.CurrentTarget = owner;
            behavior.CurrentState = NibiruNpcState.Following;
        }

        // Add animal to commander group
        IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<NibiruAnimalCommanderSystem>().AddAnimal(owner, uid);
    }

    private void UntameAnimal(EntityUid uid, NibiruTamableComponent component)
    {
        var prevOwner = component.OwnerUid;
        component.IsTamed = false;
        component.OwnerUid = null;
        component.TrustLevel = 0;

        // Remove friendly tag
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
        if (!TryComp(item, out MetaDataComponent? meta) || meta.EntityPrototype == null)
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
        if (!_tag.HasTag(item, FoodTag))
            return false;

        // If there is a specific list - eat only this
        if (component.AcceptedFood != null && component.AcceptedFood.Count > 0)
        {
            if (!TryComp(item, out MetaDataComponent? meta) || meta.EntityPrototype == null)
                return false;
            return component.AcceptedFood.Contains(meta.EntityPrototype.ID);
        }

        // Check by diet type
        switch (component.Diet)
        {
            case NibiruAnimalDiet.Carnivore:
                return _tag.HasTag(item, MeatTag); // Carnivores eat meat

            case NibiruAnimalDiet.Herbivore:
                // Herbivores do not eat meat
                return !_tag.HasTag(item, MeatTag) && (_tag.HasTag(item, PlantTag) || _tag.HasTag(item, FruitTag) || _tag.HasTag(item, VegetableTag));

            case NibiruAnimalDiet.Omnivore:
            default:
                return true; // Omnivores eat everything with Food tag
        }
    }

    /// <summary>
    /// Give animal command. Only for learned commands.
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

                if (!TryComp(target.Value, out TransformComponent? _))
                    return false;

                if (command == NibiruAnimalCommand.Grab && !HasComp<PullableComponent>(target.Value))
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
                // Chasing: animal actively pursues target by scent,
                // but does not attack (handled in ProcessChasing via Search command)
                behavior.CurrentState = NibiruNpcState.Chasing;
                return true;

            case NibiruAnimalCommand.Deliver:
                // Open UI for player
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
