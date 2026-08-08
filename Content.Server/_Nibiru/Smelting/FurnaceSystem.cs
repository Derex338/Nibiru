using Content.Server._Nibiru.Fuel;
using Content.Server.Stack;
using Content.Shared._Nibiru.Fuel;
using Content.Shared._Nibiru.Smelting;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Kitchen;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Tag;
using Content.Shared.Temperature;
using Content.Shared.Temperature.Components;
using Content.Shared.Verbs;
using Microsoft.CodeAnalysis;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using System.Linq;
using Content.Shared.FixedPoint;

namespace Content.Server._Nibiru.Smelting;

public sealed partial class SmeltingFurnaceSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private TagSystem _tag = default!;
    [Dependency] private StackSystem _stack = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private ItemSlotsSystem _itemSlotsSystem = default!;

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
        //component.OreContainer = _container.EnsureContainer<Container>(uid, component.ContainerId);
        //component.SolutionContainer = _container.EnsureContainer<ContainerSlot>(uid, component.SolutionContainerId);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<SmeltingFurnaceComponent, FuelConsumptionComponent>();
        while (query.MoveNext(out var uid, out var furnace, out var fuel))
        {
            // Печь работает только если топливо достаточно горячее
            if (!fuel.IsOperational) //|| furnace.OreContainer == null)
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

        var inputContainer = _container.EnsureContainer<Container>(uid, furnace.ContainerId);

        foreach (var entity in inputContainer.ContainedEntities.ToArray())
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
            //BurnItem(furnaceUid, itemUid, furnace);
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
        var amountLeft = FixedPoint2.New((double)ore.ResultAmount);
        var outputContainer = _itemSlotsSystem.GetItemOrNull(furnaceUid, furnace.SolutionContainerId);

        if (outputContainer is not null && _solution.TryGetFitsInDispenser(outputContainer.Value, out var target, out var targetSolution))
        {
            var canAdd = targetSolution.AvailableVolume;
            if (canAdd > 0)
            {
                var toAddAmount = FixedPoint2.Min(canAdd, amountLeft);

                if (_solution.TryAddReagent(target.Value, ore.ResultReagent, toAddAmount, out var accepted, temperature: ore.ResultTemperature))
                {
                    amountLeft -= accepted;
                    _solution.SetTemperature(target.Value, ore.ResultTemperature);
                }
            }
        }

        if (amountLeft > 0 && _solution.TryGetSolution(furnaceUid, furnace.Solution, out var furnaceSoln, out var furnaceSolutionComp))
        {
            _solution.TryAddReagent(furnaceSoln.Value, ore.ResultReagent, amountLeft, out var accepted, temperature: ore.ResultTemperature);
            if (accepted > 0)
                _solution.SetTemperature(furnaceSoln.Value, ore.ResultTemperature);
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
        if (args.Handled)
            return;

        var inputContainer = _container.EnsureContainer<Container>(uid, comp.ContainerId);

        //Проверяем что это либо руда, либо предмет с температурой
        var canInsert = HasComp<SmeltableOreComponent>(args.Used);
                       //HasComp<TemperatureComponent>(args.Used);

        if (!canInsert)
        {
            return;
        }

        //Проверяем вместимость
        if (inputContainer.ContainedEntities.Count >= comp.MaxOreCapacity)
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

            if (!_container.Insert(splitStack.Value, inputContainer))
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
            if (!_container.Insert(args.Used, inputContainer))
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
        if (!args.IsInDetailsRange)
            return;

        var inputContainer = _container.EnsureContainer<Container>(uid, comp.ContainerId);

        var oreCount = inputContainer.ContainedEntities.Count;
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

        if (_solution.TryGetSolution(uid, comp.Solution, out var solutionEnt, out var solution))
        {
            args.PushMarkup(Loc.GetString(
                "smelting-furnace-examine-volume",
                ("amount", solution.Volume),
                ("capacity", solution.MaxVolume)
            ));
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
        if (!args.CanAccess || !args.CanInteract)
            return;

        var inputContainer = _container.EnsureContainer<Container>(uid, comp.ContainerId);

        if (inputContainer.ContainedEntities.Count > 0)
        {
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

        var outputContainer = _itemSlotsSystem.GetItemOrNull(uid, comp.SolutionContainerId);
        if (outputContainer != null && _solution.TryGetSolution(uid, comp.Solution, out var furnaceSoln, out var furnaceSolutionComp) && furnaceSolutionComp.Volume > 0)
        {
            var verbPour = new AlternativeVerb
            {
                Text = Loc.GetString("smelting-furnace-verb-pour"),
                Icon = new SpriteSpecifier.Texture(
                    new("/Textures/Interface/VerbIcons/spill.svg.192dpi.png")
                ),
                Priority = 1,
                Act = () => PourIntoContainer(uid, comp, args.User, outputContainer.Value, furnaceSoln.Value, furnaceSolutionComp)
            };
            args.Verbs.Add(verbPour);
        }
    }

    private void PourIntoContainer(EntityUid furnaceUid, SmeltingFurnaceComponent comp, EntityUid user, EntityUid targetContainer, Entity<SolutionComponent> furnaceSoln, Solution furnaceSolutionData)
    {
        if (!_solution.TryGetFitsInDispenser(targetContainer, out var targetSoln, out var targetSolutionData))
            return;

        // Re-fetch furnace solution to ensure up-to-date data
        if (!_solution.TryGetSolution(furnaceUid, comp.Solution, out var currentFurnaceSoln, out var currentFurnaceSolutionData))
            return;

        var transferAmount = FixedPoint2.Min(currentFurnaceSolutionData.Volume, targetSolutionData.AvailableVolume);

        if (transferAmount <= 0)
        {
            _popup.PopupEntity(Loc.GetString("smelting-furnace-container-full"), furnaceUid, user);
            return;
        }

        var split = _solution.SplitSolution(currentFurnaceSoln.Value, transferAmount);
        if (!_solution.TryAddSolution(targetSoln.Value, split))
        {
            // If failed to add, return to furnace
            _solution.TryAddSolution(currentFurnaceSoln.Value, split);
            return;
        }

        if (comp.MeltCompleteSound != null)
            _audio.PlayPvs(comp.MeltCompleteSound, furnaceUid);

        _popup.PopupEntity(Loc.GetString("smelting-furnace-poured"), furnaceUid, user);
    }

    /// <summary>
    /// Опустошает печь
    /// </summary>
    private void EmptyFurnace(EntityUid uid, SmeltingFurnaceComponent comp, EntityUid user)
    {
        var tooHot = false;
        var extracted = 0;

        var inputContainer = _container.EnsureContainer<Container>(uid, comp.ContainerId);

        // Извлекаем руду/предметы
        foreach (var itemUid in inputContainer.ContainedEntities.ToArray())
        {
            if (TryComp<TemperatureComponent>(itemUid, out var temp) &&
                temp.CurrentTemperature > 300)
            {
                tooHot = true;
                continue;
            }

            _container.Remove(itemUid, inputContainer);
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
