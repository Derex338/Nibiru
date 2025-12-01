using Content.Server._Nibiru.Fuel;
using Content.Server.Chemistry.Containers.EntitySystems;
using Content.Server.Stack;
using Content.Shared._Nibiru.Fuel;
using Content.Shared._Nibiru.Smelting;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Tag;
using Content.Shared.Temperature.Components;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
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
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly StackSystem _stackSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SmeltingFurnaceComponent, ComponentInit>(OnFurnaceInit);
        SubscribeLocalEvent<SmeltingFurnaceComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<SmeltingFurnaceComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<SmeltingFurnaceComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);

        // Слушаем события топлива
        //SubscribeLocalEvent<SmeltingFurnaceComponent, FuelStateChangedEvent>(OnFuelStateChanged);
    }

    private void OnFurnaceInit(EntityUid uid, SmeltingFurnaceComponent component, ComponentInit args)
    {
        // Создаём контейнер для руд
        component.OreContainer = _container.EnsureContainer<Container>(uid, component.ContainerId);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<SmeltingFurnaceComponent, FuelConsumptionComponent>();
        while (query.MoveNext(out var uid, out var furnace, out var fuel))
        {
            UpdateFurnaceTemperature(uid, furnace, fuel, frameTime);
            UpdateSmelting(uid, furnace, frameTime);
        }
    }

    /// <summary>
    /// Обновляет температуру печи в зависимости от топлива
    /// </summary>
    private void UpdateFurnaceTemperature(EntityUid uid, SmeltingFurnaceComponent furnace, FuelConsumptionComponent fuel, float frameTime)
    {
        var targetTemperature = 0f;

        // Если топливо горит - нагреваем до его температуры
        if (fuel.CurrentState is FuelLightState.Lit or FuelLightState.Fading)
        {
            targetTemperature = fuel.Temperature;
        }

        // Плавно изменяем температуру
        if (furnace.CurrentTemperature < targetTemperature)
        {
            // Нагрев
            furnace.CurrentTemperature = Math.Min(
                targetTemperature,
                furnace.CurrentTemperature + furnace.HeatingRate * frameTime
            );
        }
        else if (furnace.CurrentTemperature > targetTemperature)
        {
            // Остывание
            furnace.CurrentTemperature = Math.Max(
                targetTemperature,
                furnace.CurrentTemperature - furnace.CoolingRate * frameTime
            );
        }

        Dirty(uid, furnace);
    }

    /// <summary>
    /// Обновляет процесс плавки руд внутри печи
    /// </summary>
    private void UpdateSmelting(EntityUid uid, SmeltingFurnaceComponent furnace, float frameTime)
    {
        if (furnace.CurrentTemperature <= 0 || furnace.OreContainer == null)
            return;

        foreach (var entity in furnace.OreContainer.ContainedEntities)
        {
            if (!TryComp<TemperatureComponent>(entity, out var temp))
                continue;

            // Нагреваем руду
            temp.CurrentTemperature = Math.Min(
                furnace.CurrentTemperature,
                temp.CurrentTemperature + frameTime * 20f // Скорость нагрева
            );

            if (!TryComp<SmeltableOreComponent>(entity, out var ore))
                continue;

            // Если достигли температуры плавления - плавим
            if (temp.CurrentTemperature >= ore.MeltingPoint)
            {
                ore.MeltingProgress += ore.MeltingSpeed * frameTime;

                // Руда полностью расплавилась
                if (ore.MeltingProgress >= 1f)
                {
                    MeltOre(uid, entity, furnace, ore);
                }
            }

            Dirty(entity, ore);
        }

        // Обновляем визуалы
        //if (TryComp<AppearanceComponent>(uid, out var appearance))
        //{
        //    _appearance.SetData(uid, SmeltingFurnaceVisuals.ContainsOre, containedEntities.Count > 0, appearance);
        //    _appearance.SetData(uid, SmeltingFurnaceVisuals.IsSmelting, anythingSmelting, appearance);
        //}
    }

    /// <summary>
    /// Плавит руду в реагент
    /// </summary>
    private void MeltOre(EntityUid furnaceUid, EntityUid oreUid, SmeltingFurnaceComponent furnace, SmeltableOreComponent ore)
    {
        if (!_solution.TryGetSolution(furnaceUid, furnace.Solution, out var solution, out var solutionComp))
        {
            return;
        }

        solutionComp.AddReagent(ore.ResultReagent, ore.ResultAmount);
        solutionComp.Temperature = ore.ResultTemperature;

        //if (!_solution.TryAddSolution(solution.Value, solutionComp))
        //{
        //    Log.Warning($"Failed to add reagent {ore.ResultReagent} to furnace {furnaceUid}");

        //    return;
        //}
        //_solution.AddThermalEnergy(solution.Value, ore.ResultTemperature);

        // Звук
        if (furnace.MeltCompleteSound != null)
            _audio.PlayPvs(furnace.MeltCompleteSound, furnaceUid);

        // Событие
        //var ev = new OreMeltedEvent(oreUid, ore.ResultReagent, ore.ResultAmount, ore.ResultTemperature);
        //RaiseLocalEvent(furnaceUid, ref ev);

        // Удаляем руду
        QueueDel(oreUid);
    }

    /// <summary>
    /// Добавление руды в печь
    /// </summary>
    private void OnInteractUsing(EntityUid uid, SmeltingFurnaceComponent component, InteractUsingEvent args)
    {
        if (args.Handled || component.OreContainer == null)
            return;

        // Проверяем что это руда
        if (!HasComp<SmeltableOreComponent>(args.Used))
            return;

        if (component.Tags != null)
        {
            bool hasTag = false;
            foreach (var tag in component.Tags)
            {
                if (_tag.HasTag(args.Used, tag))
                {
                    hasTag = true;
                    break;
                }
            }
            if (!hasTag)
            {
                _popup.PopupEntity(Loc.GetString("smelting-furnace-incorrect-ore"), uid, args.User);
                return;
            }
        }

        // Проверяем вместимость
        if (component.OreContainer.ContainedEntities.Count >= component.MaxOreCapacity)
        {
            _popup.PopupEntity(Loc.GetString("smelting-furnace-full"), uid, args.User);
            return;
        }

        if (TryComp(args.Used, out StackComponent? stack))
        {
            if (stack.Count >= component.MaxOreCapacity || !_container.Insert(args.Used, component.OreContainer))
            {
                _popup.PopupEntity(Loc.GetString("smelting-furnace-insert-failed"), uid, args.User);
                return;
            }
        }
        else
        {
            // Добавляем руду в печь
            if (!_container.Insert(args.Used, component.OreContainer))
            {
                _popup.PopupEntity(Loc.GetString("smelting-furnace-insert-failed"), uid, args.User);
                return;
            }
        }

        //_popup.PopupEntity(Loc.GetString("smelting-furnace-ore-added"), uid, args.User);
        args.Handled = true;
    }

    /// <summary>
    /// Examine - показываем содержимое и температуру
    /// </summary>
    private void OnExamined(EntityUid uid, SmeltingFurnaceComponent component, ExaminedEvent args)
    {
        if (!args.IsInDetailsRange || component.OreContainer == null)
            return;

        args.PushMarkup(Loc.GetString("smelting-furnace-examine-temperature",
            ("temperature", $"{component.CurrentTemperature:F0}")));

        var oreCount = component.OreContainer.ContainedEntities.Count;
        if (oreCount > 0)
        {
            args.PushMarkup(Loc.GetString("smelting-furnace-examine-contains",
                ("count", oreCount),
                ("max", component.MaxOreCapacity)));
        }
        else
        {
            args.PushMarkup(Loc.GetString("smelting-furnace-examine-empty"));
        }
    }

    /// <summary>
    /// Verb для извлечения всех руд
    /// </summary>
    private void OnGetVerbs(EntityUid uid, SmeltingFurnaceComponent component, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || component.OreContainer == null)
            return;

        if (component.OreContainer.ContainedEntities.Count == 0)
            return;

        var verb = new AlternativeVerb
        {
            Text = Loc.GetString("smelting-furnace-verb-empty"),
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/eject.svg.192dpi.png")),
            Act = () =>
            {
                EmptyFurnace(uid, component, args.User);
            }
        };

        args.Verbs.Add(verb);
    }

    /// <summary>
    /// Опустошает печь
    /// </summary>
    private void EmptyFurnace(EntityUid uid, SmeltingFurnaceComponent component, EntityUid user)
    {
        if (component.OreContainer == null)
            return;

        //var xform = Transform(uid);
        //var coordinates = xform.Coordinates;

        foreach (var oreUid in component.OreContainer.ContainedEntities)
        {
            if (TryComp<TemperatureComponent>(oreUid, out var temp) && temp.CurrentTemperature > 300)
            {
                _popup.PopupEntity(Loc.GetString("smelting-furnace-ore-too-hot"), uid, user);
                continue;
            }
            else
                _container.Remove(oreUid, component.OreContainer);
            //Transform(oreUid).Coordinates = coordinates;
        }

        _popup.PopupEntity(Loc.GetString("smelting-furnace-emptied"), uid, user);
    }

    /// <summary>
    /// Реакция на изменение состояния топлива
    /// </summary>
    private void OnFuelStateChanged(EntityUid uid, SmeltingFurnaceComponent component, ref FuelStateChangedEvent args)
    {
        // Можно добавить звуки или эффекты когда печь нагревается/остывает
    }
}
