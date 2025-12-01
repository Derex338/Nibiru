using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Content.Server.EUI;
using Content.Shared.IdentityManagement;
using System.Linq;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Robust.Shared.Serialization.Manager;
using Content.Server._Nibiru.Fuel;
using Content.Shared._Nibiru.Fuel;
using Content.Server.Stack;
using Content.Shared.Stacks;
using Robust.Shared.Audio.Systems;
using Content.Shared.Item;
using Content.Shared.IgnitionSource;
using Content.Shared.NameModifier.EntitySystems;
using JetBrains.Annotations;
using Content.Shared.Light.Components;
using Content.Server.Light.Components;
using Robust.Server.GameObjects;
using Content.Shared.Examine;

namespace Content.Server._Nibiru.Fuel
{

    [UsedImplicitly]
    public sealed class FuelSystem : EntitySystem
    {
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
        [Dependency] private readonly SharedDoAfterSystem _doAfterSystem = default!;
        [Dependency] private readonly NameModifierSystem _nameModifier = default!;
        [Dependency] private readonly StackSystem _stackSystem = default!;
        [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
        [Dependency] private readonly SharedAudioSystem _audio = default!;
        [Dependency] private readonly SharedItemSystem _item = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<FuelConsumptionComponent, InteractUsingEvent>(AddFuel);
            SubscribeLocalEvent<FuelConsumptionComponent, ComponentInit>(OnExpLightInit);

            SubscribeLocalEvent<FuelConsumptionComponent, ExaminedEvent>(OnExamined);
        }

        public override void Update(float frameTime)
        {
            var query = EntityQueryEnumerator<FuelConsumptionComponent>();
            while (query.MoveNext(out var uid, out var light))
            {
                UpdateFuel((uid, light), frameTime);
            }
        }

        private void UpdateFuel(Entity<FuelConsumptionComponent> ent, float frameTime)
        {
            var component = ent.Comp;
            if (!component.Activated)
                return;

            component.StateExpiryTime -= frameTime;

            if (component.StateExpiryTime <= 0f)
            {
                var ev = new FuelStateChangedEvent(false, component.StateExpiryTime, component.Temperature);
                RaiseLocalEvent(ent, ref ev);

                switch (component.CurrentState)
                {
                    case FuelLightState.Lit:
                        component.CurrentState = FuelLightState.Fading;
                        component.StateExpiryTime = (float)component.FadeOutDuration.TotalSeconds;

                        UpdateVisualizer(ent);

                        break;

                    default:
                    case FuelLightState.Fading:
                        component.CurrentState = FuelLightState.Dead;
                        component.Temperature = 0.1f;
                        _nameModifier.RefreshNameModifiers(ent.Owner);

                        UpdateSounds(ent);
                        UpdateVisualizer(ent);

                        if (TryComp<ItemComponent>(ent, out var item))
                        {
                            _item.SetHeldPrefix(ent, "unlit", component: item);
                        }

                        break;
                }
            }
        }

        private void AddFuel(EntityUid uid, FuelConsumptionComponent component, InteractUsingEvent args)
        {
            if (args.Handled)
                return;

            if (TryComp<IgnitionSourceComponent>(args.Used, out var ignit) && ignit.Ignited && TryActivate((uid, component)))
                return;

            if (!TryComp<FuelComponent>(args.Used, out var fuel))
                return;

            if (component.StateExpiryTime + fuel.Value >= component.MaxFuelAmount)
                return;

            if (TryComp(args.Used, out StackComponent? stack))
            {
                if (component.CurrentState is FuelLightState.Dead)
                {
                    component.CurrentState = FuelLightState.BrandNew;
                    component.StateExpiryTime = (float)fuel.Value;

                    _nameModifier.RefreshNameModifiers(uid);
                    _stackSystem.SetCount(args.Used, stack.Count - 1, stack);
                    UpdateVisualizer((uid, component));
                    return;
                }

                component.StateExpiryTime += (float)fuel.Value;
                component.Temperature = (float)fuel.TemperatureMax;
                _stackSystem.SetCount(args.Used, stack.Count - 1, stack);
            }
            else
            {
                if (component.CurrentState is FuelLightState.Dead)
                    component.CurrentState = FuelLightState.BrandNew;

                component.StateExpiryTime += (float)fuel.Value;
                component.Temperature = fuel.TemperatureMax;
                EntityManager.QueueDeleteEntity(args.Used);
            }

            args.Handled = true;
        }

        private void UpdateVisualizer(Entity<FuelConsumptionComponent> ent, AppearanceComponent? appearance = null)
        {
            var component = ent.Comp;
            if (!Resolve(ent, ref appearance, false))
                return;

            _appearance.SetData(ent, FuelLightVisuals.State, component.CurrentState, appearance);

            switch (component.CurrentState)
            {
                case FuelLightState.Lit:
                    _appearance.SetData(ent, FuelLightVisuals.Behavior, component.TurnOnBehaviourID, appearance);
                    break;

                case FuelLightState.Fading:
                    _appearance.SetData(ent, FuelLightVisuals.Behavior, component.FadeOutBehaviourID, appearance);
                    break;

                case FuelLightState.Dead:
                    _appearance.SetData(ent, FuelLightVisuals.Behavior, string.Empty, appearance);
                    var ignite = new IgnitionEvent(false);
                    RaiseLocalEvent(ent, ref ignite);
                    break;
            }
        }

        private void UpdateSounds(Entity<FuelConsumptionComponent> ent)
        {
            var component = ent.Comp;

            switch (component.CurrentState)
            {
                case FuelLightState.Lit:
                    _audio.PlayPvs(component.LitSound, ent);
                    break;
                case FuelLightState.Fading:
                    break;
                default:
                    _audio.PlayPvs(component.DieSound, ent);
                    break;
            }
        }

        public bool TryActivate(Entity<FuelConsumptionComponent> ent)
        {
            var component = ent.Comp;
            if (!component.Activated && component.CurrentState == FuelLightState.BrandNew)
            {
                if (TryComp<ItemComponent>(ent, out var item))
                {
                    _item.SetHeldPrefix(ent, "lit", component: item);
                }

                var ignite = new IgnitionEvent(true);
                RaiseLocalEvent(ent, ref ignite);

                var ev = new FuelStateChangedEvent(true, component.StateExpiryTime, component.Temperature);
                RaiseLocalEvent(ent, ref ev);

                component.CurrentState = FuelLightState.Lit;

                UpdateSounds(ent);
                UpdateVisualizer(ent);
            }
            return true;
        }

        private void OnExpLightInit(EntityUid uid, FuelConsumptionComponent component, ComponentInit args)
        {
            if (TryComp<ItemComponent>(uid, out var item))
            {
                _item.SetHeldPrefix(uid, "unlit", component: item);
            }

            component.CurrentState = FuelLightState.BrandNew;
            component.StateExpiryTime = 100f;
            EnsureComp<PointLightComponent>(uid);
        }

        private void OnExamined(Entity<FuelConsumptionComponent> ent, ref ExaminedEvent args)
        {
            if (!args.IsInDetailsRange)
                return;

            args.PushMarkup(Loc.GetString("entity-fuel-examined", ("ExpiryTime", ent.Comp.StateExpiryTime.ToString("F1"))));
        }

    }
}
