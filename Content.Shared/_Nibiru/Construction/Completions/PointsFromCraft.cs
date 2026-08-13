using Content.Shared.Construction;
using Content.Shared._Nibiru.Factions;
using Content.Shared.Research.Components;

namespace Content.Shared._Nibiru.Construction.Completions;

[DataDefinition]
public sealed partial class PointsFromCraft : IGraphAction
{
    [DataField("points")]
    public int Points = 0;

    [DataField("decreasing")]
    public bool Decreasing = false;

    public void PerformAction(EntityUid uid, EntityUid? userUid, IEntityManager entityManager)
    {
        if (userUid == null)
            return;

        var points = Points;

        if (Decreasing)
        {
            var count = 0;
            var prototype = entityManager.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID;
            if (prototype != null)
            {
                var query = entityManager.EntityQueryEnumerator<MetaDataComponent>();
                while (query.MoveNext(out _, out var meta))
                {
                    if (meta.EntityPrototype?.ID == prototype)
                        count++;
                }
            }

            if (count > 0)
            {
                points = Points / count;
            }
        }

        if (entityManager.TryGetComponent<FactionComponent>(userUid, out var faction) && faction.ResearchServer != null)
        {
            if (entityManager.TryGetComponent<ResearchServerComponent>(faction.ResearchServer.Value, out var server))
            {
                server.Points += points;
                entityManager.Dirty(faction.ResearchServer.Value, server);
                var ev = new ResearchServerPointsChangedEvent(faction.ResearchServer.Value, server.Points, points);
                foreach (var client in server.Clients)
                {
                    entityManager.EventBus.RaiseLocalEvent(client, ref ev);
                }
            }
        }
    }
}
