using Robust.Shared.GameStates;

namespace Content.Shared._Nibiru.NPC.Behavior;

/// <summary>
/// Стайный компонент: объединяет NPC в группы с общим целеуказанием.
/// Когда один член стаи обнаруживает врага, вся стая получает информацию о цели.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NibiruNpcPackComponent : Component
{
    /// <summary>
    /// Идентификатор стаи. NPC с одинаковым PackId действуют сообща.
    /// Генерируется при спавне группы или присоединении к ней.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public string? PackId;

    /// <summary>
    /// Максимальное расстояние связи внутри стаи.
    /// Если член стаи находится дальше, он не получает обновления от сородичей.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float PackCommunicationRange = 15f;

    /// <summary>
    /// Является ли этот NPC лидером (альфой) стаи.
    /// Убийство лидера вызывает панику или дезорганизацию в стае.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public bool IsLeader;

    /// <summary>
    /// EntityUid лидера стаи, за которым следуют члены.
    /// </summary>
    [ViewVariables]
    public EntityUid? LeaderUid;

    /// <summary>
    /// Дистанция следования за лидером.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float FollowLeaderRange = 4f;

    /// <summary>
    /// Длительность состояния паники стаи после гибели лидера (в секундах).
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float PanicDuration = 15f;

    /// <summary>
    /// Оставшееся время паники (если лидер убит).
    /// </summary>
    [ViewVariables]
    public float PanicTimer;
}
