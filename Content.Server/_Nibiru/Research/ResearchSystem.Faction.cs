using System.Linq;
using Content.Server._Nibiru.Construction;
using Content.Server.Research.Components;
using Content.Shared._Nibiru.Factions;
using Content.Shared._Nibiru.Research;
using Content.Shared.Construction;
using Content.Shared.Research.Components;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Server.Research.Systems;

public sealed partial class ResearchSystem
{
    public void BindResearchTable(EntityUid table, EntityUid? builder = null)
    {
        if (!TryComp<FactionComponent>(table, out var tableFaction))
            return;

        if (string.IsNullOrWhiteSpace(tableFaction.FactionName))
            return;

        if (!HasComp<ResearchClientComponent>(table))
            return;

        var factionServer = EnsureFactionResearchEntity(tableFaction.FactionName, table, builder);
        MigrateTableIntoFactionServer(table, factionServer);
        BindFactionMembersToServer(tableFaction.FactionName, factionServer);
        ConnectFactionClient(table, factionServer);
        UpdateFancyConsoleInterface(table);
    }

    public void ClearFactionResearch(string factionName)
    {
        if (string.IsNullOrWhiteSpace(factionName))
            return;

        var recipeCheck = EntityManager.System<ConstructionRecipeCheck>();
        var playerQuery = EntityQueryEnumerator<FactionComponent, ActorComponent>();
        while (playerQuery.MoveNext(out var member, out var faction, out var actor))
        {
            if (faction.FactionName != factionName)
                continue;

            faction.ResearchServer = null;
            var crafts = recipeCheck.GetAvailableRecipes(member, faction, faction.StaticPacks);
            RaiseNetworkEvent(new ConstructionCrafts(GetNetEntity(member), crafts), actor.PlayerSession);
        }

        var consoleQuery = EntityQueryEnumerator<ResearchConsoleComponent, FactionComponent, TechnologyDatabaseComponent>();
        while (consoleQuery.MoveNext(out var table, out _, out var faction, out var database))
        {
            if (faction.FactionName != factionName)
                continue;

            UnregisterClient(table);
            ResetTechnologyDatabase(table, database);
            UpdateFancyConsoleInterface(table);
        }

        ClearFactionMembersResearchServer(factionName, null);

        var servers = new List<EntityUid>();
        var markerQuery = EntityQueryEnumerator<FactionResearchServerComponent, FactionComponent>();
        while (markerQuery.MoveNext(out var uid, out _, out var faction))
        {
            if (faction.FactionName == factionName)
                servers.Add(uid);
        }

        foreach (var server in servers)
        {
            QueueDel(server);
        }
    }

    public void MergeTechnologyDatabases(
        EntityUid target,
        TechnologyDatabaseComponent targetDb,
        TechnologyDatabaseComponent sourceDb)
    {
        foreach (var tech in sourceDb.UnlockedTechnologies.ToList())
        {
            if (targetDb.UnlockedTechnologies.Contains(tech))
                continue;

            AddTechnology(target, tech, targetDb);
        }

        foreach (var epoch in sourceDb.UnlockedEpochs.ToList())
        {
            if (targetDb.UnlockedEpochs.Contains(epoch))
                continue;

            targetDb.UnlockedEpochs.Add(epoch);
        }

        Dirty(target, targetDb);
    }

    public void ResetTechnologyDatabase(EntityUid uid, TechnologyDatabaseComponent? database = null)
    {
        if (!Resolve(uid, ref database))
            return;

        var disciplines = database.SupportedDisciplines.ToList();
        database.MainDiscipline = null;
        database.CurrentTechnologyCards = new List<string>();
        database.SupportedDisciplines = disciplines;
        database.UnlockedTechnologies = new List<string>();
        database.UnlockedRecipes = new List<string>();
        database.UnlockedCrafts = new List<string>();
        database.CurrentEpoch = "StoneAge";
        database.UnlockedEpochs = new List<string> { "StoneAge" };
        Dirty(uid, database);
    }

    private void HandleFactionServerShutdown(EntityUid uid)
    {
        if (!HasComp<FactionResearchServerComponent>(uid))
            return;

        if (!TryComp<FactionComponent>(uid, out var faction) || string.IsNullOrWhiteSpace(faction.FactionName))
            return;

        ClearFactionMembersResearchServer(faction.FactionName, uid);
    }

    private void HandleFactionDatabaseModified(EntityUid uid)
    {
        if (!HasComp<FactionResearchServerComponent>(uid))
            return;

        PushFactionCrafts(uid);
    }

    private void HandleFactionConsoleOpened(EntityUid uid, EntityUid actor)
    {
        if (!TryComp<FactionComponent>(actor, out var actorFaction) ||
            string.IsNullOrWhiteSpace(actorFaction.FactionName))
            return;

        var tableFaction = EnsureComp<FactionComponent>(uid);
        if (string.IsNullOrWhiteSpace(tableFaction.FactionName))
            tableFaction.FactionName = actorFaction.FactionName;

        if (tableFaction.FactionName != actorFaction.FactionName)
            return;

        BindResearchTable(uid, actor);
    }

    private EntityUid EnsureFactionResearchEntity(string factionName, EntityUid table, EntityUid? builder)
    {
        var existing = ResolveFactionResearchServer(factionName, builder);
        if (existing != null)
            return existing.Value;

        var mapUid = Transform(table).MapUid;
        if (mapUid == null)
            return table;

        var server = Spawn("FactionResearchServer", new EntityCoordinates(mapUid.Value, default));
        var faction = EnsureComp<FactionComponent>(server);
        faction.FactionName = factionName;

        if (TryComp<TechnologyDatabaseComponent>(table, out var tableDb) &&
            TryComp<TechnologyDatabaseComponent>(server, out var serverDb))
        {
            serverDb.SupportedDisciplines = tableDb.SupportedDisciplines.ToList();
            Dirty(server, serverDb);
        }

        if (TryComp<ResearchServerComponent>(server, out var researchServer))
            researchServer.ServerName = $"{factionName} Research";

        BindFactionMembersToServer(factionName, server);
        return server;
    }

    private void MigrateTableIntoFactionServer(EntityUid table, EntityUid factionServer)
    {
        if (table == factionServer)
            return;

        if (HasComp<ResearchServerComponent>(table))
        {
            if (TryComp<ResearchServerComponent>(table, out var tableServer) &&
                TryComp<ResearchServerComponent>(factionServer, out var factionServerComp) &&
                tableServer.Points != 0)
            {
                ModifyServerPoints(factionServer, tableServer.Points, factionServerComp);
                tableServer.Points = 0;
                Dirty(table, tableServer);
            }

            if (TryComp<TechnologyDatabaseComponent>(table, out var tableDb) &&
                TryComp<TechnologyDatabaseComponent>(factionServer, out var factionDb))
            {
                MergeTechnologyDatabases(factionServer, factionDb, tableDb);
            }

            UnregisterClient(table);
            RemComp<ResearchServerComponent>(table);
        }

        if (TryComp<TechnologyDatabaseComponent>(table, out var clientDb))
            ResetTechnologyDatabase(table, clientDb);
    }

    private void PushFactionCrafts(EntityUid researchServer)
    {
        if (!TryComp<FactionComponent>(researchServer, out var serverFaction))
            return;

        if (string.IsNullOrWhiteSpace(serverFaction.FactionName))
            return;

        var recipeCheck = EntityManager.System<ConstructionRecipeCheck>();
        var query = EntityQueryEnumerator<FactionComponent, ActorComponent>();
        while (query.MoveNext(out var member, out var faction, out var actor))
        {
            if (faction.FactionName != serverFaction.FactionName)
                continue;

            faction.ResearchServer = researchServer;
            var crafts = recipeCheck.GetAvailableRecipes(member, faction, faction.StaticPacks);
            RaiseNetworkEvent(new ConstructionCrafts(GetNetEntity(member), crafts), actor.PlayerSession);
        }
    }

    private EntityUid? ResolveFactionResearchServer(string factionName, EntityUid? hint = null)
    {
        if (hint != null &&
            TryComp<FactionComponent>(hint, out var hintFaction) &&
            hintFaction.FactionName == factionName &&
            hintFaction.ResearchServer is { } hintServer &&
            Exists(hintServer) &&
            HasComp<FactionResearchServerComponent>(hintServer) &&
            HasComp<ResearchServerComponent>(hintServer))
        {
            return hintServer;
        }

        var markerQuery = EntityQueryEnumerator<FactionResearchServerComponent, FactionComponent, ResearchServerComponent>();
        while (markerQuery.MoveNext(out var uid, out _, out var faction, out _))
        {
            if (faction.FactionName == factionName)
                return uid;
        }

        var memberQuery = EntityQueryEnumerator<FactionComponent>();
        while (memberQuery.MoveNext(out _, out var faction))
        {
            if (faction.FactionName != factionName)
                continue;

            if (faction.ResearchServer is not { } server || !Exists(server))
                continue;

            if (!HasComp<FactionResearchServerComponent>(server) || !HasComp<ResearchServerComponent>(server))
                continue;

            return server;
        }

        return null;
    }

    private void BindFactionMembersToServer(string factionName, EntityUid server)
    {
        var query = EntityQueryEnumerator<FactionComponent>();
        while (query.MoveNext(out _, out var faction))
        {
            if (faction.FactionName != factionName)
                continue;

            faction.ResearchServer = server;
        }
    }

    private void ConnectFactionClient(EntityUid client, EntityUid server)
    {
        if (!TryComp<ResearchClientComponent>(client, out var clientComp))
            return;

        if (clientComp.Server == server)
        {
            SyncClientWithServer(client, clientComponent: clientComp);
            return;
        }

        UnregisterClient(client, clientComp);
        RegisterClient(client, server, clientComp);
    }

    private void ClearFactionMembersResearchServer(string factionName, EntityUid? oldServer)
    {
        var query = EntityQueryEnumerator<FactionComponent>();
        while (query.MoveNext(out _, out var faction))
        {
            if (faction.FactionName != factionName)
                continue;

            if (oldServer != null && faction.ResearchServer != oldServer)
                continue;

            faction.ResearchServer = null;
        }
    }
}
