using Content.Shared._Nibiru.NPC.Behavior.Components;
using Content.Shared._Nibiru.NPC.Training;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Interaction;
using Content.Server._Nibiru.NPC.Systems.Utility;
using Content.Shared._Nibiru.NPC.Behavior;

namespace Content.Server._Nibiru.NPC.Systems.Behavior;

/// <summary>
/// Manages the mood of tamed animals.
/// Mood decreases over time, increases when fed and petted.
/// If the mood is low, the animal may stop obeying or become wild.
/// </summary>
public sealed partial class NibiruAnimalMoodSystem : EntitySystem
{
    [Dependency] private NibiruAnimalSoundSystem _sounds = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NibiruAnimalMoodComponent, InteractHandEvent>(OnPetted);
        SubscribeLocalEvent<NibiruAnimalMoodComponent, DamageChangedEvent>(OnDamaged);
    }

    /// <summary>
    /// Petting — interaction with an empty hand.
    /// </summary>
    private void OnPetted(EntityUid uid, NibiruAnimalMoodComponent component, InteractHandEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<NibiruTamableComponent>(uid, out var tamable) || !tamable.IsTamed)
            return;

        // Only owner can pet with a positive effect
        if (tamable.OwnerUid != args.User)
            return;

        component.Mood = MathF.Min(component.Mood + component.MoodPerPetting, component.MaxMood);
        UpdateMoodState(uid, component);
        args.Handled = true;
    }

    private void OnDamaged(EntityUid uid, NibiruAnimalMoodComponent component, DamageChangedEvent args)
    {
        if (!args.DamageIncreased || args.Origin == null)
            return;

        if (!TryComp<NibiruTamableComponent>(uid, out var tamable) || !tamable.IsTamed)
            return;

        // Strike from owner reduces mood
        if (tamable.OwnerUid == args.Origin)
        {
            component.Mood = MathF.Max(0, component.Mood - component.MoodPenaltyOnHit);
            UpdateMoodState(uid, component);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<NibiruAnimalMoodComponent, NibiruTamableComponent>();
        while (query.MoveNext(out var uid, out var mood, out var tamable))
        {
            if (!tamable.IsTamed)
                continue;

            // Smooth mood decrease
            mood.Mood = MathF.Max(0, mood.Mood - mood.MoodDecayRate * frameTime);
            UpdateMoodState(uid, mood);

            // When wild, completely reset taming
            if (mood.MoodState == AnimalMoodState.Wild)
            {
                tamable.IsTamed = false;
                tamable.OwnerUid = null;
                tamable.TrustLevel = 0;

                if (TryComp<NibiruNpcStateMachineComponent>(uid, out var state))
                {
                    state.CurrentTarget = null;
                    state.CurrentState = NibiruNpcState.Idle;
                }
            }
        }
    }

    /// <summary>
    /// Updates the qualitative state of mood based on a numerical value.
    /// </summary>
    private void UpdateMoodState(EntityUid uid, NibiruAnimalMoodComponent mood)
    {
        var ratio = mood.Mood / mood.MaxMood;

        mood.MoodState = ratio switch
        {
            >= 0.75f => AnimalMoodState.Happy,
            >= 0.5f => AnimalMoodState.Content,
            >= 0.25f => AnimalMoodState.Sad,
            > 0.1f => AnimalMoodState.Angry,
            _ => AnimalMoodState.Wild
        };
    }

    /// <summary>
    /// Increases mood when fed (called from NibiruTamingSystem).
    /// </summary>
    public void OnFed(EntityUid uid, NibiruAnimalMoodComponent? mood = null)
    {
        if (!Resolve(uid, ref mood, false))
            return;

        mood.Mood = MathF.Min(mood.Mood + mood.MoodPerFeeding, mood.MaxMood);
        UpdateMoodState(uid, mood);
    }
}
