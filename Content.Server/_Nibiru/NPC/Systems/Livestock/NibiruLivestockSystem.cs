// Obsolete root using removed
using Content.Shared._Nibiru.NPC.Livestock;
using Content.Shared._Nibiru.NPC.Training;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.GameObjects;

namespace Content.Server._Nibiru.NPC.Systems.Livestock;

/// <summary>
/// Управляет животноводством: рост ресурсов (шерсть, молоко),
/// разведение (скрещивание, беременность, рождение потомства).
/// </summary>
public sealed class NibiruLivestockSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NibiruLivestockComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(EntityUid uid, NibiruLivestockComponent component, MapInitEvent args)
    {
        // Рандомизируем пол при создании
        component.Sex = _random.Prob(0.5f) ? LivestockSex.Male : LivestockSex.Female;
        Dirty(uid, component);
        UpdateAppearance(uid, component);
    }

    private void UpdateAppearance(EntityUid uid, NibiruLivestockComponent component)
    {
        _appearance.SetData(uid, LivestockVisuals.Sex, component.Sex);
        if (!HasComp<NibiruLivestockBabyComponent>(uid))
        {
            _appearance.SetData(uid, LivestockVisuals.BabyStage, -1);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var livestockQuery = EntityQueryEnumerator<NibiruLivestockComponent>();
        var toUpdate = new List<(EntityUid, NibiruLivestockComponent)>();
        while (livestockQuery.MoveNext(out var uid, out var livestock))
        {
            toUpdate.Add((uid, livestock));
        }

        foreach (var (uid, livestock) in toUpdate)
        {
            UpdateResourceGrowth(uid, livestock, frameTime);
            UpdateBreeding(uid, livestock, frameTime);
            UpdateGrowthStage(uid, frameTime, livestock);
        }
    }

    /// <summary>
    /// Обновляет прогресс роста собираемых ресурсов.
    /// </summary>
    private void UpdateResourceGrowth(EntityUid uid, NibiruLivestockComponent livestock, float frameTime)
    {
        if (HasComp<NibiruLivestockBabyComponent>(uid))
            return;

        if (TryComp<Content.Shared.Nutrition.Components.HungerComponent>(uid, out var hunger))
        {
            if (hunger.CurrentThreshold <= Content.Shared.Nutrition.Components.HungerThreshold.Starving)
                return; // Слишком голодны для роста
        }

        bool grew = false;
        foreach (var resource in livestock.HarvestableResources)
        {
            if (resource.ReadyToHarvest)
                continue;

            resource.GrowthAccumulator += frameTime;
            grew = true;
        }

        if (grew && hunger != null)
        {
            // Расход сытости на производство: примерно 1 ед/сек
            IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<Content.Shared.Nutrition.EntitySystems.HungerSystem>().ModifyHunger(uid, -1.0f * frameTime, hunger);
        }
    }

    /// <summary>
    /// Обрабатывает процесс размножения.
    /// </summary>
    private void UpdateBreeding(EntityUid uid, NibiruLivestockComponent livestock, float frameTime)
    {
        if (HasComp<NibiruLivestockBabyComponent>(uid))
            return;

        if (TryComp<Content.Shared.Nutrition.Components.HungerComponent>(uid, out var hunger))
        {
            if (hunger.CurrentThreshold <= Content.Shared.Nutrition.Components.HungerThreshold.Starving)
                return; // Слишком голодны для размножения
        }

        // Кулдаун после рождения
        if (livestock.BreedingCooldownAccumulator > 0)
        {
            livestock.BreedingCooldownAccumulator -= frameTime;
            return;
        }

        // Процесс вынашивания
        if (livestock.IsPregnant)
        {
            livestock.GestationAccumulator += frameTime;

            if (hunger != null)
            {
                // Расход сытости на вынашивание потомства: 2 ед/сек
                IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<Content.Shared.Nutrition.EntitySystems.HungerSystem>().ModifyHunger(uid, -2.0f * frameTime, hunger);
            }

            if (livestock.GestationAccumulator >= livestock.GestationTime)
            {
                GiveBirth(uid, livestock);
            }

            return;
        }

        // Автопоиск партнёра, если самка и готова к размножению
        if (livestock.Sex == LivestockSex.Female && livestock.ReadyToBreed && livestock.CanBreed)
        {
            TryFindMate(uid, livestock);
        }
    }

    /// <summary>
    /// Ищет самца того же вида в радиусе для спаривания.
    /// </summary>
    private void TryFindMate(EntityUid female, NibiruLivestockComponent femaleLivestock)
    {
        var nearbyEntities = new HashSet<EntityUid>();
        _lookup.GetEntitiesInRange(female, 5f, nearbyEntities);

        foreach (var nearby in nearbyEntities)
        {
            if (nearby == female || !TryComp<NibiruLivestockComponent>(nearby, out var maleLivestock))
                continue;

            if (maleLivestock.Sex != LivestockSex.Male || !maleLivestock.CanBreed)
                continue;

            // Проверяем совместимость: одинаковый прототип потомства
            if (femaleLivestock.OffspringPrototype != maleLivestock.OffspringPrototype)
                continue;

            // Начинаем вынашивание
            femaleLivestock.IsPregnant = true;
            femaleLivestock.GestationAccumulator = 0f;
            return;
        }
    }

    /// <summary>
    /// Рождение потомства.
    /// </summary>
    private void GiveBirth(EntityUid mother, NibiruLivestockComponent livestock)
    {
        livestock.IsPregnant = false;
        livestock.GestationAccumulator = 0f;
        livestock.BreedingCooldownAccumulator = livestock.BreedingCooldown;

        if (string.IsNullOrEmpty(livestock.OffspringPrototype))
            return;

        var xform = Transform(mother);
        var count = _random.Next(livestock.OffspringCount, livestock.MaxOffspringCount + 1);

        for (var i = 0; i < count; i++)
        {
            var offset = _random.NextVector2(1f);
            var spawnCoords = xform.Coordinates.Offset(offset);
            var offspring = Spawn(livestock.OffspringPrototype, spawnCoords);

            // Потомство приручённого животного наследует хозяина
            if (TryComp<NibiruTamableComponent>(mother, out var tamable) && tamable.IsTamed)
            {
                if (TryComp<NibiruTamableComponent>(offspring, out var offspringTamable))
                {
                    offspringTamable.IsTamed = true;
                    offspringTamable.OwnerUid = tamable.OwnerUid;
                    offspringTamable.TrustLevel = offspringTamable.TrustThreshold;
                }
            }
        }
    }

    /// <summary>
    /// Собирает готовый ресурс с животного.
    /// </summary>
    public bool TryHarvestResource(EntityUid animal, EntityUid harvester, int resourceIndex)
    {
        if (!TryComp<NibiruLivestockComponent>(animal, out var livestock))
            return false;

        if (resourceIndex < 0 || resourceIndex >= livestock.HarvestableResources.Count)
            return false;

        var resource = livestock.HarvestableResources[resourceIndex];
        if (!resource.ReadyToHarvest)
            return false;

        // Проверяем инструмент, если нужен
        // (полная проверка инструмента реализуется позже)

        var xform = Transform(animal);
        for (var i = 0; i < resource.Yield; i++)
        {
            Spawn(resource.ItemPrototype, xform.Coordinates);
        }

        resource.GrowthAccumulator = 0f;
        return true;
    }

    private void UpdateGrowthStage(EntityUid animal, float frameTime, NibiruLivestockComponent? livestock = null, NibiruLivestockBabyComponent? baby = null)
    {
        if (!Resolve(animal, ref livestock))
            return;

        if (!Resolve(animal, ref baby, false))
            return;

        float growthModifier = 1.0f;
        if (TryComp<Content.Shared.Nutrition.Components.HungerComponent>(animal, out var hunger))
        {
            if (hunger.CurrentThreshold <= Content.Shared.Nutrition.Components.HungerThreshold.Starving)
                growthModifier = 0.0f; // Не растёт при сильном голоде

            // Расход сытости на рост (уменьшенный по сравнению со взрослыми: например 0.5 ед/сек)
            IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<Content.Shared.Nutrition.EntitySystems.HungerSystem>().ModifyHunger(animal, -0.5f * frameTime, hunger);
        }

        baby.GrowthAccumulator += frameTime * growthModifier;

        if (baby.GrowthAccumulator >= baby.StageGrowthTime)
        {
            baby.GrowthAccumulator = 0f;
            baby.GrowthStage++;
            Dirty(animal, baby);

            _appearance.SetData(animal, LivestockVisuals.BabyStage, baby.GrowthStage);
        }

        if (baby.GrowthStage >= baby.Stages.Count)
        {
            RemComp<NibiruLivestockBabyComponent>(animal);
            UpdateAppearance(animal, livestock);
        }
    }
}
