using Robust.Shared.GameStates;

namespace Content.Shared._Nibiru.NPC.Behavior.Components;

/// <summary>
/// Компонент боевого стиля НПС-животного.
/// Определяет какую тактику использует животное в бою.
/// Специфические параметры каждого стиля хранятся в отдельных компонентах:
/// - Default: без дополнительных компонентов
/// - HitAndLeap: <see cref="NibiruNpcHitAndRunAttackComponent"/>
/// - Charge: <see cref="NibiruNpcChargeAttackComponent"/>
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NibiruNpcCombatComponent : Component
{
    /// <summary>
    /// Боевой стиль данного животного.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public NibiruCombatStyle CombatStyle = NibiruCombatStyle.Default;

    // ── Параметры стиля Default ───────────────────────────────────────────

    /// <summary>
    /// Расстояние на которое животное отходит назад после удара (Default-стиль).
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public float PostAttackRetreatDistance = 1.5f;

    /// <summary>
    /// Длительность паузы после отступа перед следующей атакой (Default-стиль).
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public float PostAttackCooldown = 0.8f;

    // ── Runtime-состояние (общее) ─────────────────────────────────────────

    /// <summary>
    /// Таймер кулдауна после атаки (используется в Default-стиле для паузы отступа).
    /// </summary>
    [ViewVariables]
    public float RetreatTimer;

    /// <summary>
    /// True если сейчас выполняется фаза отступа (Default-стиль).
    /// </summary>
    [ViewVariables]
    public bool IsRetreating;
}
