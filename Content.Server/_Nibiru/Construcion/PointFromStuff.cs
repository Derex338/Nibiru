using Content.Server.NPC.HTN;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Player;
using Content.Server.KillTracking;
using Content.Shared._Nibiru.Factions;
using Content.Shared._Nibiru.Research.Components;
using Content.Shared.Research.Components;
using Content.Server._Nibiru.Research.Components;

namespace Content.Server._Nibiru.Research;

public sealed class PointsFromStuffSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<PointsFromKillComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<FactionComponent, HarvestPlantMessage>(OnHarvest);
    }

    private void OnMobStateChanged(EntityUid uid, PointsFromKillComponent component, MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead || args.OldMobState >= args.NewMobState)
            return;

        // impulse is the entity that did the finishing blow.
        var killImpulse = GetKillSource(args.Origin);

        KillSource killSource;

        // the impulse gets the kill and the most damage gets the assist
        killSource = killImpulse;

        // it's a suicide if:
        // - you caused your own death
        // - the kill source was the entity that died
        // - the entity that died had an assist on themselves
        var suicide = args.Origin == uid ||
                      killSource is KillNpcSource npc && npc.NpcEnt == uid ||
                      killSource is KillPlayerSource player && player.PlayerId == CompOrNull<ActorComponent>(uid)?.PlayerSession.UserId;

		if(EntityManager.TryGetComponent<FactionComponent>(args.Origin, out var user)
		&& EntityManager.TryGetComponent<FactionComponent>(user.ResearchServer, out var server)
		&& EntityManager.TryGetComponent<ResearchServerComponent>(user.ResearchServer, out var research)
		&& server.FactionName == user.FactionName)
		{
			research.Points += component.Points;
		}
    }

    private KillSource GetKillSource(EntityUid? sourceEntity)
    {
        if (TryComp<ActorComponent>(sourceEntity, out var actor))
            return new KillPlayerSource(actor.PlayerSession.UserId);
        if (HasComp<HTNComponent>(sourceEntity))
            return new KillNpcSource(sourceEntity.Value);
        return new KillEnvironmentSource();
    }

    private void OnHarvest(EntityUid user, FactionComponent comp, HarvestPlantMessage msg)
    {
        if (comp.ResearchServer == null)
            return;

        if (EntityManager.TryGetComponent<FactionComponent>(comp.ResearchServer, out var server)
        && EntityManager.TryGetComponent<ResearchServerComponent>(comp.ResearchServer, out var research)
        && server.FactionName == comp.FactionName)
        {
            research.Points += msg._seed.Points;
        }
    }
}
