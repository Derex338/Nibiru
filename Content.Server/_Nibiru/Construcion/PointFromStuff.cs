using Content.Server.NPC.HTN;
using Content.Shared.Mobs;
using Robust.Shared.Player;
using Content.Server.KillTracking;
using Content.Shared._Nibiru.Factions;
using Content.Shared._Nibiru.Research.Components;
using Content.Shared.Research.Components;
using Content.Server._Nibiru.Research.Components;
using Robust.Shared.IoC;
using Content.Server.Research.Systems;

namespace Content.Server._Nibiru.Research;

public sealed class PointsFromStuffSystem : EntitySystem
{
    [Dependency] private readonly ResearchSystem _research = default!;

    private float _accumulator;

    public override void Initialize()
    {
        SubscribeLocalEvent<PointsFromKillComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<FactionComponent, HarvestPlantMessage>(OnHarvest);
        //SubscribeLocalEvent<PointsFromDestructionComponent, DestructionEventArgs>(OnDestruction);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _accumulator += frameTime;

        while (_accumulator >= 10.0f)
        {
            _accumulator -= 10.0f;
            var query = EntityQueryEnumerator<ResearchServerComponent>();
            while (query.MoveNext(out var uid, out var research))
            {
                _research.ModifyServerPoints(uid, 1, research);
            }
        }
    }

    private void OnMobStateChanged(EntityUid uid, PointsFromKillComponent component, MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead || args.OldMobState >= args.NewMobState)
            return;

		if(EntityManager.TryGetComponent<FactionComponent>(args.Origin, out var user)
		&& EntityManager.TryGetComponent<FactionComponent>(user.ResearchServer, out var server)
		&& EntityManager.TryGetComponent<ResearchServerComponent>(user.ResearchServer, out var research)
		&& server.FactionName == user.FactionName)
		{
            research.Points += component.Points;
            //_research.ModifyServerPoints(user.ResearchServer.Value, component.Points, research);
		}
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
            //_research.ModifyServerPoints(comp.ResearchServer.Value, msg._seed.Points, research);
        }
    }
    //private void OnDestruction(EntityUid uid, PointsFromDestructionComponent component, DestructionEventArgs args)
    //{
    //    if (component.CurrentOre == null)
    //        return;

    //    var proto = _proto.Index<OrePrototype>(component.CurrentOre);

    //    if (proto.OreEntity == null)
    //        return;

    //    var coords = Transform(uid).Coordinates;
    //    var toSpawn = _random.Next(proto.MinOreYield, proto.MaxOreYield + 1);
    //    for (var i = 0; i < toSpawn; i++)
    //    {
    //        Spawn(proto.OreEntity, coords.Offset(_random.NextVector2(0.2f)));
    //    }
    //}
}
