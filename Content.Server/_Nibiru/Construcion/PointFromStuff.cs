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

public sealed partial class PointsFromStuffSystem : EntitySystem
{
    [Dependency] private ResearchSystem _research = default!;

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
            var paid = new HashSet<EntityUid>();
            var query = EntityQueryEnumerator<FactionComponent>();
            while (query.MoveNext(out _, out var faction))
            {
                if (faction.ResearchServer is not { } server || !paid.Add(server))
                    continue;

                if (!TryComp<ResearchServerComponent>(server, out var research))
                    continue;

                _research.ModifyServerPoints(server, 1, research);
            }
        }
    }

    private void OnMobStateChanged(EntityUid uid, PointsFromKillComponent component, MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead || args.OldMobState >= args.NewMobState)
            return;

		if(TryComp<FactionComponent>(args.Origin, out var user)
		&& TryComp<FactionComponent>(user.ResearchServer, out var server)
		&& TryComp<ResearchServerComponent>(user.ResearchServer, out var research)
		&& server.FactionName == user.FactionName)
		{
            _research.ModifyServerPoints(user.ResearchServer.Value, component.Points, research);
		}
    }

    private void OnHarvest(EntityUid user, FactionComponent comp, HarvestPlantMessage msg)
    {
        if (comp.ResearchServer == null)
            return;

        if (TryComp<FactionComponent>(comp.ResearchServer, out var server)
        && TryComp<ResearchServerComponent>(comp.ResearchServer, out var research)
        && server.FactionName == comp.FactionName)
        {
            _research.ModifyServerPoints(comp.ResearchServer.Value, msg._seed.Points, research);
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
