using Content.Shared.Cargo;
using Content.Shared.Construction.Prototypes;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Lathe;
using Content.Shared.NameModifier.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Random.Helpers;
using Content.Shared.Research.Components;
using Content.Shared.Research.Prototypes;
using Content.Shared.Research.Systems;
using Content.Shared.Research.TechnologyDisk.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization;

namespace Content.Shared.Research.TechnologyDisk.Systems;

public sealed partial class TechnologyDiskSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _protoMan = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedResearchSystem _research = default!;
    [Dependency] private SharedLatheSystem _lathe = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private NameModifierSystem _nameModifier = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TechnologyDiskComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<TechnologyDiskComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<TechnologyDiskComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<TechnologyDiskComponent, PriceCalculationEvent>(OnPriceCalculation);
        SubscribeLocalEvent<TechnologyDiskComponent, RefreshNameModifiersEvent>(OnRefreshNameModifiers);
    }

    private void OnMapInit(Entity<TechnologyDiskComponent> ent, ref MapInitEvent args)
    {
        TryPickAndSetRecipe(ent);
        TrySetVisuals(ent);
    }

    /// <summary>
    /// Attempts to pick and set a random recipe/craft as the chosen one.
    /// If the disk already has recipes or crafts, does nothing.
    /// </summary>
    private void TryPickAndSetRecipe(Entity<TechnologyDiskComponent> ent)
    {
        if (ent.Comp.Recipes != null || ent.Comp.Crafts != null)
            return;

        int tier;
        if (ent.Comp.Tier.HasValue)
        {
            tier = ent.Comp.Tier.Value;
        }
        else
        {
            var weightedRandom = _protoMan.Index(ent.Comp.TierWeightPrototype);
            tier = int.Parse(weightedRandom.Pick(_random));
            ent.Comp.Tier = tier;
        }

        var recipeBundles = new HashSet<(ProtoId<LatheRecipePrototype> recipe, ProtoId<TechDisciplinePrototype> discipline)>();
        var craftBundles = new HashSet<(ProtoId<ConstructionPrototype> craft, ProtoId<TechDisciplinePrototype> discipline)>();
        foreach (var tech in _protoMan.EnumeratePrototypes<TechnologyPrototype>())
        {
            if (tech.Tier != tier)
                continue;
            if (ent.Comp.Discipline != null && tech.Discipline != ent.Comp.Discipline.Value)
                continue;

            foreach (var recipe in tech.RecipeUnlocks)
            {
                recipeBundles.Add((recipe, tech.Discipline));
            }

            foreach (var craft in tech.CraftUnlocks)
            {
                craftBundles.Add((craft, tech.Discipline));
            }
        }

        if (craftBundles.Count > 0)
        {
            var bundle = _random.Pick(craftBundles);
            ent.Comp.Discipline = bundle.discipline;
            ent.Comp.Crafts = [bundle.craft];
            Dirty(ent);
            return;
        }

        if (recipeBundles.Count > 0)
        {
            var bundle = _random.Pick(recipeBundles);
            ent.Comp.Discipline = bundle.discipline;
            ent.Comp.Recipes = [bundle.recipe];
            Dirty(ent);
            return;
        }

        Log.Warning($"Failed to pick recipe for a tech disk: no suitable recipes were found (tier={tier}, discipline={ent.Comp.Discipline})");
    }

    /// <summary>
    /// Attempts to set tier and discipline visuals based on chosen tier and discipline.
    /// </summary>
    private void TrySetVisuals(Entity<TechnologyDiskComponent> ent)
    {
        TrySetTierVisuals(ent);
        TrySetDisciplineVisuals(ent);
    }

    /// <summary>
    /// Attempts to set tier visuals based on chosen tier.
    /// </summary>
    private void TrySetTierVisuals(Entity<TechnologyDiskComponent> ent)
    {
        if (ent.Comp.Tier is not { } tier)
            return;

        _appearance.SetData(ent.Owner, TechDiskVisuals.Tier, tier);
    }

    /// <summary>
    /// Attempts to set discipline visuals based on chosen discipline.
    /// </summary>
    private void TrySetDisciplineVisuals(Entity<TechnologyDiskComponent> ent)
    {
        if (!_protoMan.Resolve(ent.Comp.Discipline, out var discipline))
            return;

        _appearance.SetData(ent.Owner, TechDiskVisuals.Discipline, discipline.ID);
    }

    private void OnAfterInteract(Entity<TechnologyDiskComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        if (!HasComp<ResearchServerComponent>(target) || !TryComp<TechnologyDatabaseComponent>(target, out var database))
            return;

        if (ent.Comp.Recipes != null)
        {
            foreach (var recipe in ent.Comp.Recipes)
            {
                _research.AddLatheRecipe(target, recipe, database);
            }
        }

        if (ent.Comp.Crafts != null)
        {
            foreach (var craft in ent.Comp.Crafts)
            {
                _research.AddCraft(target, craft, database);
            }
        }

        _popup.PopupClient(Loc.GetString("tech-disk-inserted"), target, args.User);
        PredictedQueueDel(ent.Owner);
        args.Handled = true;
    }

    private void OnExamine(Entity<TechnologyDiskComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp is { Tier: not null, Discipline: not null }
            && _protoMan.Resolve(ent.Comp.Discipline, out var disciplineProto))
        {
            var desc = Loc.GetString("tech-disk-examine-desc",
                ("tier", ent.Comp.Tier),
                ("branch", Loc.GetString(disciplineProto.Name))
            );

            args.PushMarkup(desc);
        }
        else
        {
            args.PushMarkup(Loc.GetString("tech-disk-examine-desc-unknown"));
        }

        var message = Loc.GetString("tech-disk-examine-none");
        var unlockCount = 0;

        if (ent.Comp.Recipes is { Count: > 0 })
        {
            var prototype = _protoMan.Index(ent.Comp.Recipes[0]);
            message = Loc.GetString("tech-disk-examine", ("result", _lathe.GetRecipeName(prototype)));
            unlockCount += ent.Comp.Recipes.Count;
        }
        else if (ent.Comp.Crafts is { Count: > 0 })
        {
            var prototype = _protoMan.Index(ent.Comp.Crafts[0]);
            message = Loc.GetString("tech-disk-examine", ("result", GetCraftName(prototype)));
            unlockCount += ent.Comp.Crafts.Count;
        }

        if (unlockCount > 1)
            message += " " + Loc.GetString("tech-disk-examine-more");

        args.PushMarkup(message);
    }

    private void OnPriceCalculation(Entity<TechnologyDiskComponent> ent, ref PriceCalculationEvent args)
    {
        if (ent.Comp.Tier is not { } tier)
            return;

        if (!ent.Comp.DiskPricePerTier.TryGetValue(tier, out var price))
            return;

        args.Price = price;
        args.Handled = true;
    }

    private void OnRefreshNameModifiers(Entity<TechnologyDiskComponent> entity, ref RefreshNameModifiersEvent args)
    {
        if (entity.Comp.Recipes != null)
        {
            foreach (var recipe in entity.Comp.Recipes)
            {
                var proto = _protoMan.Index(recipe);
                args.AddModifier("tech-disk-name-format", extraArgs: ("technology", _lathe.GetRecipeName(proto)));
            }
        }

        if (entity.Comp.Crafts != null)
        {
            foreach (var craft in entity.Comp.Crafts)
            {
                var proto = _protoMan.Index(craft);
                args.AddModifier("tech-disk-name-format", extraArgs: ("technology", GetCraftName(proto)));
            }
        }
    }

    private static string GetCraftName(ConstructionPrototype prototype)
    {
        if (prototype.SetName is { } locId)
            return Loc.GetString(locId);

        return prototype.Name ?? prototype.ID;
    }
}

[Serializable, NetSerializable]
public enum TechDiskVisuals : byte
{
    Tier,
    Discipline
}
