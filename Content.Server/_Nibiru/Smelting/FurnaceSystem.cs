using Content.Server._Nibiru.Fuel;
using Content.Server.Stack;
using Content.Shared._Nibiru.Fuel;
using Content.Shared._Nibiru.Smelting;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Tag;
using Content.Shared.Temperature;
using Content.Shared.Temperature.Components;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using System.Linq;

namespace Content.Server._Nibiru.Smelting;

public sealed class SmeltingFurnaceSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly StackSystem _stack = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SmeltingFurnaceComponent, ComponentInit>(OnFurnaceInit);
        SubscribeLocalEvent<SmeltingFurnaceComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<SmeltingFurnaceComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<SmeltingFurnaceComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);

        // Слушаем события от системы топлива
        SubscribeLocalEvent<SmeltingFurnaceComponent, TemperatureChangedEvent>(OnTemperatureChanged);
        SubscribeLocalEvent<SmeltingFurnaceComponent, FuelStateChangedEvent>(OnFuelStateChanged);
    }

    private void OnFurnaceInit(EntityUid uid, SmeltingFurnaceComponent component, ComponentInit args)
    {
        component.OreContainer = _container.EnsureContainer<Container>(uid, component.ContainerId);
        component.SolutionContainer = _container.EnsureContainer<Container>(uid, "solution_container");
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<SmeltingFurnaceComponent, FuelConsumptionComponent>();
        while (query.MoveNext(out var uid, out var furnace, out var fuel))
        {
            // Печь работает только если топливо достаточно горячее
            if (!fuel.IsOperational || furnace.OreContainer == null)
                continue;

            UpdateFurnaceContents((uid, furnace, fuel), frameTime);
        }
    }

    /// <summary>
    /// Обновляет содержимое печи - нагревает, плавит, сжигает
    /// </summary>
    private void UpdateFurnaceContents(
        Entity<SmeltingFurnaceComponent, FuelConsumptionComponent> ent,
        float dt)
    {
        var (uid, furnace, fuel) = ent;
        var furnaceTemp = fuel.CurrentTemperature;
        var anythingSmelting = false;

        foreach (var entity in furnace.OreContainer!.ContainedEntities.ToArray())
        {
            // Обрабатываем руду
            if (TryComp<SmeltableOreComponent>(entity, out var ore))
            {
                if (ProcessOre(uid, entity, furnace, ore, furnaceTemp, dt))
                {
                    anythingSmelting = true;
                    continue;
                }
            }

            // Обрабатываем обычные предметы с температурой
            if (TryComp<TemperatureComponent>(entity, out var temp))
            {
                ProcessTemperature(uid, entity, furnace, temp, furnaceTemp, dt);
            }
        }

        // Обновляем визуалы
        UpdateVisuals(uid, furnace, anythingSmelting);
    }

    /// <summary>
    /// Обрабатывает плавку руды
    /// </summary>
    private bool ProcessOre(
        EntityUid furnaceUid,
        EntityUid oreUid,
        SmeltingFurnaceComponent furnace,
        SmeltableOreComponent ore,
        float furnaceTemp,
        float dt)
    {
        // Нагреваем руду если есть компонент температуры
        if (TryComp<TemperatureComponent>(oreUid, out var temp))
        {
            HeatEntity(temp, furnaceTemp, dt, 30f); // Скорость нагрева руды
        }

        var currentTemp = temp?.CurrentTemperature ?? furnaceTemp;

        // Если достигли температуры плавления - плавим
        if (currentTemp >= ore.MeltingPoint)
        {
            ore.MeltingProgress += ore.MeltingSpeed * dt;

            // Руда полностью расплавилась
            if (ore.MeltingProgress >= 1f)
            {
                MeltOre(furnaceUid, oreUid, furnace, ore);
                return false;
            }

            Dirty(oreUid, ore);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Обрабатывает нагрев обычного предмета
    /// </summary>
    private void ProcessTemperature(
        EntityUid furnaceUid,
        EntityUid itemUid,
        SmeltingFurnaceComponent furnace,
        TemperatureComponent temp,
        float furnaceTemp,
        float dt)
    {
        HeatEntity(temp, furnaceTemp, dt, 40f); // Скорость нагрева предметов

        // Если предмет достиг температуры горения - сжигаем его
        if (temp.CurrentTemperature >= furnace.BurnTemperature)
        {
            BurnItem(furnaceUid, itemUid, furnace);
        }
    }

    /// <summary>
    /// Нагревает предмет
    /// </summary>
    private void HeatEntity(TemperatureComponent temp, float targetTemp, float dt, float rate)
    {
        if (temp.CurrentTemperature < targetTemp)
        {
            temp.CurrentTemperature = Math.Min(
                targetTemp,
                temp.CurrentTemperature + rate * dt
            );
        }
    }

    /// <summary>
    /// Плавит руду в реагент
    /// </summary>
    private void MeltOre(
        EntityUid furnaceUid,
        EntityUid oreUid,
        SmeltingFurnaceComponent furnace,
        SmeltableOreComponent ore)
    {
        Entity<SolutionContainerManagerComponent>? containerEntity = null;
        string solutionName = string.Empty;

        // Проверяем есть ли сосуд в печи
        if (furnace.SolutionContainer != null && furnace.SolutionContainer.ContainedEntities.Count > 0)
        {
            foreach (var container in furnace.SolutionContainer.ContainedEntities)
            {
                if (TryComp<SolutionContainerManagerComponent>(container, out var comp))
                {
                    containerEntity = (container, comp);
                    solutionName = comp.Containers.FirstOrDefault() ?? string.Empty;
                    break;
                }
            }
        }

        // Если нашли сосуд - льём в него
        if (containerEntity.HasValue && !string.IsNullOrEmpty(solutionName))
        {
            if (_solution.TryGetSolution(containerEntity.Value.Owner, solutionName, out var solution, out var solutionComp))
            {
                _solution.TryAddReagent(
                    solution.Value,
                    ore.ResultReagent,
                    ore.ResultAmount,
                    out _);

                //solutionComp.AddReagent(ore.ResultReagent, ore.ResultAmount);
                //solutionComp.Temperature = ore.ResultTemperature;
            }
        }
        // Иначе льём в хранилище печи
        else if (_solution.TryGetSolution(furnaceUid, furnace.Solution, out var furnaceSolution, out var furnaceSolutionComp))
        {
            furnaceSolutionComp.AddReagent(ore.ResultReagent, ore.ResultAmount);
            furnaceSolutionComp.Temperature = ore.ResultTemperature;
        }

        // Звук плавления
        if (furnace.MeltCompleteSound != null)
            _audio.PlayPvs(furnace.MeltCompleteSound, furnaceUid);

        // Событие
        var ev = new OreMeltedEvent(oreUid, ore.ResultReagent, ore.ResultAmount);
        RaiseLocalEvent(furnaceUid, ev);

        // Удаляем руду
        QueueDel(oreUid);
    }


    /// <summary>
    /// Сжигает предмет
    /// </summary>
    private void BurnItem(EntityUid furnaceUid, EntityUid itemUid, SmeltingFurnaceComponent furnace)
    {
        if (furnace.BurnSound != null)
            _audio.PlayPvs(furnace.BurnSound, furnaceUid);

        var ev = new ItemBurnedEvent(itemUid);
        RaiseLocalEvent(furnaceUid, ev);

        QueueDel(itemUid);
    }

    /// <summary>
    /// Добавление предмета в печь
    /// </summary>
    private void OnInteractUsing(EntityUid uid, SmeltingFurnaceComponent comp, InteractUsingEvent args)
    {
        if (args.Handled || comp.OreContainer == null || comp.SolutionContainer == null)
            return;

        // Проверяем что это сосуд с раствором
        if (HasComp<SolutionContainerManagerComponent>(args.Used))
        {
            // Можно вставить только если слот пуст
            if (comp.SolutionContainer.ContainedEntities.Count > 0)
            {
                _popup.PopupEntity(
                    Loc.GetString("smelting-furnace-container-slot-occupied"),
                    uid,
                    args.User
                );
                return;
            }

            if (!_container.Insert(args.Used, comp.SolutionContainer))
            {
                _popup.PopupEntity(
                    Loc.GetString("smelting-furnace-insert-failed"),
                    uid,
                    args.User
                );
                return;
            }

            _popup.PopupEntity(
                Loc.GetString("smelting-furnace-container-inserted"),
                uid,
                args.User
            );
            args.Handled = true;
            return;
        }

        // Проверяем что это либо руда, либо предмет с температурой
        var canInsert = HasComp<SmeltableOreComponent>(args.Used) ||
                       HasComp<TemperatureComponent>(args.Used);

        if (!canInsert)
        {
            _popup.PopupEntity(
                Loc.GetString("smelting-furnace-cant-insert"),
                uid,
                args.User
            );
            return;
        }

        // Проверяем теги если есть whitelist
        if (comp.Tags != null && comp.Tags.Count > 0)
        {
            bool hasTag = false;
            foreach (var tag in comp.Tags)
            {
                if (_tag.HasTag(args.Used, tag))
                {
                    hasTag = true;
                    break;
                }
            }

            if (!hasTag)
            {
                _popup.PopupEntity(
                    Loc.GetString("smelting-furnace-incorrect-item"),
                    uid,
                    args.User
                );
                return;
            }
        }

        // Проверяем вместимость
        if (comp.OreContainer.ContainedEntities.Count >= comp.MaxOreCapacity)
        {
            _popup.PopupEntity(
                Loc.GetString("smelting-furnace-full"),
                uid,
                args.User
            );
            return;
        }

        // Если это стак - берём один предмет
        if (TryComp<StackComponent>(args.Used, out var stack) && stack.Count > 1)
        {
            var splitStack = _stack.Split(
                (args.Used, stack),
                1,
                Transform(uid).Coordinates
            );

            if (!splitStack.HasValue)
                return;

            if (!_container.Insert(splitStack.Value, comp.OreContainer))
            {
                _popup.PopupEntity(
                    Loc.GetString("smelting-furnace-insert-failed"),
                    uid,
                    args.User
                );
                return;
            }
        }
        else
        {
            if (!_container.Insert(args.Used, comp.OreContainer))
            {
                _popup.PopupEntity(
                    Loc.GetString("smelting-furnace-insert-failed"),
                    uid,
                    args.User
                );
                return;
            }
        }

        _popup.PopupEntity(
            Loc.GetString("smelting-furnace-insert-success"),
            uid,
            args.User
        );

        args.Handled = true;
    }

    /// <summary>
    /// Examine - показываем содержимое и температуру
    /// </summary>
    private void OnExamined(EntityUid uid, SmeltingFurnaceComponent comp, ExaminedEvent args)
    {
        if (!args.IsInDetailsRange || comp.OreContainer == null)
            return;

        if (TryComp<FuelConsumptionComponent>(uid, out var fuel))
        {
            args.PushMarkup(Loc.GetString(
                "smelting-furnace-examine-temperature",
                ("temperature", $"{fuel.CurrentTemperature:F0}")
            ));

            if (fuel.IsOperational)
                args.PushMarkup(Loc.GetString("smelting-furnace-examine-operational"));
            else
                args.PushMarkup(Loc.GetString("smelting-furnace-examine-cold"));
        }

        var oreCount = comp.OreContainer.ContainedEntities.Count;
        if (oreCount > 0)
        {
            args.PushMarkup(Loc.GetString(
                "smelting-furnace-examine-contains",
                ("count", oreCount)
            ));
        }
        else
        {
            args.PushMarkup(Loc.GetString("smelting-furnace-examine-empty"));
        }
    }

    /// <summary>
    /// Verb для извлечения содержимого
    /// </summary>
    private void OnGetVerbs(
        EntityUid uid,
        SmeltingFurnaceComponent comp,
        GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || comp.OreContainer == null || comp.SolutionContainer == null)
            return;

        if (comp.OreContainer.ContainedEntities.Count == 0 && comp.SolutionContainer.ContainedEntities.Count == 0)
            return;

        var verb = new AlternativeVerb
        {
            Text = Loc.GetString("smelting-furnace-verb-empty"),
            Icon = new SpriteSpecifier.Texture(
                new("/Textures/Interface/VerbIcons/eject.svg.192dpi.png")
            ),
            Act = () => EmptyFurnace(uid, comp, args.User)
        };

        args.Verbs.Add(verb);
    }

    /// <summary>
    /// Опустошает печь
    /// </summary>
    private void EmptyFurnace(EntityUid uid, SmeltingFurnaceComponent comp, EntityUid user)
    {
        if (comp.OreContainer == null || comp.SolutionContainer == null)
            return;

        var tooHot = false;
        var extracted = 0;

        // Извлекаем руду/предметы
        foreach (var itemUid in comp.OreContainer.ContainedEntities.ToArray())
        {
            if (TryComp<TemperatureComponent>(itemUid, out var temp) &&
                temp.CurrentTemperature > 300)
            {
                tooHot = true;
                continue;
            }

            _container.Remove(itemUid, comp.OreContainer);
            extracted++;
        }

        // Извлекаем сосуд с раствором
        foreach (var itemUid in comp.SolutionContainer.ContainedEntities.ToArray())
        {
            _container.Remove(itemUid, comp.SolutionContainer);
            extracted++;
        }

        if (tooHot)
        {
            _popup.PopupEntity(
                Loc.GetString("smelting-furnace-items-too-hot"),
                uid,
                user
            );
        }

        if (extracted > 0)
        {
            _popup.PopupEntity(
                Loc.GetString("smelting-furnace-emptied", ("count", extracted)),
                uid,
                user
            );
        }
    }

    /// <summary>
    /// Обновляет визуалы печи
    /// </summary>
    private void UpdateVisuals(
        EntityUid uid,
        SmeltingFurnaceComponent comp,
        bool isSmelting)
    {
        if (!TryComp<AppearanceComponent>(uid, out var appearance))
            return;

        var hasItems = comp.OreContainer != null &&
                      comp.OreContainer.ContainedEntities.Count > 0;

        _appearance.SetData(uid, SmeltingFurnaceVisuals.ContainsOre, hasItems, appearance);
        _appearance.SetData(uid, SmeltingFurnaceVisuals.IsSmelting, isSmelting, appearance);
    }

    private void OnTemperatureChanged(
        EntityUid uid,
        SmeltingFurnaceComponent comp,
        ref TemperatureChangedEvent args)
    {
        // Можно добавить звуки или эффекты при изменении температуры
    }

    private void OnFuelStateChanged(
        EntityUid uid,
        SmeltingFurnaceComponent comp,
        ref FuelStateChangedEvent args)
    {
        // Можно добавить уведомления когда топливо заканчивается
        if (!args.IsLit && comp.OreContainer != null &&
            comp.OreContainer.ContainedEntities.Count > 0)
        {
            // Топливо закончилось, но в печи есть предметы
        }
    }
}

/// <summary>
/// Событие когда руда расплавилась
/// </summary>
public sealed class OreMeltedEvent : EntityEventArgs
{
    public EntityUid OreEntity;
    public string Reagent;
    public float Amount;

    public OreMeltedEvent(EntityUid oreEntity, string reagent, float amount)
    {
        OreEntity = oreEntity;
        Reagent = reagent;
        Amount = amount;
    }
}

/// <summary>
/// Событие когда предмет сгорел
/// </summary>
public sealed class ItemBurnedEvent : EntityEventArgs
{
    public EntityUid ItemEntity;

    public ItemBurnedEvent(EntityUid itemEntity)
    {
        ItemEntity = itemEntity;
    }
}
