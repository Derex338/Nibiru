using Content.Server.Connection.Whitelist.Conditions;
using Content.Server.Stack;
using Content.Shared._Nibiru.Fuel;
using Content.Shared.Atmos;
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
using Content.Shared.Tools.Systems;
using Content.Shared.Verbs;
using Content.Shared.Whitelist;
using JetBrains.Annotations;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Utility;

namespace Content.Server._Nibiru.Fuel;

[UsedImplicitly]
public sealed partial class FuelSystem : EntitySystem
{
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private NameModifierSystem _nameModifier = default!;
    [Dependency] private StackSystem _stack = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPointLightSystem _light = default!;
    [Dependency] private SharedItemSystem _item = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;
    [Dependency] private SharedToolSystem _tool = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FuelConsumptionComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<FuelConsumptionComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<FuelConsumptionComponent, ExtinguishEvent>(OnExtinguishEvent);
        SubscribeLocalEvent<FuelConsumptionComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<FuelConsumptionComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<FuelConsumptionComponent, ExtinguishDoAfterEvent>(OnExtinguishDoAfter);
        SubscribeLocalEvent<FuelConsumptionComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
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
        comp.CurrentTemperature = 20f;

        var light = EnsureComp<PointLightComponent>(uid);
        _light.SetEnabled(uid, false, light);

        if (TryComp<ItemComponent>(uid, out var item))
            _item.SetHeldPrefix(uid, "unlit", component: item);

        UpdateVisualizer((uid, comp));
    }

    /// <summary>
    /// Updates fuel consumption
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
                    // Fade out
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
                    // Fuel ran out
                    Extinguish(ent);
                    break;
            }
        }
    }

    /// <summary>
    /// Updates object temperature
    /// </summary>
    private void UpdateTemperature(Entity<FuelConsumptionComponent> ent, float dt)
    {
        var comp = ent.Comp;
        var oldTemp = comp.CurrentTemperature;
        var targetTemp = 20f;

        // Determining target temperature
        if (comp.CurrentState == FuelLightState.Lit)
        {
            targetTemp = comp.TargetBurnTemperature;
        }
        else if (comp.CurrentState == FuelLightState.Fading)
        {
            // Fade out temperature
            var fadeProgress = comp.StateExpiryTime / (float)comp.FadeOutDuration.TotalSeconds;
            targetTemp = 20f + (comp.TargetBurnTemperature - 20f) * fadeProgress;
        }

        // Smooth temperature change
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

        // Check for operational status change
        var wasOperational = oldTemp >= comp.MinOperatingTemperature;
        var isOperational = comp.CurrentTemperature >= comp.MinOperatingTemperature;

        if (wasOperational != isOperational)
        {
            var tempEvent = new TemperatureChangedEvent(
                oldTemp,
                comp.CurrentTemperature,
                isOperational
            );
            RaiseLocalEvent(ent, ref tempEvent);
            Dirty(ent, comp);
            UpdateVisualizer(ent);
        }
        else if (Math.Abs(oldTemp - comp.CurrentTemperature) > 5.0f)
        {
            // Rare update for temperature synchronization
            Dirty(ent, comp);
            UpdateVisualizer(ent);
        }
    }

    private void OnInteractUsing(EntityUid uid, FuelConsumptionComponent comp, InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (TryComp<IgnitionSourceComponent>(args.Used, out var ignit) && ignit.Ignited)
        {
            if (TryIgnite((uid, comp)))
            {
                args.Handled = true;
            }
            return;
        }

        // Extinguishing with tool
        if (comp.CanBeExtinguished && (comp.CurrentState == FuelLightState.Lit || comp.CurrentState == FuelLightState.Fading))
        {
            var isTool = false;
            if (comp.ExtinguisherWhitelist != null && _whitelist.IsValid(comp.ExtinguisherWhitelist, args.Used))
            {
                isTool = true;
            }
            else if (!string.IsNullOrEmpty(comp.ExtinguisherQuality) && _tool.HasQuality(args.Used, comp.ExtinguisherQuality))
            {
                isTool = true;
            }

            if (isTool)
            {
                var ev = new ExtinguishDoAfterEvent();
                var doAfterArgs = new DoAfterArgs(EntityManager, args.User, comp.ExtinguishToolDuration, ev, uid, target: uid, used: args.Used)
                {
                    BreakOnMove = true,
                    BreakOnDamage = true,
                    NeedHand = true
                };

                _doAfter.TryStartDoAfter(doAfterArgs);
                args.Handled = true;
                return;
            }
        }

        // Adding fuel
        if (!TryComp<FuelComponent>(args.Used, out var fuel))
            return;

        if (comp.FuelWhitelist != null && !_whitelist.IsValid(comp.FuelWhitelist, args.Used))
            return;

        var canAdd = comp.StateExpiryTime + fuel.Value <= comp.MaxFuelAmount;
        if (!canAdd)
            return;

        // If object is dead, restore it
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

    private void OnExtinguishEvent(EntityUid uid, FuelConsumptionComponent comp, ref ExtinguishEvent args)
    {
        if (comp.CanBeExtinguished)
            Extinguish((uid, comp));
    }

    private void OnExtinguishDoAfter(EntityUid uid, FuelConsumptionComponent comp, ExtinguishDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        Extinguish((uid, comp));
        args.Handled = true;
    }

    private void OnGetVerbs(EntityUid uid, FuelConsumptionComponent comp, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess || !comp.CanBeExtinguished || !comp.CanExtinguishByHand)
            return;

        if (comp.CurrentState != FuelLightState.Lit && comp.CurrentState != FuelLightState.Fading)
            return;

        AlternativeVerb verb = new()
        {
            Act = () => Extinguish((uid, comp)),
            Text = Loc.GetString("fuel-system-verb-extinguish"),
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/extinguish.svg.192dpi.png")),
            Priority = 0
        };
        args.Verbs.Add(verb);
    }

    private void OnActivate(EntityUid uid, FuelConsumptionComponent comp, ActivateInWorldEvent args)
    {
        if (args.Handled || !comp.CanBeExtinguished || !comp.CanExtinguishByHand)
            return;

        if (comp.CurrentState == FuelLightState.Lit || comp.CurrentState == FuelLightState.Fading)
        {
            Extinguish((uid, comp));
            args.Handled = true;
        }
    }

    public bool TryIgnite(Entity<FuelConsumptionComponent> ent)
    {
        var comp = ent.Comp;

        if ((comp.CurrentState != FuelLightState.BrandNew && comp.CurrentState != FuelLightState.Dead) || comp.StateExpiryTime <= 0f)
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

        if (comp.LoopedSound != null && comp.PlayingStream == null)
        {
            comp.PlayingStream = _audio.PlayPvs(comp.LoopedSound, ent, AudioParams.Default.WithLoop(true))?.Entity;
        }

        UpdateVisualizer(ent);

        return true;
    }

    public void Extinguish(Entity<FuelConsumptionComponent> ent)
    {
        var comp = ent.Comp;

        comp.CurrentState = FuelLightState.BrandNew;

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

        comp.PlayingStream = _audio.Stop(comp.PlayingStream);

        _nameModifier.RefreshNameModifiers(ent.Owner);
        UpdateVisualizer(ent);
    }

    private void UpdateVisualizer(Entity<FuelConsumptionComponent> ent)
    {
        var comp = ent.Comp;

        if (!TryComp<PointLightComponent>(ent, out var light))
            return;

        var isLit = comp.CurrentState == FuelLightState.Lit || comp.CurrentState == FuelLightState.Fading;
        if (light.Enabled != isLit)
        {
            _light.SetEnabled(ent, isLit, light);
        }

        if (!TryComp<AppearanceComponent>(ent, out var appearance))
            return;

        var behavior = string.Empty;
        if (comp.CurrentState == FuelLightState.Lit)
        {
            behavior = comp.CurrentTemperature < comp.TargetBurnTemperature * 0.8f
                ? (string.IsNullOrEmpty(comp.TurnOnBehaviourID) ? comp.LitBehaviourID : comp.TurnOnBehaviourID)
                : (string.IsNullOrEmpty(comp.LitBehaviourID) ? comp.TurnOnBehaviourID : comp.LitBehaviourID);
        }
        else if (comp.CurrentState == FuelLightState.Fading)
        {
            behavior = comp.FadeOutBehaviourID;
        }

        if (_appearance.TryGetData<string>(ent, FuelLightVisuals.Behavior, out var oldBehavior, appearance) && oldBehavior == behavior)
        {
            // =(
        }
        else
        {
            _appearance.SetData(ent, FuelLightVisuals.Behavior, behavior, appearance);
        }

        _appearance.SetData(ent, FuelLightVisuals.State, comp.CurrentState, appearance);
        Dirty(ent, comp);
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
