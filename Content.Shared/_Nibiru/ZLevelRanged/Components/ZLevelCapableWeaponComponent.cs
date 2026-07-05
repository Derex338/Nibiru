using Robust.Shared.GameStates;

namespace Content.Shared._Nibiru.ZLevelRanged.Components;

/// <summary>
/// Помечает оружие как способное стрелять между Z-уровнями.
/// Снаряды этого оружия получат ZLevelProjectileComponent.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ZLevelCapableWeaponComponent : Component
{
    /// <summary>
    /// Может ли это оружие стрелять прямой наводкой между уровнями
    /// (стрела летит по прямой и может попасть на другой уровень через отверстие)
    /// </summary>
    [DataField]
    public bool AllowDirectFire = true;

    /// <summary>
    /// Процент пути на котором снаряд проверяет падение вниз
    /// </summary>
    [DataField]
    public float FallCheckDistance = 0.7f;
}
