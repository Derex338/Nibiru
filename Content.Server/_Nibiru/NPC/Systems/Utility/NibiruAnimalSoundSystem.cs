using Content.Shared._Nibiru.NPC.Behavior.Events;
using Content.Shared._Nibiru.NPC.Behavior.Components;
using Content.Shared._Nibiru.NPC.Training;
using Content.Shared._Nibiru.NPC.Livestock;
using Content.Shared._Nibiru.NPC.Utility;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;
using Content.Server._Nibiru.NPC.Systems.Behavior;
using Content.Shared._Nibiru.NPC.Commands;
using Content.Shared._Nibiru.NPC.Behavior;

namespace Content.Server._Nibiru.NPC.Systems.Utility;

/// <summary>
/// Воспроизводит звуки животных в зависимости от их состояния и действий.
/// </summary>
public sealed partial class NibiruAnimalSoundSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NibiruAnimalAbilityComponent, AnimalGrowlEvent>(OnGrowl);
    }

    public void PlayHurtSound(EntityUid uid, NibiruNpcAudioComponent component)
    {
        if (component.HurtSound != null)
            _audio.PlayPvs(component.HurtSound, uid);
    }

    public void PlayAggroSound(EntityUid uid, NibiruNpcAudioComponent component)
    {
        if (component.AggroSound != null)
            _audio.PlayPvs(component.AggroSound, uid);
    }

    public void PlayFleeSound(EntityUid uid, NibiruNpcAudioComponent component)
    {
        if (component.FleeSound != null)
            _audio.PlayPvs(component.FleeSound, uid);
    }

    public void PlayDeathSound(EntityUid uid, NibiruNpcAudioComponent component)
    {
        if (component.DeathSound != null)
            _audio.PlayPvs(component.DeathSound, uid);
    }



    private void OnGrowl(EntityUid uid, NibiruAnimalAbilityComponent component, AnimalGrowlEvent args)
    {
        if (component.GrowlSound != null)
            _audio.PlayPvs(component.GrowlSound, uid);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Обновленный цикл с использованием новых компонентов
        var query = EntityQueryEnumerator<NibiruNpcStateMachineComponent, NibiruNpcAudioComponent>();
        while (query.MoveNext(out var uid, out var state, out var audio))
        {
            // Звуки состояний (рычание при погоне, визг при бегстве)
            switch (state.CurrentState)
            {
                case NibiruNpcState.Chasing:
                    if (audio.AggroSound != null && _random.Prob(0.005f))
                        _audio.PlayPvs(audio.AggroSound, uid);
                    break;
                case NibiruNpcState.Fleeing:
                    if (audio.FleeSound != null && _random.Prob(0.005f))
                        _audio.PlayPvs(audio.FleeSound, uid);
                    break;
                case NibiruNpcState.Idle:
                case NibiruNpcState.Patrolling:
                case NibiruNpcState.Following:
                    if (audio.AmbientSound != null)
                    {
                        audio.AmbientSoundAccumulator += frameTime;
                        if (audio.AmbientSoundAccumulator >= audio.AmbientSoundInterval)
                        {
                            audio.AmbientSoundAccumulator = 0f;
                            if (_random.Prob(0.3f))
                                _audio.PlayPvs(audio.AmbientSound, uid);
                        }
                    }
                    break;
            }
        }

        // Звуки сна
        var sleepQuery = EntityQueryEnumerator<NibiruSleepCycleComponent>();
        while (sleepQuery.MoveNext(out var uid, out var sleep))
        {
            if (sleep.IsSleeping && sleep.SleepingSound != null && _random.Prob(0.002f))
                _audio.PlayPvs(sleep.SleepingSound, uid);
        }
    }

    /// <summary>
    /// Воспроизводит звук кормления.
    /// </summary>
    public void PlayFeedingSound(EntityUid uid)
    {
        if (TryComp<NibiruTamableComponent>(uid, out var tamable) && tamable.FeedingSound != null)
            _audio.PlayPvs(tamable.FeedingSound, uid);
    }

    /// <summary>
    /// Воспроизводит звук приручения.
    /// </summary>
    public void PlayTamedSound(EntityUid uid)
    {
        if (TryComp<NibiruTamableComponent>(uid, out var tamable) && tamable.TamedSound != null)
            _audio.PlayPvs(tamable.TamedSound, uid);
    }

    /// <summary>
    /// Воспроизводит звук стрижки.
    /// </summary>
    public void PlayShearingSound(EntityUid uid)
    {
        if (TryComp<NibiruLivestockComponent>(uid, out var livestock) && livestock.ShearingSound != null)
            _audio.PlayPvs(livestock.ShearingSound, uid);
    }

    /// <summary>
    /// Воспроизводит звук дойки.
    /// </summary>
    public void PlayMilkingSound(EntityUid uid)
    {
        if (TryComp<NibiruLivestockComponent>(uid, out var livestock) && livestock.MilkingSound != null)
            _audio.PlayPvs(livestock.MilkingSound, uid);
    }

    /// <summary>
    /// Воспроизводит звук рождения.
    /// </summary>
    public void PlayBirthSound(EntityUid uid)
    {
        if (TryComp<NibiruLivestockComponent>(uid, out var livestock) && livestock.BirthSound != null)
            _audio.PlayPvs(livestock.BirthSound, uid);
    }

    /// <summary>
    /// Воспроизводит звук привязывания.
    /// </summary>
    public void PlayLeashSound(EntityUid uid)
    {
        if (TryComp<NibiruLeashableComponent>(uid, out var leash) && leash.LeashSound != null)
            _audio.PlayPvs(leash.LeashSound, uid);
    }
}
