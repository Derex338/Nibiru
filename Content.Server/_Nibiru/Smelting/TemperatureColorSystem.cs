using Content.Shared._Nibiru.Smelting;
using Content.Shared.Temperature;
using JetBrains.Annotations;
using Robust.Server.GameObjects;

namespace Content.Server._Nibiru.Smelting;

[UsedImplicitly]
public sealed class TemperatureColorSystem : EntitySystem
{
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly PointLightSystem _pointLight = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TemperatureColorComponent, OnTemperatureChangeEvent>(OnTemperatureChanged);
        SubscribeLocalEvent<TemperatureColorComponent, ComponentStartup>(OnComponentStartup);
    }

    private void OnComponentStartup(EntityUid uid, TemperatureColorComponent component, ComponentStartup args)
    {
        // Инициализируем начальное состояние
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

        // Сохраняем состояние в компоненте для синхронизации
        component.CurrentTemperature = temperature;
        component.CurrentColor = color;
        Dirty(uid, component);

        // Обновляем визуальные данные через Appearance
        if (TryComp<AppearanceComponent>(uid, out var appearance))
        {
            _appearance.SetData(uid, TemperatureColorVisuals.Temperature, temperature, appearance);
            _appearance.SetData(uid, TemperatureColorVisuals.Color, color, appearance);
        }

        // Управляем свечением
        UpdateGlow(uid, component, temperature, color, luminosity);
    }

    /// <summary>
    /// Обновляет свечение объекта на основе температуры
    /// </summary>
    private void UpdateGlow(EntityUid uid, TemperatureColorComponent component,
        float temperature, Color color, float luminosity)
    {
        // Порог начала свечения (обычно ~800K для металлов)
        if (temperature < component.GlowThreshold)
        {
            // Выключаем свечение
            if (TryComp<PointLightComponent>(uid, out var light))
            {
                _pointLight.SetEnabled(uid, false, light);
            }
            return;
        }

        // Создаём или обновляем компонент свечения
        var pointLight = EnsureComp<PointLightComponent>(uid);

        // Вычисляем интенсивность свечения
        float intensity = CalculateGlowIntensity(temperature, luminosity, component);
        float radius = CalculateGlowRadius(temperature, component);

        // Применяем настройки свечения
        _pointLight.SetColor(uid, color, pointLight);
        _pointLight.SetEnergy(uid, intensity, pointLight);
        _pointLight.SetRadius(uid, radius, pointLight);
        _pointLight.SetEnabled(uid, true, pointLight);

        // Мягкие тени для реалистичности
        _pointLight.SetCastShadows(uid, component.CastShadows, pointLight);
    }

    /// <summary>
    /// Вычисляет интенсивность свечения на основе температуры
    /// </summary>
    private float CalculateGlowIntensity(float temperature, float luminosity, TemperatureColorComponent component)
    {
        // Нормализуем температуру в диапазон 0-1
        float normalizedTemp = (temperature - component.GlowThreshold) /
                              (component.MaxGlowTemperature - component.GlowThreshold);
        normalizedTemp = Math.Clamp(normalizedTemp, 0f, 1f);

        // Используем степенную функцию для более реалистичного нарастания
        float intensity = (float)Math.Pow(normalizedTemp, component.IntensityExponent);

        // Масштабируем к желаемому диапазону
        return component.MinIntensity + intensity * (component.MaxIntensity - component.MinIntensity);
    }

    /// <summary>
    /// Вычисляет радиус свечения
    /// </summary>
    private float CalculateGlowRadius(float temperature, TemperatureColorComponent component)
    {
        float normalizedTemp = (temperature - component.GlowThreshold) /
                              (component.MaxGlowTemperature - component.GlowThreshold);
        normalizedTemp = Math.Clamp(normalizedTemp, 0f, 1f);

        // Радиус растёт медленнее интенсивности
        float radiusFactor = (float)Math.Pow(normalizedTemp, 0.5);

        return component.MinRadius + radiusFactor * (component.MaxRadius - component.MinRadius);
    }

    /// <summary>
    /// Возвращает цвет раскалённого металла (800–12000 K)
    /// Использует приближение закона излучения чёрного тела
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
    /// Светимость металла (Вт/м²) — закон Стефана–Больцмана
    /// </summary>
    public static float TemperatureToLuminosity(float temperature)
    {
        const double sigma = 5.670374419e-8;
        return (float)(sigma * Math.Pow(temperature, 4));
    }

    /// <summary>
    /// Возвращает описательное название цвета по температуре
    /// </summary>
    public static string GetColorName(float temperature)
    {
        return temperature switch
        {
            < 800f => "Тёмный",
            < 1000f => "Тускло-красный",
            < 1300f => "Тёмно-красный",
            < 1500f => "Вишнёво-красный",
            < 1800f => "Ярко-красный",
            < 2000f => "Оранжево-красный",
            < 2300f => "Оранжевый",
            < 2700f => "Жёлто-оранжевый",
            < 3200f => "Жёлтый",
            < 4000f => "Бело-жёлтый",
            < 5500f => "Белый",
            < 7000f => "Голубовато-белый",
            < 10000f => "Голубой",
            _ => "Ярко-голубой"
        };
    }
}
