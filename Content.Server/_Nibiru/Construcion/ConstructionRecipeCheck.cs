using Content.Server.Construction.Components;
using Content.Server.Stack;
using Content.Shared.Construction;
using Content.Shared.Construction.Prototypes;
using Content.Shared.DoAfter;
using Robust.Shared.Prototypes;
using Robust.Server.GameObjects;
using Content.Shared.Research.Components;
using Content.Shared._Nibiru.Factions;
using Content.Shared.Construction.Messages;
using Content.Shared.Construction;
using Content.Shared.UserInterface;
using Content.Shared.IdentityManagement;
using JetBrains.Annotations;
using Robust.Server.Containers;
using SharedToolSystem = Content.Shared.Tools.Systems.SharedToolSystem;
using System.Linq;
﻿using Content.Server.Mind;
using Robust.Shared.Player;
using Robust.Shared.Map;

namespace Content.Server._Nibiru.Construction;

/// <summary>
/// The server-side implementation of the construction system, which is used for constructing entities in game.
/// </summary>
[UsedImplicitly]
public sealed partial class ConstructionRecipeCheck : SharedConstructionSystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly MindSystem _minds = default!;
    [Dependency] private readonly ISharedPlayerManager _player = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;

    private static readonly HashSet<Entity<TechnologyDatabaseComponent>> ClientLookup = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<ConstructionUIOpen>(OnRequestCraftsInfoEvent);
        SubscribeLocalEvent<TechnologyDatabaseComponent, CraftsGetRecipesEvent>(OnGetRecipes);
    }

    private void OnRequestCraftsInfoEvent(ConstructionUIOpen msg, EntitySessionEventArgs args)
    {
        if (!args.SenderSession.AttachedEntity.HasValue || args.SenderSession.AttachedEntity != GetEntity(msg.NetEntity))
            return;

        var entity = args.SenderSession.AttachedEntity.Value;
        if (!EntityManager.TryGetComponent<FactionComponent>(entity, out var comp))
            return;

        List<ProtoId<ConstructionPrototype>> crafts = GetAvailableRecipes(entity, comp);

        //crafts.Add(new("MimeHardsuit"));

        RaiseNetworkEvent(new ConstructionCrafts(GetNetEntity(entity), crafts), args.SenderSession);
    }

    public List<ProtoId<ConstructionPrototype>> GetAvailableRecipes(EntityUid uid, FactionComponent comp, bool getUnavailable = false)
    {
        var ev = new CraftsGetRecipesEvent((uid, comp), getUnavailable);
        AddRecipesFromPacks(ev.Recipes, comp.StaticPacks);

        var allServers = GetServers(uid).ToList();

        if (EntityManager.TryGetComponent<FactionComponent>(uid, out var Player)
        && Player.ResearchServer is null)
        {
            foreach (var server in allServers)
            {
                if (EntityManager.TryGetComponent<FactionComponent>(server, out var Serv)
                && Serv.FactionName == Player.FactionName)
                {
                    Player.ResearchServer = server;
                    RaiseLocalEvent(server, ev);
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

        return ev.Recipes.ToList();
    }

    public void AddRecipesFromPacks(HashSet<ProtoId<ConstructionPrototype>> recipes, IEnumerable<ProtoId<ConstructionPackPrototype>> packs)
    {
        foreach (var id in packs)
        {
            var pack = _proto.Index(id);
            recipes.UnionWith(pack.Recipes);
        }
    }

    public void OnGetRecipes(EntityUid uid, TechnologyDatabaseComponent component, CraftsGetRecipesEvent args)
    {
        //if (uid == args.Const)
        //return;

        if (EntityManager.TryGetComponent<FactionComponent>(uid, out var Server)
            && EntityManager.TryGetComponent<FactionComponent>(args.User, out var Player)
            && Server.FactionName == Player.FactionName)
        {/*
				foreach (var id in args.Comp.StaticPacks)
				{
					var pack = _proto.Index(id);
					foreach (var recipe in pack.Recipes)
					{
						if (args.GetUnavailable || component.UnlockedCrafts.Contains(recipe))
						{
							args.Recipes.Add(recipe);
							args.Recipes.Add(new("TileWeb"));
						}
					}
				}*/

            foreach (var recipe in component.UnlockedCrafts)
            {
                args.Recipes.Add(recipe);
                //args.Recipes.Add(new("TileWeb"));
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
