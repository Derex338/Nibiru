using Content.Shared._Nibiru.Smelting;
using Content.Shared.Temperature;
using JetBrains.Annotations;
using Robust.Server.GameObjects;

namespace Content.Server._Nibiru.Smelting;

[UsedImplicitly]
public sealed partial class TemperatureColorSystem : EntitySystem
{
    [Dependency] private AppearanceSystem _appearance = default!;
    [Dependency] private PointLightSystem _pointLight = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TemperatureColorComponent, OnTemperatureChangeEvent>(OnTemperatureChanged);
        SubscribeLocalEvent<TemperatureColorComponent, ComponentStartup>(OnComponentStartup);
    }

    private void OnComponentStartup(EntityUid uid, TemperatureColorComponent component, ComponentStartup args)
    {
        UpdateTemperatureVisuals(uid, component, component.CurrentTemperature);
    }

    private void OnTemperatureChanged(EntityUid uid, TemperatureColorComponent component, OnTemperatureChangeEvent args)
    {
        UpdateTemperatureVisuals(uid, component, args.CurrentTemperature);
    }

    private void UpdateTemperatureVisuals(EntityUid uid, TemperatureColorComponent component, float temperature)
    {
        var color = TemperatureToColor(temperature);
        var luminosity = TemperatureToLuminosity(temperature);

        component.CurrentTemperature = temperature;
        component.CurrentColor = color;
        Dirty(uid, component);

        if (TryComp<AppearanceComponent>(uid, out var appearance))
        {
            _appearance.SetData(uid, TemperatureColorVisuals.Temperature, temperature, appearance);
            _appearance.SetData(uid, TemperatureColorVisuals.Color, color, appearance);
        }

        UpdateGlow(uid, component, temperature, color, luminosity);
    }

    private void UpdateGlow(EntityUid uid, TemperatureColorComponent component,
        float temperature, Color color, float luminosity)
    {
        // Starting glow threshold (usually ~800K for metals)
        if (temperature < component.GlowThreshold)
        {
            // Turning off the glow
            if (TryComp<PointLightComponent>(uid, out var light))
            {
                _pointLight.SetEnabled(uid, false, light);
            }
            return;
        }

        var pointLight = EnsureComp<PointLightComponent>(uid);

        float intensity = CalculateGlowIntensity(temperature, luminosity, component);
        float radius = CalculateGlowRadius(temperature, component);

        // Applying glow settings
        _pointLight.SetColor(uid, color, pointLight);
        _pointLight.SetEnergy(uid, intensity, pointLight);
        _pointLight.SetRadius(uid, radius, pointLight);
        _pointLight.SetEnabled(uid, true, pointLight);

        // Soft shadows for realism
        _pointLight.SetCastShadows(uid, component.CastShadows, pointLight);
    }

    /// <summary>
    /// Calculating glow intensity based on temperature
    /// </summary>
    private float CalculateGlowIntensity(float temperature, float luminosity, TemperatureColorComponent component)
    {
        // Normalizing temperature to 0-1 range
        float normalizedTemp = (temperature - component.GlowThreshold) /
                              (component.MaxGlowTemperature - component.GlowThreshold);
        normalizedTemp = Math.Clamp(normalizedTemp, 0f, 1f);

        // Using a power function for more realistic increase
        float intensity = (float)Math.Pow(normalizedTemp, component.IntensityExponent);

        // Scaling to the desired range
        return component.MinIntensity + intensity * (component.MaxIntensity - component.MinIntensity);
    }

    /// <summary>
    /// Calculating glow radius
    /// </summary>
    private float CalculateGlowRadius(float temperature, TemperatureColorComponent component)
    {
        float normalizedTemp = (temperature - component.GlowThreshold) /
                              (component.MaxGlowTemperature - component.GlowThreshold);
        normalizedTemp = Math.Clamp(normalizedTemp, 0f, 1f);

        // Radius grows slower than intensity
        float radiusFactor = (float)Math.Pow(normalizedTemp, 0.5);

        return component.MinRadius + radiusFactor * (component.MaxRadius - component.MinRadius);
    }

    /// <summary>
    /// Returns the color of glowing metal (800–12000 K)
    /// Uses an approximation of the black body radiation law
    /// </summary>
    public static Color TemperatureToColor(float temperature)
    {
        temperature = Math.Clamp(temperature, 800f, 12000f);
        double t = temperature / 100.0;
        double r, g, b;

        // Красный канал
        if (t <= 66.0)
            r = 255.0;
        else
        {
            r = 329.698727446 * Math.Pow(t - 60.0, -0.1332047592);
            r = Math.Clamp(r, 0.0, 255.0);
        }

        // Зелёный канал
        if (t <= 66.0)
        {
            g = 99.4708025861 * Math.Log(t) - 161.1195681661;
            g = Math.Clamp(g, 0.0, 255.0);
        }
        else
        {
            g = 288.1221695283 * Math.Pow(t - 60.0, -0.0755148492);
            g = Math.Clamp(g, 0.0, 255.0);
        }

        // Синий канал
        if (t >= 66.0)
            b = 255.0;
        else if (t <= 19.0)
            b = 0.0;
        else
        {
            b = 138.5177312231 * Math.Log(t - 10.0) - 305.0447927307;
            b = Math.Clamp(b, 0.0, 255.0);
        }

        return new Color(
            (float)(r / 255.0),
            (float)(g / 255.0),
            (float)(b / 255.0)
        );
    }

    /// <summary>
    /// Metal luminosity (W/m²) — Stefan–Boltzmann law
    /// </summary>
    public static float TemperatureToLuminosity(float temperature)
    {
        const double sigma = 5.670374419e-8;
        return (float)(sigma * Math.Pow(temperature, 4));
    }

    /// <summary>
    /// Returns a descriptive color name based on temperature
    /// </summary>
    public static string GetColorName(float temperature)
    {
        return temperature switch
        {
            < 800f => "dim",
            < 1000f => "dull red",
            < 1300f => "dark red",
            < 1500f => "cherry red",
            < 1800f => "bright red",
            < 2000f => "orange red",
            < 2300f => "orange",
            < 2700f => "yellow orange",
            < 3200f => "yellow",
            < 4000f => "white yellow",
            < 5500f => "white",
            < 7000f => "light blue",
            < 10000f => "blue",
            _ => "bright blue"
        };
    }
}
