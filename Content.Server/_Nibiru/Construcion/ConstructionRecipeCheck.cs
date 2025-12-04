using Content.Shared.Construction;
using Content.Shared.Construction.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Server.GameObjects;
using Content.Shared.Research.Components;
using Content.Shared._Nibiru.Factions;
using JetBrains.Annotations;
using System.Linq;
﻿using Content.Server.Mind;
using Robust.Shared.Player;
using Robust.Shared.Map;
using Content.Shared._Nibiru.Workbench;
using Content.Shared.Lathe;
using Content.Shared.UserInterface;

namespace Content.Server._Nibiru.Construction;

/// <summary>
/// The server-side implementation of the construction system, which is used for return unloced recipes to client.
/// </summary>
[UsedImplicitly]
public sealed partial class ConstructionRecipeCheck : SharedConstructionSystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly MindSystem _minds = default!;
    [Dependency] private readonly ISharedPlayerManager _player = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly UserInterfaceSystem _uiSys = default!;

    private static readonly HashSet<Entity<TechnologyDatabaseComponent>> ClientLookup = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<ConstructionUIOpen>(OnRequestCraftsInfoEvent);
        SubscribeLocalEvent<TechnologyDatabaseComponent, CraftsGetRecipesEvent>(OnGetRecipes);

        SubscribeLocalEvent<WorkbenchComponent, RequestRecipesWorkbenchMessage>(OnRequestRecipesWorkbench);
        SubscribeLocalEvent<WorkbenchComponent, BeforeActivatableUIOpenEvent>((u, c, _) => UpdateUI(u, c));
    }

    private void OnRequestCraftsInfoEvent(ConstructionUIOpen msg, EntitySessionEventArgs args)
    {
        if (!args.SenderSession.AttachedEntity.HasValue || args.SenderSession.AttachedEntity != GetEntity(msg.NetEntity))
            return;

        var entity = args.SenderSession.AttachedEntity.Value;
        if (!EntityManager.TryGetComponent<FactionComponent>(entity, out var comp))
            return;

        comp.StaticPacks.Add(new ProtoId<ConstructionPackPrototype>("FactionBase"));

        List<ProtoId<ConstructionPrototype>> crafts = GetAvailableRecipes(entity, comp, comp.StaticPacks);

        RaiseNetworkEvent(new ConstructionCrafts(GetNetEntity(entity), crafts), args.SenderSession);
    }

    private void OnRequestRecipesWorkbench(EntityUid uid, WorkbenchComponent component, RequestRecipesWorkbenchMessage msg)
    {
        UpdateUI(uid, component);
    }

    private void UpdateUI(EntityUid uid, WorkbenchComponent component)
    {
        if (!EntityManager.TryGetComponent<FactionComponent>(uid, out var comp))
            return;

        var state = new WorkbenchUpdateState(GetAvailableRecipes(uid, comp, component.StaticPacks));
        _uiSys.SetUiState(uid, WorkbenchUiKey.Key, state);
    }

    public List<ProtoId<ConstructionPrototype>> GetAvailableRecipes(EntityUid uid, FactionComponent comp, List<ProtoId<ConstructionPackPrototype>> packs, bool getUnavailable = false)
    {
        var ev = new CraftsGetRecipesEvent((uid, comp), getUnavailable);

        if (EntityManager.TryGetComponent<FactionComponent>(uid, out var Player)
        && Player.ResearchServer is null)
        {
            var allServers = GetServers(uid).ToList();

            foreach (var server in allServers)
            {
                if (EntityManager.TryGetComponent<FactionComponent>(server, out var Serv)
                && Serv.FactionName == Player.FactionName)
                {
                    Player.ResearchServer = server;
                    RaiseLocalEvent(server, ev);

                    break;
                }
            }
        }
        else if (EntityManager.TryGetComponent<FactionComponent>(uid, out var PlayerHui)
        && PlayerHui.ResearchServer is { } serverUid
        && EntityManager.TryGetComponent<FactionComponent>(serverUid, out var server)
        && server.FactionName == PlayerHui.FactionName)
        {
            RaiseLocalEvent(serverUid, ev);
        }

        AddRecipesFromPacks(ev.Recipes, packs);
        return ev.Recipes.ToList();
    }

    public void AddRecipesFromPacks(HashSet<ProtoId<ConstructionPrototype>> recipes, List<ProtoId<ConstructionPackPrototype>> packs)
    {
        foreach (var id in packs)
        {
            var pack = _proto.Index(id);
            recipes.UnionWith(pack.Recipes);
        }
    }

    public void OnGetRecipes(EntityUid uid, TechnologyDatabaseComponent component, CraftsGetRecipesEvent args)
    {
        if (EntityManager.TryGetComponent<FactionComponent>(uid, out var Server)
            && EntityManager.TryGetComponent<FactionComponent>(args.User, out var Player)
            && Server.FactionName == Player.FactionName)
        {
            foreach (var recipe in component.UnlockedCrafts)
            {
                if (_proto.TryIndex<ConstructionPrototype>(recipe, out var comp)
                    && comp.EntitysToShowRecipe.Count > 0
                    && EntityManager.TryGetComponent<MetaDataComponent>(args.User, out var meta)
                    && meta.EntityPrototype != null)
                {
                    foreach (var entity in comp.EntitysToShowRecipe)
                    {
                        if (entity == meta.EntityPrototype.ID)
                        {
                            args.Recipes.Add(recipe);
                        }
                    }

                    return;
                }
                else
                    args.Recipes.Add(recipe);
            }
        }
    }

    public HashSet<Entity<TechnologyDatabaseComponent>> GetServers(EntityUid client)
    {
        ClientLookup.Clear();

        var clientXform = Transform(client);
        if (clientXform.GridUid is not { } grid)
            return ClientLookup;

        _lookup.GetGridEntities(grid, ClientLookup);
        return ClientLookup;
    }
}
