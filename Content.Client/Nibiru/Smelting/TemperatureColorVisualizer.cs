using Content.Client.Nibiru.Fuel;
using Content.Shared._Nibiru.Smelting;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using System.Linq;

namespace Content.Client.Nibiru.Smelting;

/// <summary>
/// Альтернативный визуализатор с более сложной логикой
/// </summary>
public sealed class TemperatureColorAdvancedVisualizer : VisualizerSystem<TemperatureColorComponent>
{
    /// <summary>
    /// Слой базового спрайта
    /// </summary>
    [DataField("baseLayer")]
    public int BaseLayer = 0;

    /// <summary>
    /// Слой свечения (overlay)
    /// </summary>
    [DataField("glowLayer")]
    public int? GlowLayer = 1;

    /// <summary>
    /// Температура, при которой начинается видимое изменение цвета
    /// </summary>
    [DataField("colorChangeThreshold")]
    public float ColorChangeThreshold = 600f;

    /// <summary>
    /// Интенсивность эффекта модуляции (0-1)
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

        // Базовый слой - смешиваем с оригинальным цветом
        if (BaseLayer < sprite.AllLayers.Count())
        {
            if (temperature >= ColorChangeThreshold)
            {
                // Интерполируем между белым и цветом свечения
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

        // Слой свечения - показываем только при высокой температуре
        if (GlowLayer.HasValue && GlowLayer.Value < sprite.AllLayers.Count())
        {
            if (temperature >= 800f)
            {
                SpriteSystem.LayerSetVisible(uid, GlowLayer.Value, true);

                // Прозрачность зависит от температуры
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
