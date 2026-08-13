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
public sealed partial class ConstructionRecipeCheck : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private MindSystem _minds = default!;
    [Dependency] private ISharedPlayerManager _player = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private UserInterfaceSystem _uiSys = default!;

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
        if (!TryComp<FactionComponent>(entity, out var comp))
            return;

        // comp.StaticPacks.Add(new ProtoId<ConstructionPackPrototype>("FactionBase"));

        List<ProtoId<ConstructionPrototype>> crafts = GetAvailableRecipes(entity, comp, comp.StaticPacks);

        RaiseNetworkEvent(new ConstructionCrafts(GetNetEntity(entity), crafts), args.SenderSession);
    }

    private void OnRequestRecipesWorkbench(EntityUid uid, WorkbenchComponent component, RequestRecipesWorkbenchMessage msg)
    {
        UpdateUI(uid, component);
    }

    private void UpdateUI(EntityUid uid, WorkbenchComponent component)
    {
        if (!TryComp<FactionComponent>(uid, out var comp))
            return;

        var state = new WorkbenchUpdateState(GetAvailableRecipes(uid, comp, component.StaticPacks));
        _uiSys.SetUiState(uid, WorkbenchUiKey.Key, state);
    }

    public List<ProtoId<ConstructionPrototype>> GetAvailableRecipes(EntityUid uid, FactionComponent comp, List<ProtoId<ConstructionPackPrototype>> packs, bool getUnavailable = false)
    {
        var ev = new CraftsGetRecipesEvent((uid, comp), getUnavailable);

        if (!TryComp<FactionComponent>(uid, out var player))
        {
            AddRecipesFromPacks(ev.Recipes, packs);
            return FilterRecipes(ev.Recipes, comp.FactionName);
        }

        if (player.ResearchServer is null || !Exists(player.ResearchServer) || !HasComp<TechnologyDatabaseComponent>(player.ResearchServer.Value))
        {
            player.ResearchServer = null;

            foreach (var server in GetServers(uid))
            {
                if (!TryComp<FactionComponent>(server, out var serverFaction) ||
                    serverFaction.FactionName != player.FactionName)
                    continue;

                if (!HasComp<ResearchServerComponent>(server))
                    continue;

                player.ResearchServer = server;
                break;
            }

            if (player.ResearchServer is null)
            {
                foreach (var server in GetServers(uid))
                {
                    if (!TryComp<FactionComponent>(server, out var serverFaction) ||
                        serverFaction.FactionName != player.FactionName)
                        continue;

                    player.ResearchServer = server;
                    break;
                }
            }
        }

        if (player.ResearchServer is { } serverUid &&
            TryComp<FactionComponent>(serverUid, out var boundServer) &&
            boundServer.FactionName == player.FactionName)
        {
            RaiseLocalEvent(serverUid, ev);
        }

        AddRecipesFromPacks(ev.Recipes, packs);
        return FilterRecipes(ev.Recipes, comp.FactionName);
    }

    private List<ProtoId<ConstructionPrototype>> FilterRecipes(HashSet<ProtoId<ConstructionPrototype>> recipes, string factionName)
    {
        var result = new List<ProtoId<ConstructionPrototype>>();
        foreach (var recipeId in recipes)
        {
            if (!_proto.TryIndex(recipeId, out var recipe))
                continue;

            if (IsRecipeUnique(recipe) && IsAlreadyBuilt(recipe, factionName))
                continue;

            result.Add(recipeId);
        }

        return result;
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
        if (TryComp<FactionComponent>(uid, out var Server)
            && TryComp<FactionComponent>(args.User, out var Player)
            && Server.FactionName == Player.FactionName)
        {
            foreach (var recipe in component.UnlockedCrafts)
            {
                if (_proto.TryIndex<ConstructionPrototype>(recipe, out var comp)
                    && comp.EntitysToShowRecipe.Count > 0
                    && TryComp(args.User, out MetaDataComponent? meta)
                    && meta.EntityPrototype != null)
                {
                    foreach (var entity in comp.EntitysToShowRecipe)
                    {
                        if (entity == meta.EntityPrototype.ID)
                        {
                            args.Recipes.Add(recipe);
                        }
                    }
                }
                else if (_proto.TryIndex<ConstructionPrototype>(recipe, out var comp1)
                    && comp1.EntitysToShowRecipe.Count == 0
                    && !HasComp<WorkbenchComponent>(args.User))
                    args.Recipes.Add(recipe);
            }
        }
    }

    public HashSet<Entity<TechnologyDatabaseComponent>> GetServers(EntityUid client)
    {
        ClientLookup.Clear();

        var clientXform = Transform(client);
        var query = EntityQueryEnumerator<TechnologyDatabaseComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var server, out var xform))
        {
            if (xform.MapUid == clientXform.MapUid)
                ClientLookup.Add((uid, server));
        }

        return ClientLookup;
    }

    private bool IsRecipeUnique(ConstructionPrototype recipe)
    {
        if (!_proto.TryIndex(recipe.Graph, out ConstructionGraphPrototype? graph))
            return false;

        foreach (var node in graph.Nodes.Values)
        {
            foreach (var edge in node.Edges)
            {
                foreach (var action in edge.Completed)
                {
                    if (action is Content.Shared._Nibiru.Construction.Completions.UniqueCraft)
                        return true;
                }
            }
        }
        return false;
    }

    private bool IsAlreadyBuilt(ConstructionPrototype recipe, string factionName)
    {
        if (!_proto.TryIndex(recipe.Graph, out ConstructionGraphPrototype? graph))
            return false;

        if (!graph.Nodes.TryGetValue(recipe.TargetNode, out var targetNode))
            return false;

        var entityId = targetNode.Entity?.GetId(null, null, new(EntityManager));
        if (entityId == null)
            return false;

        var query = EntityQueryEnumerator<FactionComponent, MetaDataComponent>();
        while (query.MoveNext(out _, out var entityFaction, out var meta))
        {
            if (entityFaction.FactionName == factionName && meta.EntityPrototype?.ID == entityId)
                return true;
        }

        return false;
    }
}
