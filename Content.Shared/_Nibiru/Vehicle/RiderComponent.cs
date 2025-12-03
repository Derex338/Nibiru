using Robust.Shared.GameStates;

namespace Content.Shared._Nibiru.Vehicle;

/// <summary>
/// Компонент для сущностей, которые в данный момент едут верхом
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RiderComponent : Component
{
    /// <summary>
    /// Транспорт, на котором едет всадник
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public EntityUid Mount;
}
