using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Nibiru.NPC.Commands;

/// <summary>
/// Временный компонент на животном: ждёт, пока хозяин не поднесёт предмет для обнюхивания.
/// Добавляется при команде Search и удаляется, когда:
///  — игрок использовал предмет на животном (InteractUsing), или
///  — истёк таймаут.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class NibiruAnimalSearchWaitingComponent : Component
{
    /// <summary>
    /// Хозяин, ожидающий результата обнюхивания.
    /// </summary>
    [DataField]
    public EntityUid Commander;

    /// <summary>
    /// Сколько секунд животное ожидает предмет (таймаут).
    /// </summary>
    [DataField]
    public float Timeout = 8f;

    /// <summary>
    /// Накопитель времени.
    /// </summary>
    [ViewVariables]
    public float Accumulator;
}
