using Content.Shared.Eui;
using Robust.Shared.GameStates;
using Robust.Shared.Maths;
using Robust.Shared.Serialization;
using System.Numerics;

namespace Content.Shared._Nibiru.Factions;

/// <summary>
/// Захваченный sprite-слой персонажа для вечного отображения на статуе.
/// </summary>
[Serializable, NetSerializable, DataDefinition]
public partial struct CapturedSpriteLayer
{
    [DataField]
    public string RsiPath;

    [DataField]
    public string State;

    [DataField]
    public Color? Color;

    [DataField]
    public Vector2? Offset;

    [DataField]
    public bool Visible = true;
}

/// <summary>
/// Компонент статуи члена фракции.
/// Хранит запечённый спрайт выбранного члена + список всех членов для выбора.
/// </summary>
[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class FactionStatueComponent : Component
{
    /// <summary>
    /// Название фракции, к которой относится статуя.
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public string FactionName = string.Empty;

    /// <summary>
    /// Все члены фракции, доступные для выбора (на момент постройки).
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public List<FactionMemberRecord> AllMembers { get; set; } = new();

    /// <summary>
    /// Выбранный член фракции.
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public NetEntity? SelectedMember { get; set; }

    /// <summary>
    /// Имя выбранного члена.
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public string SelectedMemberName = string.Empty;

    /// <summary>
    /// Спрайт выбранного члена, запечённый навсегда.
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public List<CapturedSpriteLayer> CapturedLayers { get; set; } = new();

    /// <summary>
    /// Entity строителя (сервер-сайд).
    /// </summary>
    [DataField]
    public EntityUid? Builder;
}

/// <summary>
/// Состояние EUI для выбора члена фракции при постройке статуи.
/// </summary>
[Serializable, NetSerializable]
public sealed class FactionStatueSelectionState : EuiStateBase
{
    public NetEntity StatueEntity;
    public string FactionName = string.Empty;
    public List<FactionMemberRecord> AllMembers = new();
}

/// <summary>
/// Сообщение от клиента с выбранным членом фракции для статуи.
/// </summary>
[Serializable, NetSerializable]
public sealed class FactionStatueSelectMessage : EuiMessageBase
{
    public NetEntity SelectedMember;
}
