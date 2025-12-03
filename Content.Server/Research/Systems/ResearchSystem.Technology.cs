using Content.Shared.Database;
using Content.Shared.Research.Components;
using Content.Shared.Research.Prototypes;
using Content.Shared.Construction.Prototypes;
using JetBrains.Annotations;
using Content.Shared._Nibiru.Research;
using Content.Shared.Inventory;
using Content.Shared.Storage;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Coordinates;
using Content.Shared.Whitelist;
using Content.Shared.ActionBlocker;
using Robust.Server.Containers;
using Robust.Shared.Containers;
using Content.Shared.Stacks;
using Content.Server.Stack;
using Content.Shared.Storage;
using Robust.Shared.Random;

namespace Content.Server.Research.Systems;

public sealed partial class ResearchSystem
{
    [Dependency] private readonly InventorySystem _inventorySystem = default!;
    [Dependency] private readonly SharedInteractionSystem _interactionSystem = default!;
    [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private readonly SharedHandsSystem _handsSystem = default!;
    //[Dependency] private readonly EntityLookupSystem _lookupSystem = default!;
    [Dependency] private readonly SharedTransformSystem _transformSystem = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelistSystem = default!;
    [Dependency] private readonly ContainerSystem _container = default!;
    [Dependency] private readonly StackSystem _stackSystem = default!;
    [Dependency] private readonly IRobustRandom _robustRandom = default!;

    /// <summary>
    /// Syncs the primary entity's database to that of the secondary entity's database.
    /// </summary>
    public void Sync(EntityUid primaryUid, EntityUid otherUid, TechnologyDatabaseComponent? primaryDb = null, TechnologyDatabaseComponent? otherDb = null)
    {
        if (!Resolve(primaryUid, ref primaryDb) || !Resolve(otherUid, ref otherDb))
            return;

        primaryDb.MainDiscipline = otherDb.MainDiscipline;
        primaryDb.CurrentTechnologyCards = otherDb.CurrentTechnologyCards;
        primaryDb.SupportedDisciplines = otherDb.SupportedDisciplines;
        primaryDb.UnlockedTechnologies = otherDb.UnlockedTechnologies;
        primaryDb.UnlockedRecipes = otherDb.UnlockedRecipes;
        primaryDb.UnlockedCrafts = otherDb.UnlockedCrafts;

        Dirty(primaryUid, primaryDb);

        var ev = new TechnologyDatabaseSynchronizedEvent();
        RaiseLocalEvent(primaryUid, ref ev);
    }

    /// <summary>
    ///     If there's a research client component attached to the owner entity,
    ///     and the research client is connected to a research server, this method
    ///     syncs against the research server, and the server against the local database.
    /// </summary>
    /// <returns>Whether it could sync or not</returns>
    public void SyncClientWithServer(EntityUid uid, TechnologyDatabaseComponent? databaseComponent = null, ResearchClientComponent? clientComponent = null)
    {
        if (!Resolve(uid, ref databaseComponent, ref clientComponent, false))
            return;

        if (!TryComp<TechnologyDatabaseComponent>(clientComponent.Server, out var serverDatabase))
            return;

        Sync(uid, clientComponent.Server.Value, databaseComponent, serverDatabase);
    }

    /// <summary>
    /// Tries to add a technology to a database, checking if it is able to
    /// </summary>
    /// <returns>If the technology was successfully added</returns>
    public bool UnlockTechnology(EntityUid client,
        string prototypeid,
        EntityUid user,
        ResearchClientComponent? component = null,
        TechnologyDatabaseComponent? clientDatabase = null)
    {
        if (!PrototypeManager.TryIndex<TechnologyPrototype>(prototypeid, out var prototype))
            return false;

        return UnlockTechnology(client, prototype, user, component, clientDatabase);
    }

    /// <summary>
    /// Tries to add a technology to a database, checking if it is able to
    /// </summary>
    /// <returns>If the technology was successfully added</returns>
    public bool UnlockTechnology(EntityUid client,
        TechnologyPrototype prototype,
        EntityUid user,
        ResearchClientComponent? component = null,
        TechnologyDatabaseComponent? clientDatabase = null)
    {
        if (!Resolve(client, ref component, ref clientDatabase, false))
            return false;

        if (!TryGetClientServer(client, out var serverEnt, out _, component))
            return false;

        if (!CanServerUnlockTechnology(client, prototype, user, clientDatabase, component))
            return false;

        AddTechnology(serverEnt.Value, prototype);
        //TrySetMainDiscipline(prototype, serverEnt.Value); // Goobstation commented
        ModifyServerPoints(serverEnt.Value, -prototype.Cost);
        UpdateTechnologyCards(serverEnt.Value);

        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(user):player} unlocked {prototype.ID} (discipline: {prototype.Discipline}, tier: {prototype.Tier}) at {ToPrettyString(client)}, for server {ToPrettyString(serverEnt.Value)}.");
        return true;
    }

    /// <summary>
    ///     Adds a technology to the database without checking if it could be unlocked.
    /// </summary>
    [PublicAPI]
    public void AddTechnology(EntityUid uid, string technology, TechnologyDatabaseComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        if (!PrototypeManager.TryIndex<TechnologyPrototype>(technology, out var prototype))
        {
            //if (PrototypeManager.TryIndex<ConstructionPrototype>(technology, out var prototype))
            //	AddTechnology(uid, prototype, component);
            return;
        }
        AddTechnology(uid, prototype, component);
    }

    /// <summary>
    ///     Adds a technology to the database without checking if it could be unlocked.
    /// </summary>
    public void AddTechnology(EntityUid uid, TechnologyPrototype technology, TechnologyDatabaseComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        //todo this needs to support some other stuff, too
        foreach (var generic in technology.GenericUnlocks)
        {
            if (generic.PurchaseEvent != null)
                RaiseLocalEvent(generic.PurchaseEvent);
        }

        component.UnlockedTechnologies.Add(technology.ID);
        var addedRecipes = new List<string>();

        foreach (var unlock in technology.RecipeUnlocks)
        {
            if (component.UnlockedRecipes.Contains(unlock))
                continue;
            component.UnlockedRecipes.Add(unlock);
            addedRecipes.Add(unlock);
        }

        foreach (var CraftUnlock in technology.CraftUnlocks)
        {
            if (component.UnlockedCrafts.Contains(CraftUnlock))
                continue;
            component.UnlockedCrafts.Add(CraftUnlock);
            addedRecipes.Add(CraftUnlock);
        }
        Dirty(uid, component);

        CheckEpochUnlock(uid, technology, component); //Nibiru

        var ev = new TechnologyDatabaseModifiedEvent(addedRecipes);
        RaiseLocalEvent(uid, ref ev);
    }

    /// <summary>
    ///     Returns whether a technology can be unlocked on this database,
    ///     taking parent technologies into account.
    /// </summary>
    /// <returns>Whether it could be unlocked or not</returns>
    public bool CanServerUnlockTechnology(EntityUid uid,
        TechnologyPrototype technology,
        EntityUid user,
        TechnologyDatabaseComponent? database = null,
        ResearchClientComponent? client = null)
    {

        if (!Resolve(uid, ref client, ref database, false))
            return false;

        if (!TryGetClientServer(uid, out _, out var serverComp, client))
            return false;

        if (!IsTechnologyAvailable(database, technology))
            return false;

        if (technology.Cost >= serverComp.Points)
            return false;

        if (!TechEntityRecipe(user, technology) && (technology.MaterialToUnlock.Count > 0 || technology.EntityToUnlock.Count > 0)) //Nibiru
            return false;

        return true;
    }

    //Nibiru start
    private bool TechEntityRecipe(EntityUid user, TechnologyPrototype technology)
    {
        //var container = _container.EnsureContainer<Container>(user, "item_construction", out var existed);
        //var containers = new Dictionary<string, Container>();
        var used = new HashSet<EntityUid>();

        /*Container GetContainer(string name)
            {
                if (containers.TryGetValue(name, out var container1))
                    return container1;

                while (true)
                {
                    var random = _robustRandom.Next();
                    var c = _container.EnsureContainer<Container>(user, random.ToString(), out var exists);

                    if (exists)
                        continue;

                    containers[name] = c;
                    return c;
                }
            }*/

        if (technology.EntityToUnlock is not null)
        {
            foreach (var recipe in technology.EntityToUnlock)
            {
                foreach (var entity in new HashSet<EntityUid>(EnumerateNearby(user)))
                {
                    if (!recipe.EntityValid(entity, out var ent))
                        continue;

                    if (used.Contains(entity))
                        continue;

                    return true;
                }
            }
        }

        if (technology.EntityToUnlock is not null)
        {
            foreach (var recipe in technology.MaterialToUnlock)
            {
                foreach (var material in new HashSet<EntityUid>(EnumerateNearby(user)))
                {
                    if (!recipe.EntityValid(material, out var stack))
                        continue;

                    if (used.Contains(material))
                        continue;

                    var splitStack = _stackSystem.Split((material, stack), recipe.Amount, user.ToCoordinates(0, 0));

                    if (splitStack == null)
                        continue;

                    //if (string.IsNullOrEmpty(recipe.Store))
                    //{
                    //	if (!_container.Insert(splitStack.Value, container))
                    //		continue;
                    //}
                    //else if (!_container.Insert(splitStack.Value, GetContainer(recipe.Store)))
                    //    continue;

                    return true;
                }
            }
        }

        return false;
    }

    private IEnumerable<EntityUid> EnumerateNearby(EntityUid user)
    {
        foreach (var item in _handsSystem.EnumerateHeld(user))
        {
            if (TryComp(item, out StorageComponent? storage))
            {
                foreach (var storedEntity in storage.Container.ContainedEntities!)
                {
                    yield return storedEntity;
                }
            }

            yield return item;
        }

        if (_inventorySystem.TryGetContainerSlotEnumerator(user, out var containerSlotEnumerator))
        {
            while (containerSlotEnumerator.MoveNext(out var containerSlot))
            {
                if (!containerSlot.ContainedEntity.HasValue)
                    continue;

                if (TryComp(containerSlot.ContainedEntity.Value, out StorageComponent? storage))
                {
                    foreach (var storedEntity in storage.Container.ContainedEntities)
                    {
                        yield return storedEntity;
                    }
                }

                yield return containerSlot.ContainedEntity.Value;
            }
        }

        var pos = _transformSystem.GetMapCoordinates(user);

        foreach (var near in _lookup.GetEntitiesInRange(pos, 2f, LookupFlags.Contained | LookupFlags.Dynamic | LookupFlags.Sundries | LookupFlags.Approximate))
        {
            if (near == user)
                continue;
            if (_interactionSystem.InRangeUnobstructed(pos, near, 2f) && _container.IsInSameOrParentContainer(user, near))
                yield return near;
        }
    }
    //Nibiru end

    private void OnDatabaseRegistrationChanged(EntityUid uid, TechnologyDatabaseComponent component, ref ResearchRegistrationChangedEvent args)
    {
        if (args.Server != null)
            return;
        component.MainDiscipline = null;
        component.CurrentTechnologyCards = new List<string>();
        component.SupportedDisciplines = new List<string>();
        component.UnlockedTechnologies = new List<string>();
        component.UnlockedRecipes = new List<string>();
        component.UnlockedCrafts = new List<string>();
        Dirty(uid, component);
    }
}
