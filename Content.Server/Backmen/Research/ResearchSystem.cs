using Content.Server.Research.Components;
using Content.Shared.Research.Components;
using Content.Shared.Research.Prototypes;
using System.Linq;
using Content.Shared.Backmen.Research;
using System.Security.Cryptography.X509Certificates;
using Content.Shared.Popups;

// ReSharper disable once CheckNamespace
namespace Content.Server.Research.Systems;

// Little restruct from Nibiru
public sealed partial class ResearchSystem
{
    private void InitializeBkm()
    {
        SubscribeLocalEvent<ResearchServerComponent, MapInitEvent>(OnServerInit);
        SubscribeLocalEvent<ResearchConsoleComponent, ConsoleChangeEpochMessage>(OnConsoleChangeEpoch);
    }

    private void UpdateFancyConsoleInterface(EntityUid uid,
        ResearchConsoleComponent? component = null,
        ResearchClientComponent? clientComponent = null)
    {
        if (!Resolve(uid, ref component, ref clientComponent, false))
            return;

        Dictionary<string, ResearchAvailability> techList;
        var points = 0;
        var currentEpoch = "StoneAge";
        var unlockedEpochs = new List<string> { "StoneAge" };

        if (TryGetClientServer(uid, out var serverUid, out var server, clientComponent) &&
            TryComp<TechnologyDatabaseComponent>(serverUid, out var db))
        {
            // Получаем данные эпох
            currentEpoch = db.CurrentEpoch;
            unlockedEpochs = new List<string>(db.UnlockedEpochs);

            // Получаем технологии ТОЛЬКО для текущей эпохи
            var allTechs = PrototypeManager.EnumeratePrototypes<TechnologyPrototype>()
                .Where(t => t.Epoch == currentEpoch) // Фильтруем по эпохе
                .ToList();

            var unlockedTechs = new HashSet<string>(db.UnlockedTechnologies);

            techList = allTechs.ToDictionary(
                proto => proto.ID,
                proto =>
                {
                    if (unlockedTechs.Contains(proto.ID))
                        return ResearchAvailability.Researched;

                    var prereqsMet = proto.TechnologyPrerequisites.All(p => unlockedTechs.Contains(p));
                    var canAfford = server.Points >= proto.Cost;

                    return prereqsMet
                        ? (canAfford ? ResearchAvailability.Available : ResearchAvailability.PrereqsMet)
                        : ResearchAvailability.Unavailable;
                });

            points = clientComponent.ConnectedToServer ? server.Points : 0;
        }
        else
        {
            techList = new Dictionary<string, ResearchAvailability>();
        }

        _uiSystem.SetUiState(uid,
            ResearchConsoleUiKey.Key,
            new ResearchConsoleBoundInterfaceState(points, techList, currentEpoch, unlockedEpochs));
    }

    private void OnServerInit(Entity<ResearchServerComponent> ent, ref MapInitEvent args)
    {
        if (!TryComp<TechnologyDatabaseComponent>(ent, out var techBase))
            return;

        foreach (var tech in techBase.RoundstartTechnologies)
        {
            AddTechnology(ent, tech, techBase);
        }
    }

    private void OnConsoleChangeEpoch(EntityUid uid, ResearchConsoleComponent component, ConsoleChangeEpochMessage args)
    {
        if (!TryGetClientServer(uid, out var serverUid, out var server))
            return;

        if (!TryComp<TechnologyDatabaseComponent>(serverUid, out var database))
            return;

        // Проверяем, разблокирована ли эпоха
        if (!database.UnlockedEpochs.Contains(args.EpochId))
            return;

        // Меняем эпоху
        SetCurrentEpoch(serverUid.Value, args.EpochId, database);

        // Обновляем все консоли
        foreach (var client in server.Clients)
        {
            if (HasComp<ResearchConsoleComponent>(client))
            {
                UpdateFancyConsoleInterface(client);
            }
        }
    }
}
