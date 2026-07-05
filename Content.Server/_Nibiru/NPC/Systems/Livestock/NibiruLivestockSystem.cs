using Content.Shared._Nibiru.NPC.Livestock;
using Content.Shared._Nibiru.NPC.Training;
using Content.Shared.Damage;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Sprite;
using Content.Shared.Weapons.Melee;
using Content.Shared.Movement.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using System.Numerics;

namespace Content.Server._Nibiru.NPC.Systems.Livestock;

public sealed class NibiruLivestockSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly HungerSystem _hunger = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedScaleVisualsSystem _scale = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NibiruLivestockComponent, MapInitEvent>(OnLegacyLivestockMapInit);
        SubscribeLocalEvent<NibiruAnimalSexComponent, MapInitEvent>(OnSexMapInit);
        SubscribeLocalEvent<NibiruAnimalGrowthComponent, MapInitEvent>(OnGrowthMapInit);
        SubscribeLocalEvent<NibiruAnimalGrowthComponent, RefreshMovementSpeedModifiersEvent>(OnGrowthRefreshSpeed);
    }

    private void OnLegacyLivestockMapInit(EntityUid uid, NibiruLivestockComponent component, MapInitEvent args)
    {
        if (!HasComp<NibiruAnimalSexComponent>(uid))
        {
            component.Sex = _random.Prob(0.5f) ? LivestockSex.Male : LivestockSex.Female;
            Dirty(uid, component);
        }

        UpdateSexAppearance(uid, component.Sex);
    }

    private void OnSexMapInit(EntityUid uid, NibiruAnimalSexComponent component, MapInitEvent args)
    {
        if (component.RandomizeOnMapInit)
        {
            component.Sex = _random.Prob(0.5f) ? LivestockSex.Male : LivestockSex.Female;
            Dirty(uid, component);
        }

        UpdateSexAppearance(uid, component.Sex);
    }

    private void OnGrowthMapInit(EntityUid uid, NibiruAnimalGrowthComponent component, MapInitEvent args)
    {
        UpdateGrowthVisuals(uid, component);
        _movementSpeed.RefreshMovementSpeedModifiers(uid);
    }

    private void OnGrowthRefreshSpeed(EntityUid uid, NibiruAnimalGrowthComponent component, RefreshMovementSpeedModifiersEvent args)
    {
        var modifier = MathF.Max(component.CurrentModifier, 0.05f);
        args.ModifySpeed(modifier, modifier);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        UpdateProducts(frameTime);
        UpdateBreeding(frameTime);
        UpdatePregnancies(frameTime);
        UpdateGrowth(frameTime);
    }

    private void UpdateProducts(float frameTime)
    {
        var query = EntityQueryEnumerator<NibiruAnimalProductsComponent>();
        while (query.MoveNext(out var uid, out var products))
        {
            GrowResources(uid, products.HarvestableResources, frameTime);
        }

        var legacyQuery = EntityQueryEnumerator<NibiruLivestockComponent>();
        while (legacyQuery.MoveNext(out var uid, out var livestock))
        {
            if (HasComp<NibiruAnimalProductsComponent>(uid))
                continue;

            GrowResources(uid, livestock.HarvestableResources, frameTime);
        }
    }

    private void GrowResources(EntityUid uid, List<LivestockResource> resources, float frameTime)
    {
        if (HasComp<NibiruAnimalGrowthComponent>(uid) || resources.Count == 0 || IsStarving(uid))
            return;

        var grew = false;
        foreach (var resource in resources)
        {
            if (resource.ReadyToHarvest)
                continue;

            resource.GrowthAccumulator += frameTime;
            grew = true;
        }

        if (grew && TryComp<HungerComponent>(uid, out var hunger))
            _hunger.ModifyHunger(uid, -1.0f * frameTime, hunger);
    }

    private void UpdateBreeding(float frameTime)
    {
        var query = EntityQueryEnumerator<NibiruAnimalBreederComponent>();
        while (query.MoveNext(out var uid, out var breeder))
        {
            if (!breeder.Enabled || HasComp<NibiruAnimalGrowthComponent>(uid) || HasComp<NibiruAnimalPregnancyComponent>(uid) || IsStarving(uid))
                continue;

            if (breeder.BreedingCooldownAccumulator > 0f)
            {
                breeder.BreedingCooldownAccumulator = MathF.Max(0f, breeder.BreedingCooldownAccumulator - frameTime);
                Dirty(uid, breeder);
                continue;
            }

            if (GetSex(uid) == LivestockSex.Female)
                TryFindMate(uid, breeder);
        }

        var legacyQuery = EntityQueryEnumerator<NibiruLivestockComponent>();
        while (legacyQuery.MoveNext(out var uid, out var livestock))
        {
            if (HasComp<NibiruAnimalBreederComponent>(uid) || !livestock.CanBreed || HasComp<NibiruAnimalGrowthComponent>(uid) || IsStarving(uid))
                continue;

            UpdateLegacyBreeding(uid, livestock, frameTime);
        }
    }

    private void UpdateLegacyBreeding(EntityUid uid, NibiruLivestockComponent livestock, float frameTime)
    {
        if (livestock.BreedingCooldownAccumulator > 0f)
        {
            livestock.BreedingCooldownAccumulator = MathF.Max(0f, livestock.BreedingCooldownAccumulator - frameTime);
            return;
        }

        if (livestock.IsPregnant)
        {
            livestock.GestationAccumulator += frameTime;
            if (TryComp<HungerComponent>(uid, out var hunger))
                _hunger.ModifyHunger(uid, -2.0f * frameTime, hunger);

            if (livestock.GestationAccumulator >= livestock.GestationTime)
                GiveBirth(uid, livestock);

            return;
        }

        if (livestock.Sex == LivestockSex.Female && livestock.ReadyToBreed)
            TryFindLegacyMate(uid, livestock);
    }

    private void TryFindMate(EntityUid female, NibiruAnimalBreederComponent breeder)
    {
        var nearbyEntities = new HashSet<EntityUid>();
        _lookup.GetEntitiesInRange(female, breeder.MateSearchRadius, nearbyEntities);

        foreach (var nearby in nearbyEntities)
        {
            if (nearby == female || HasComp<NibiruAnimalGrowthComponent>(nearby) || GetSex(nearby) != LivestockSex.Male)
                continue;

            if (!TryComp<NibiruAnimalBreederComponent>(nearby, out var mate) || !mate.Enabled)
                continue;

            if (!IsCompatibleMate(female, nearby, breeder, mate))
                continue;

            var pregnancy = EnsureComp<NibiruAnimalPregnancyComponent>(female);
            pregnancy.OffspringPrototype = breeder.OffspringPrototype;
            pregnancy.GestationTime = breeder.GestationTime;
            pregnancy.GestationAccumulator = 0f;
            pregnancy.MinOffspringCount = breeder.MinOffspringCount;
            pregnancy.MaxOffspringCount = Math.Max(breeder.MinOffspringCount, breeder.MaxOffspringCount);
            pregnancy.Growth = breeder.Growth;
            Dirty(female, pregnancy);
            UpdatePregnancyAppearance(female, true);
            return;
        }
    }

    private void TryFindLegacyMate(EntityUid female, NibiruLivestockComponent femaleLivestock)
    {
        var nearbyEntities = new HashSet<EntityUid>();
        _lookup.GetEntitiesInRange(female, 5f, nearbyEntities);

        foreach (var nearby in nearbyEntities)
        {
            if (nearby == female || !TryComp<NibiruLivestockComponent>(nearby, out var maleLivestock))
                continue;

            if (maleLivestock.Sex != LivestockSex.Male || !maleLivestock.CanBreed || HasComp<NibiruAnimalGrowthComponent>(nearby))
                continue;

            if (femaleLivestock.OffspringPrototype != maleLivestock.OffspringPrototype)
                continue;

            femaleLivestock.IsPregnant = true;
            femaleLivestock.GestationAccumulator = 0f;
            Dirty(female, femaleLivestock);
            UpdatePregnancyAppearance(female, true);
            return;
        }
    }

    private bool IsCompatibleMate(EntityUid first, EntityUid second, NibiruAnimalBreederComponent firstBreeder, NibiruAnimalBreederComponent secondBreeder)
    {
        if (!string.IsNullOrEmpty(firstBreeder.SpeciesId) || !string.IsNullOrEmpty(secondBreeder.SpeciesId))
            return firstBreeder.SpeciesId == secondBreeder.SpeciesId;

        if (!string.IsNullOrEmpty(firstBreeder.OffspringPrototype) || !string.IsNullOrEmpty(secondBreeder.OffspringPrototype))
            return firstBreeder.OffspringPrototype == secondBreeder.OffspringPrototype;

        return Prototype(first) == Prototype(second);
    }

    private void UpdatePregnancies(float frameTime)
    {
        var query = EntityQueryEnumerator<NibiruAnimalPregnancyComponent>();
        while (query.MoveNext(out var uid, out var pregnancy))
        {
            if (IsStarving(uid))
                continue;

            pregnancy.GestationAccumulator += frameTime;
            if (TryComp<HungerComponent>(uid, out var hunger))
                _hunger.ModifyHunger(uid, -2.0f * frameTime, hunger);

            Dirty(uid, pregnancy);

            if (pregnancy.GestationAccumulator >= pregnancy.GestationTime)
                GiveBirth(uid, pregnancy);
        }
    }

    private void GiveBirth(EntityUid mother, NibiruAnimalPregnancyComponent pregnancy)
    {
        var breeder = CompOrNull<NibiruAnimalBreederComponent>(mother);
        if (breeder != null)
        {
            breeder.BreedingCooldownAccumulator = breeder.BreedingCooldown;
            Dirty(mother, breeder);
        }

        SpawnOffspring(mother, pregnancy.OffspringPrototype, pregnancy.MinOffspringCount, pregnancy.MaxOffspringCount, pregnancy.Growth);
        RemComp<NibiruAnimalPregnancyComponent>(mother);
        UpdatePregnancyAppearance(mother, false);
    }

    private void GiveBirth(EntityUid mother, NibiruLivestockComponent livestock)
    {
        livestock.IsPregnant = false;
        livestock.GestationAccumulator = 0f;
        livestock.BreedingCooldownAccumulator = livestock.BreedingCooldown;
        Dirty(mother, livestock);

        var growth = new NibiruAnimalGrowthSettings();
        SpawnOffspring(mother, livestock.OffspringPrototype, livestock.OffspringCount, livestock.MaxOffspringCount, growth);
        UpdatePregnancyAppearance(mother, false);
    }

    private void SpawnOffspring(EntityUid mother, string? offspringPrototype, int minCount, int maxCount, NibiruAnimalGrowthSettings growth)
    {
        var prototype = string.IsNullOrEmpty(offspringPrototype) ? Prototype(mother) : offspringPrototype;
        if (string.IsNullOrEmpty(prototype))
            return;

        var xform = Transform(mother);
        var count = _random.Next(minCount, Math.Max(minCount, maxCount) + 1);

        for (var i = 0; i < count; i++)
        {
            var offspring = Spawn(prototype, xform.Coordinates.Offset(_random.NextVector2(1f)));
            if (!string.IsNullOrEmpty(offspringPrototype) && string.IsNullOrEmpty(growth.AdultPrototype))
                growth.AdultPrototype = Prototype(mother);

            SetupOffspring(mother, offspring, growth);
        }
    }

    private void SetupOffspring(EntityUid mother, EntityUid offspring, NibiruAnimalGrowthSettings growth)
    {
        if (growth.AddGrowthComponent)
        {
            var childGrowth = EnsureComp<NibiruAnimalGrowthComponent>(offspring);
            childGrowth.GrowTime = growth.GrowTime;
            childGrowth.StartScale = growth.StartScale;
            childGrowth.AdultScale = growth.AdultScale;
            childGrowth.AdultPrototype = growth.AdultPrototype;
            childGrowth.ModifierSteps = new List<NibiruAnimalGrowthModifierStep>(growth.ModifierSteps);
            childGrowth.Age = 0f;
            childGrowth.CurrentModifier = GetGrowthModifier(childGrowth, 0f);
            Dirty(offspring, childGrowth);
            UpdateGrowthVisuals(offspring, childGrowth);
            _movementSpeed.RefreshMovementSpeedModifiers(offspring);
            ApplyGrowthDamageModifier(offspring, childGrowth.CurrentModifier);
        }

        CopyTameState(mother, offspring);
    }

    private void CopyTameState(EntityUid source, EntityUid target)
    {
        if (!TryComp<NibiruTamableComponent>(source, out var tamable) || !tamable.IsTamed ||
            !TryComp<NibiruTamableComponent>(target, out var targetTamable))
            return;

        targetTamable.IsTamed = true;
        targetTamable.OwnerUid = tamable.OwnerUid;
        targetTamable.TrustLevel = targetTamable.TrustThreshold;
    }

    private void UpdateGrowth(float frameTime)
    {
        var query = EntityQueryEnumerator<NibiruAnimalGrowthComponent>();
        while (query.MoveNext(out var uid, out var growth))
        {
            if (IsStarving(uid))
                continue;

            growth.Age = MathF.Min(growth.GrowTime, growth.Age + frameTime);
            var progress = GetGrowthProgress(growth);
            var modifier = GetGrowthModifier(growth, progress);
            var changedModifier = !MathHelper.CloseToPercent(growth.CurrentModifier, modifier, 0.001f);
            growth.CurrentModifier = modifier;
            Dirty(uid, growth);
            UpdateGrowthVisuals(uid, growth);

            if (changedModifier)
            {
                _movementSpeed.RefreshMovementSpeedModifiers(uid);
                ApplyGrowthDamageModifier(uid, modifier);
            }

            if (progress >= 1f)
            {
                FinishGrowth(uid, growth);
            }
        }
    }

    private void FinishGrowth(EntityUid uid, NibiruAnimalGrowthComponent growth)
    {
        if (!string.IsNullOrEmpty(growth.AdultPrototype) && growth.AdultPrototype != Prototype(uid))
        {
            var adult = Spawn(growth.AdultPrototype, Transform(uid).Coordinates);
            CopyTameState(uid, adult);
            QueueDel(uid);
            return;
        }

        _scale.SetSpriteScale(uid, new Vector2(growth.AdultScale, growth.AdultScale));
        RemComp<NibiruAnimalGrowthComponent>(uid);
        _movementSpeed.RefreshMovementSpeedModifiers(uid);
    }

    private float GetGrowthProgress(NibiruAnimalGrowthComponent growth)
    {
        if (growth.GrowTime <= 0f)
            return 1f;

        return Math.Clamp(growth.Age / growth.GrowTime, 0f, 1f);
    }

    private float GetGrowthModifier(NibiruAnimalGrowthComponent growth, float progress)
    {
        var modifier = 0.1f;
        foreach (var step in growth.ModifierSteps)
        {
            if (progress >= step.Progress)
                modifier = step.Modifier;
        }

        return progress >= 1f ? 1f : modifier;
    }

    private void UpdateGrowthVisuals(EntityUid uid, NibiruAnimalGrowthComponent growth)
    {
        var progress = GetGrowthProgress(growth);
        var scale = MathHelper.Lerp(growth.StartScale, growth.AdultScale, progress);
        _scale.SetSpriteScale(uid, new Vector2(scale, scale));
        _appearance.SetData(uid, NibiruAnimalReproductionVisuals.GrowthProgress, progress);
        _appearance.SetData(uid, NibiruAnimalReproductionVisuals.GrowthModifier, growth.CurrentModifier);
    }

    private void ApplyGrowthDamageModifier(EntityUid uid, float modifier)
    {
        if (!TryComp<MeleeWeaponComponent>(uid, out var melee) || melee.Damage == null)
            return;

        if (!TryComp<NibiruAnimalGrowthDamageMemoryComponent>(uid, out var memory))
        {
            memory = AddComp<NibiruAnimalGrowthDamageMemoryComponent>(uid);
            memory.AdultDamage = melee.Damage;
        }

        melee.Damage = memory.AdultDamage * modifier;
        Dirty(uid, melee);
    }

    public bool TryHarvestResource(EntityUid animal, EntityUid harvester, int resourceIndex)
    {
        var resources = GetResources(animal);
        if (resources == null || resourceIndex < 0 || resourceIndex >= resources.Count)
            return false;

        var resource = resources[resourceIndex];
        if (!resource.ReadyToHarvest)
            return false;

        var xform = Transform(animal);
        for (var i = 0; i < resource.Yield; i++)
            Spawn(resource.ItemPrototype, xform.Coordinates);

        resource.GrowthAccumulator = 0f;
        return true;
    }

    public List<LivestockResource>? GetResources(EntityUid animal)
    {
        if (TryComp<NibiruAnimalProductsComponent>(animal, out var products))
            return products.HarvestableResources;

        return TryComp<NibiruLivestockComponent>(animal, out var livestock) ? livestock.HarvestableResources : null;
    }

    public LivestockSex GetSex(EntityUid uid)
    {
        if (TryComp<NibiruAnimalSexComponent>(uid, out var sex))
            return sex.Sex;

        return TryComp<NibiruLivestockComponent>(uid, out var livestock) ? livestock.Sex : LivestockSex.Female;
    }

    private bool IsStarving(EntityUid uid)
    {
        return TryComp<HungerComponent>(uid, out var hunger) && hunger.CurrentThreshold <= HungerThreshold.Starving;
    }

    private string? Prototype(EntityUid uid)
    {
        return MetaData(uid).EntityPrototype?.ID;
    }

    private void UpdateSexAppearance(EntityUid uid, LivestockSex sex)
    {
        _appearance.SetData(uid, LivestockVisuals.Sex, sex);
        _appearance.SetData(uid, NibiruAnimalReproductionVisuals.Sex, sex);
    }

    private void UpdatePregnancyAppearance(EntityUid uid, bool isPregnant)
    {
        _appearance.SetData(uid, NibiruAnimalReproductionVisuals.IsPregnant, isPregnant);
    }
}

[RegisterComponent]
public sealed partial class NibiruAnimalGrowthDamageMemoryComponent : Component
{
    public DamageSpecifier AdultDamage = new();
}
