using Content.Server.Connection.Whitelist.Conditions;
using Content.Server.Stack;
using Content.Shared._Nibiru.Fuel;
using Content.Shared.Damage.Components;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.IdentityManagement;
using Content.Shared.IgnitionSource;
using Content.Shared.Interaction;
using Content.Shared.Item;
using Content.Shared.Light.Components;
using Content.Shared.NameModifier.EntitySystems;
using Content.Shared.Stacks;
using Content.Shared.Temperature.Components;
using Content.Shared.Whitelist;
using JetBrains.Annotations;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;

namespace Content.Server._Nibiru.Fuel;

[UsedImplicitly]
public sealed class FuelSystem : EntitySystem
{
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly NameModifierSystem _nameModifier = default!;
    [Dependency] private readonly StackSystem _stack = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedItemSystem _item = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FuelConsumptionComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<FuelConsumptionComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<FuelConsumptionComponent, ExaminedEvent>(OnExamined);
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<FuelConsumptionComponent>();
        while (query.MoveNext(out var uid, out var fuel))
        {
            UpdateFuelConsumption((uid, fuel), frameTime);
            UpdateTemperature((uid, fuel), frameTime);
        }
    }

    private void OnInit(EntityUid uid, FuelConsumptionComponent comp, ComponentInit args)
    {
        comp.CurrentState = FuelLightState.BrandNew;
        //comp.StateExpiryTime = 0f;
        comp.CurrentTemperature = 20f;

        EnsureComp<PointLightComponent>(uid);

        if (TryComp<ItemComponent>(uid, out var item))
            _item.SetHeldPrefix(uid, "unlit", component: item);

        UpdateVisualizer((uid, comp));
    }

    /// <summary>
    /// Обновляет потребление топлива
    /// </summary>
    private void UpdateFuelConsumption(Entity<FuelConsumptionComponent> ent, float dt)
    {
        var comp = ent.Comp;

        if (comp.CurrentState != FuelLightState.Lit && comp.CurrentState != FuelLightState.Fading)
            return;

        comp.StateExpiryTime -= dt * comp.FuelConsumptionRate;

        if (comp.StateExpiryTime <= 0f)
        {
            switch (comp.CurrentState)
            {
                case FuelLightState.Lit:
                    // Переход к затуханию
                    comp.CurrentState = FuelLightState.Fading;
                    comp.StateExpiryTime = (float)comp.FadeOutDuration.TotalSeconds;

                    var fadeEvent = new FuelStateChangedEvent(
                        true,
                        comp.StateExpiryTime,
                        comp.CurrentTemperature
                    );
                    RaiseLocalEvent(ent, ref fadeEvent);

                    UpdateVisualizer(ent);
                    break;

                case FuelLightState.Fading:
                    // Топливо закончилось
                    Extinguish(ent);
                    break;
            }
        }
    }

    /// <summary>
    /// Обновляет температуру объекта
    /// </summary>
    private void UpdateTemperature(Entity<FuelConsumptionComponent> ent, float dt)
    {
        var comp = ent.Comp;
        var oldTemp = comp.CurrentTemperature;
        var targetTemp = 20f;

        // Определяем целевую температуру
        if (comp.CurrentState == FuelLightState.Lit)
        {
            targetTemp = comp.TargetBurnTemperature;
        }
        else if (comp.CurrentState == FuelLightState.Fading)
        {
            // При затухании температура плавно падает
            var fadeProgress = comp.StateExpiryTime / (float)comp.FadeOutDuration.TotalSeconds;
            targetTemp = 20f + (comp.TargetBurnTemperature - 20f) * fadeProgress;
        }

        // Плавное изменение температуры
        if (comp.CurrentTemperature < targetTemp)
        {
            comp.CurrentTemperature = Math.Min(
                targetTemp,
                comp.CurrentTemperature + comp.HeatingRate * dt
            );
        }
        else if (comp.CurrentTemperature > targetTemp)
        {
            comp.CurrentTemperature = Math.Max(
                targetTemp,
                comp.CurrentTemperature - comp.CoolingRate * dt
            );
        }

        if (TryComp<TemperatureComponent>(ent, out var tempComp) && tempComp.CurrentTemperature < comp.CurrentTemperature)
        {
            tempComp.CurrentTemperature += comp.CurrentTemperature / 10 * dt;
        }

        // Проверка изменения операционного статуса
        var wasOperational = oldTemp >= comp.MinOperatingTemperature;
        var isOperational = comp.CurrentTemperature >= comp.MinOperatingTemperature;

        if (Math.Abs(oldTemp - comp.CurrentTemperature) > 0.1f || wasOperational != isOperational)
        {
            var tempEvent = new TemperatureChangedEvent(
                oldTemp,
                comp.CurrentTemperature,
                isOperational
            );
            RaiseLocalEvent(ent, ref tempEvent);
            Dirty(ent, comp);
        }
    }

    private void OnInteractUsing(EntityUid uid, FuelConsumptionComponent comp, InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        // Поджигание
        if (TryComp<IgnitionSourceComponent>(args.Used, out var ignit) && ignit.Ignited)
        {
            if (TryIgnite((uid, comp)))
            {
                args.Handled = true;
            }
            return;
        }

        // Добавление топлива
        if (!TryComp<FuelComponent>(args.Used, out var fuel))
            return;

        if (comp.FuelWhitelist != null && !_whitelist.IsValid(comp.FuelWhitelist, args.Used))
            return;

        var canAdd = comp.StateExpiryTime + fuel.Value <= comp.MaxFuelAmount;
        if (!canAdd)
            return;

        // Если объект погас, восстанавливаем его
        if (comp.CurrentState == FuelLightState.Dead)
        {
            comp.CurrentState = FuelLightState.BrandNew;
            comp.StateExpiryTime = fuel.Value;
            comp.TargetBurnTemperature = fuel.TemperatureMax;
            _nameModifier.RefreshNameModifiers(uid);
        }
        else
        {
            comp.StateExpiryTime += fuel.Value;
            comp.TargetBurnTemperature = Math.Max(comp.TargetBurnTemperature, fuel.TemperatureMax);
        }

        // Удаление топлива из стака или удаление объекта
        if (TryComp(args.Used, out StackComponent? stack))
        {
            _stack.SetCount(args.Used, stack.Count - 1, stack);
        }
        else
        {
            QueueDel(args.Used);
        }

        UpdateVisualizer((uid, comp));
        args.Handled = true;
    }

    /// <summary>
    /// Поджигает объект
    /// </summary>
    public bool TryIgnite(Entity<FuelConsumptionComponent> ent)
    {
        var comp = ent.Comp;

        if (comp.CurrentState != FuelLightState.BrandNew || comp.StateExpiryTime <= 0f)
            return false;

        comp.CurrentState = FuelLightState.Lit;

        if (TryComp<ItemComponent>(ent, out var item))
            _item.SetHeldPrefix(ent, "lit", component: item);

        var igniteEvent = new IgnitionEvent(true);
        RaiseLocalEvent(ent, ref igniteEvent);

        var stateEvent = new FuelStateChangedEvent(
            true,
            comp.StateExpiryTime,
            comp.CurrentTemperature
        );
        RaiseLocalEvent(ent, ref stateEvent);

        _audio.PlayPvs(comp.LitSound, ent);
        UpdateVisualizer(ent);

        return true;
    }

    /// <summary>
    /// Тушит объект
    /// </summary>
    public void Extinguish(Entity<FuelConsumptionComponent> ent)
    {
        var comp = ent.Comp;

        comp.CurrentState = FuelLightState.Dead;
        comp.StateExpiryTime = 0f;

        if (TryComp<ItemComponent>(ent, out var item))
            _item.SetHeldPrefix(ent, "unlit", component: item);

        var igniteEvent = new IgnitionEvent(false);
        RaiseLocalEvent(ent, ref igniteEvent);

        var stateEvent = new FuelStateChangedEvent(
            false,
            0f,
            comp.CurrentTemperature
        );
        RaiseLocalEvent(ent, ref stateEvent);

        _audio.PlayPvs(comp.DieSound, ent);
        _nameModifier.RefreshNameModifiers(ent.Owner);
        UpdateVisualizer(ent);
    }

    private void UpdateVisualizer(Entity<FuelConsumptionComponent> ent)
    {
        var comp = ent.Comp;

        if (!TryComp<AppearanceComponent>(ent, out var appearance))
            return;

        _appearance.SetData(ent, FuelLightVisuals.State, comp.CurrentState, appearance);

        switch (comp.CurrentState)
        {
            case FuelLightState.Lit:
                _appearance.SetData(ent, FuelLightVisuals.Behavior, comp.TurnOnBehaviourID, appearance);
                break;

            case FuelLightState.Fading:
                _appearance.SetData(ent, FuelLightVisuals.Behavior, comp.FadeOutBehaviourID, appearance);
                break;

            case FuelLightState.Dead:
            case FuelLightState.BrandNew:
                _appearance.SetData(ent, FuelLightVisuals.Behavior, string.Empty, appearance);
                break;
        }
    }

    private void OnExamined(EntityUid uid, FuelConsumptionComponent comp, ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        var fuelPercent = (comp.StateExpiryTime / comp.MaxFuelAmount) * 100f;
        args.PushMarkup(Loc.GetString("fuel-consumption-examined",
            ("fuel", comp.StateExpiryTime.ToString("F0")),
            ("percent", fuelPercent.ToString("F0")),
            ("temperature", comp.CurrentTemperature.ToString("F0"))
        ));

        if (comp.IsOperational)
        {
            args.PushMarkup(Loc.GetString("fuel-consumption-operational"));
        }
    }
}
