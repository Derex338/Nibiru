using Content.Client.Light.Components;
using Content.Shared.Light.Components;
using Content.Client._Nibiru.Fuel;
using Content.Shared._Nibiru.Fuel;
using Robust.Client.GameObjects;
using Robust.Shared.Audio.Systems;
using Content.Client.Light.EntitySystems;

namespace Content.Client._Nibiru.Fuel;

public sealed class FuelConsumptionSystem : VisualizerSystem<FuelConsumptionComponent>
{
    [Dependency] private readonly PointLightSystem _pointLightSystem = default!;
    [Dependency] private readonly SharedAudioSystem _audioSystem = default!;
    [Dependency] private readonly LightBehaviorSystem _lightBehavior = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FuelConsumptionComponent, ComponentShutdown>(OnLightShutdown);
    }

    private void OnLightShutdown(EntityUid uid, FuelConsumptionComponent component, ComponentShutdown args)
    {
        component.PlayingStream = _audioSystem.Stop(component.PlayingStream);
    }

    protected override void OnAppearanceChange(EntityUid uid, FuelConsumptionComponent comp, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (AppearanceSystem.TryGetData<string>(uid, FuelLightVisuals.Behavior, out var lightBehaviourID, args.Component)
            && TryComp<LightBehaviourComponent>(uid, out var lightBehaviour))
        {
            _lightBehavior.StopLightBehaviour((uid, lightBehaviour));

            if (!string.IsNullOrEmpty(lightBehaviourID))
            {
                _lightBehavior.StartLightBehaviour((uid, lightBehaviour), lightBehaviourID);
            }
            else if (TryComp<PointLightComponent>(uid, out var light))
            {
                _pointLightSystem.SetEnabled(uid, false, light);
            }
        }

        if (!AppearanceSystem.TryGetData<FuelLightState>(uid, FuelLightVisuals.State, out var state, args.Component))
            return;

        switch (state)
        {
            case FuelLightState.Lit:
                _audioSystem.Stop(comp.PlayingStream);
                comp.PlayingStream = _audioSystem.PlayPvs(
                    comp.LoopedSound, uid)?.Entity;

                if (SpriteSystem.LayerMapTryGet((uid, args.Sprite), FuelLightVisualLayers.Overlay, out var layerIdx, true))
                {
                    if (!string.IsNullOrWhiteSpace(comp.IconStateLit))
                        SpriteSystem.LayerSetRsiState((uid, args.Sprite), layerIdx, comp.IconStateLit);
                    if (!string.IsNullOrWhiteSpace(comp.SpriteShaderLit))
                        args.Sprite.LayerSetShader(layerIdx, comp.SpriteShaderLit);
                    else
                        args.Sprite.LayerSetShader(layerIdx, null, null);
                    if (comp.GlowColorLit.HasValue)
                        SpriteSystem.LayerSetColor((uid, args.Sprite), layerIdx, comp.GlowColorLit.Value);
                    SpriteSystem.LayerSetVisible((uid, args.Sprite), layerIdx, true);
                }

                if (comp.GlowColorLit.HasValue)
                    SpriteSystem.LayerSetColor((uid, args.Sprite), FuelLightVisualLayers.Glow, comp.GlowColorLit.Value);
                SpriteSystem.LayerSetVisible((uid, args.Sprite), FuelLightVisualLayers.Glow, true);

                break;
            case FuelLightState.Dead:
                comp.PlayingStream = _audioSystem.Stop(comp.PlayingStream);
                if (SpriteSystem.LayerMapTryGet((uid, args.Sprite), FuelLightVisualLayers.Overlay, out layerIdx, true))
                {
                    if (!string.IsNullOrWhiteSpace(comp.IconStateSpent))
                        SpriteSystem.LayerSetRsiState((uid, args.Sprite), layerIdx, comp.IconStateSpent);
                    if (!string.IsNullOrWhiteSpace(comp.SpriteShaderSpent))
                        args.Sprite.LayerSetShader(layerIdx, comp.SpriteShaderSpent);
                    else
                        args.Sprite.LayerSetShader(layerIdx, null, null);
                }

                SpriteSystem.LayerSetVisible((uid, args.Sprite), FuelLightVisualLayers.Glow, false);
                break;
        }
    }
}
