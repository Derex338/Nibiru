using Content.Client._Nibiru.Fuel;
using Content.Shared._Nibiru.Smelting;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using System.Linq;

namespace Content.Client._Nibiru.Smelting;

/// <summary>
/// Alternative visualizer with more complex logic
/// </summary>
public sealed class TemperatureColorAdvancedVisualizer : VisualizerSystem<TemperatureColorComponent>
{
    /// <summary>
    /// Base sprite layer
    /// </summary>
    [DataField("baseLayer")]
    public int BaseLayer = 0;

    /// <summary>
    /// Glow layer (overlay)
    /// </summary>
    [DataField("glowLayer")]
    public int? GlowLayer = 1;

    /// <summary>
    /// Temperature at which visible color change begins
    /// </summary>
    [DataField("colorChangeThreshold")]
    public float ColorChangeThreshold = 600f;

    /// <summary>
    /// Modulation effect intensity (0-1)
    /// </summary>
    [DataField("modulationStrength")]
    public float ModulationStrength = 0.7f;

    protected override void OnAppearanceChange(EntityUid uid, TemperatureColorComponent comp, ref AppearanceChangeEvent args)
    {
        if (!AppearanceSystem.TryGetData<float>(uid, TemperatureColorVisuals.Temperature, out var temperature, args.Component))
            return;

        if (!AppearanceSystem.TryGetData<Color>(uid, TemperatureColorVisuals.Color, out var glowColor, args.Component))
            return;

        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        // Base layer - mixing with original color
        if (BaseLayer < sprite.AllLayers.Count())
        {
            if (temperature >= ColorChangeThreshold)
            {
                // Interpolating between white and glow color
                float factor = Math.Clamp(
                    (temperature - ColorChangeThreshold) / 1000f * ModulationStrength,
                    0f, 1f
                );

                var baseColor = Color.InterpolateBetween(Color.White, glowColor, factor);
                SpriteSystem.LayerSetColor(uid, BaseLayer, baseColor);
            }
            else
            {
                SpriteSystem.LayerSetColor(uid, BaseLayer, Color.White);
            }
        }

        // Glow layer - showing only at high temperatures
        if (GlowLayer.HasValue && GlowLayer.Value < sprite.AllLayers.Count())
        {
            if (temperature >= 800f)
            {
                SpriteSystem.LayerSetVisible(uid, GlowLayer.Value, true);

                // Opacity depends on temperature
                float alpha = Math.Clamp((temperature - 800f) / 2000f, 0f, 1f);
                var overlayColor = glowColor.WithAlpha(alpha);

                SpriteSystem.LayerSetColor(uid, GlowLayer.Value, overlayColor);
            }
            else
            {
                SpriteSystem.LayerSetVisible(uid, GlowLayer.Value, false);
            }
        }
    }
}
