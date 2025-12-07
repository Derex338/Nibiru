using Content.Server._Nibiru.Fuel;
using Content.Server.Storage.EntitySystems;
using Content.Server.Temperature.Systems;
using Content.Shared._Nibiru.Fuel;
using Content.Shared._Nibiru.Heating;
using Content.Shared._Nibiru.Smelting;
using Content.Shared.Damage;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Placeable;
using Content.Shared.Popups;
using Content.Shared.Storage;
using Content.Shared.Temperature;
using Content.Shared.Temperature.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;

namespace Content.Server._Nibiru.Heating;

/// <summary>
/// Система для нагрева предметов на поверхности (костры, жаровни и т.д.)
/// </summary>
public sealed class HeatingSurfaceSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly TemperatureSystem _temperature = default!;
    [Dependency] private readonly DamageableSystem _damageableSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HeatingSurfaceComponent, ExaminedEvent>(OnExamined);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<HeatingSurfaceComponent, FuelConsumptionComponent>();
        while (query.MoveNext(out var uid, out var surface, out var fuel))
        {
            // Поверхность нагревает только если источник достаточно горячий
            if (fuel.CurrentTemperature < surface.MinSourceTemperature)
            {
                UpdateVisuals(uid, surface, false, false);
                continue;
            }

            UpdateHeating((uid, surface, fuel), frameTime);
        }
    }

    /// <summary>
    /// Обновляет нагрев предметов на поверхности
    /// </summary>
    private void UpdateHeating(
        Entity<HeatingSurfaceComponent, FuelConsumptionComponent> ent,
        float dt)
    {
        var (uid, surface, fuel) = ent;
        var sourceTemp = fuel.CurrentTemperature;
        var hasItems = false;
        var isHeating = false;

        // Получаем предметы на поверхности
        List<EntityUid> itemsToHeat;

        if (surface.RequirePlacedOnSurface &&
            TryComp<ItemPlacerComponent>(uid, out var placeable))
        {
            // Используем PlaceableSurface
            itemsToHeat = GetItemsOnSurface(uid, placeable);
        }
        else
        {
            // Используем радиус
            itemsToHeat = GetItemsInRadius(uid, surface);
        }

        hasItems = itemsToHeat.Count > 0;

        // Обрабатываем каждый предмет
        foreach (var entity in itemsToHeat)
        {
            ProcessItem(uid, entity, surface, sourceTemp, dt);
            isHeating = true;
        }

        // Звук готовки
        if (isHeating)
        {
            surface.CookingSoundTimer -= dt;
            if (surface.CookingSoundTimer <= 0f)
            {
                if (surface.CookingSound != null)
                    _audio.PlayPvs(surface.CookingSound, uid);

                surface.CookingSoundTimer = surface.CookingSoundInterval;
            }
        }

        UpdateVisuals(uid, surface, hasItems, isHeating);
        Dirty(uid, surface);
    }

    /// <summary>
    /// Получает предметы с PlaceableSurface
    /// </summary>
    private List<EntityUid> GetItemsOnSurface(
        EntityUid surfaceUid,
        ItemPlacerComponent placeable)
    {
        var items = new List<EntityUid>();

        foreach (var item in placeable.PlacedEntities)
        {
            if (Exists(item))
                items.Add(item);
        }

        return items;
    }

    /// <summary>
    /// Получает предметы в радиусе
    /// </summary>
    private List<EntityUid> GetItemsInRadius(
        EntityUid surfaceUid,
        HeatingSurfaceComponent surface)
    {
        var items = new List<EntityUid>();
        var xform = Transform(surfaceUid);

        var nearby = _lookup.GetEntitiesInRange(
            xform.Coordinates,
            surface.HeatingRadius
        );

        foreach (var entity in nearby)
        {
            if (entity == surfaceUid)
                continue;

            // Проверяем что предмет находится на той же высоте
            //var itemXform = Transform(entity);
            //if (!_transform.IsParentOf(xform, itemXform.ParentUid))
            //    continue;

            if (HasComp<TemperatureComponent>(entity))
                items.Add(entity);
        }

        return items;
    }

    /// <summary>
    /// Обрабатывает нагрев отдельного предмета
    /// </summary>
    private bool ProcessItem(
        EntityUid surfaceUid,
        EntityUid itemUid,
        HeatingSurfaceComponent surface,
        float sourceTemp,
        float dt)
    {
        // Руда
        if (TryComp<SmeltableOreComponent>(itemUid, out var ore))
        {
            return ProcessOreOnSurface(surfaceUid, itemUid, surface, ore, sourceTemp, dt);
        }

        // Обычный предмет с температурой
        if (TryComp<TemperatureComponent>(itemUid, out var temp))
        {
            return ProcessTemperatureOnSurface(surfaceUid, itemUid, surface, temp, sourceTemp, dt);
        }

        return false;
    }

    /// <summary>
    /// Обрабатывает руду на поверхности
    /// </summary>
    private bool ProcessOreOnSurface(
        EntityUid surfaceUid,
        EntityUid oreUid,
        HeatingSurfaceComponent surface,
        SmeltableOreComponent ore,
        float sourceTemp,
        float dt)
    {
        // Нагреваем руду
        if (TryComp<TemperatureComponent>(oreUid, out var temp))
        {
            HeatEntity(oreUid, temp, sourceTemp, dt, surface.HeatingRate);
        }

        var currentTemp = temp?.CurrentTemperature ?? sourceTemp;

        // Если достигли температуры плавления
        if (currentTemp >= ore.MeltingPoint)
        {
            ore.MeltingProgress += ore.MeltingSpeed * dt;

            if (ore.MeltingProgress >= 1f)
            {
                // Руда расплавилась - создаём лужу металла или удаляем
                _popup.PopupEntity(
                    Loc.GetString("heating-surface-ore-melted"),
                    oreUid,
                    PopupType.Medium
                );

                var ev = new OreMeltedOnSurfaceEvent(surfaceUid, oreUid);
                RaiseLocalEvent(surfaceUid, ev);

                //QueueDel(oreUid);
                return false;
            }

            Dirty(oreUid, ore);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Обрабатывает обычный предмет
    /// </summary>
    private bool ProcessTemperatureOnSurface(
        EntityUid surfaceUid,
        EntityUid itemUid,
        HeatingSurfaceComponent surface,
        TemperatureComponent temp,
        float sourceTemp,
        float dt)
    {
        HeatEntity(itemUid, temp, sourceTemp, dt, surface.HeatingRate);

        // Если предмет слишком горячий - сжигаем
        if (temp.CurrentTemperature >= surface.BurnTemperature)
        {
            BurnItem(surfaceUid, itemUid, surface, sourceTemp, dt);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Нагревает предмет
    /// </summary>
    private void HeatEntity(
        EntityUid ent,
        TemperatureComponent temp,
        float targetTemp,
        float dt,
        float rate)
    {
        if (temp.CurrentTemperature < targetTemp)
        {
            var energy = targetTemp * dt;
            _temperature.ChangeHeat(ent, energy);
        }
    }

    /// <summary>
    /// Сжигает предмет
    /// </summary>
    private void BurnItem(
        EntityUid surfaceUid,
        EntityUid itemUid,
        HeatingSurfaceComponent surface,
        float temp,
        float dt)
    {
        //if (surface.BurnSound != null)
        //    _audio.PlayPvs(surface.BurnSound, surfaceUid);

        //_popup.PopupEntity(
        //    Loc.GetString("heating-surface-item-burned"),
        //    itemUid,
        //    PopupType.Medium
        //);

        DamageSpecifier burnDamage = new();
        burnDamage.DamageDict = new Dictionary<string, FixedPoint2>
        {
            { "Heat", temp / 100 * dt }
        };
        _damageableSystem.TryChangeDamage(itemUid, burnDamage, origin: surfaceUid);

        //var ev = new ItemBurnedOnSurfaceEvent(surfaceUid, itemUid);
        //RaiseLocalEvent(surfaceUid, ev);

        //QueueDel(itemUid);
    }

    /// <summary>
    /// Обновляет визуалы
    /// </summary>
    private void UpdateVisuals(
        EntityUid uid,
        HeatingSurfaceComponent surface,
        bool hasItems,
        bool isHeating)
    {
        if (!TryComp<AppearanceComponent>(uid, out var appearance))
            return;

        _appearance.SetData(uid, HeatingSurfaceVisuals.HasItems, hasItems, appearance);
        _appearance.SetData(uid, HeatingSurfaceVisuals.IsHeating, isHeating, appearance);
    }

    private void OnExamined(EntityUid uid, HeatingSurfaceComponent comp, ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        if (TryComp<FuelConsumptionComponent>(uid, out var fuel))
        {
            if (fuel.CurrentTemperature >= comp.MinSourceTemperature)
            {
                args.PushMarkup(Loc.GetString(
                    "heating-surface-examine-hot",
                    ("temperature", $"{fuel.CurrentTemperature:F0}")
                ));
            }
            else
            {
                args.PushMarkup(Loc.GetString("heating-surface-examine-cold"));
            }
        }
    }
}

/// <summary>
/// Событие когда руда расплавилась на поверхности
/// </summary>
public sealed class OreMeltedOnSurfaceEvent : EntityEventArgs
{
    public EntityUid Surface;
    public EntityUid Ore;

    public OreMeltedOnSurfaceEvent(EntityUid surface, EntityUid ore)
    {
        Surface = surface;
        Ore = ore;
    }
}

/// <summary>
/// Событие когда предмет сгорел на поверхности
/// </summary>
public sealed class ItemBurnedOnSurfaceEvent : EntityEventArgs
{
    public EntityUid Surface;
    public EntityUid Item;

    public ItemBurnedOnSurfaceEvent(EntityUid surface, EntityUid item)
    {
        Surface = surface;
        Item = item;
    }
}
