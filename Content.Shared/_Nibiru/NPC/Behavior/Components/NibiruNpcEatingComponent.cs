using Robust.Shared.GameStates;

namespace Content.Shared._Nibiru.NPC.Behavior.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NibiruNpcEatingComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public List<string> EdibleTiles = new() { "Grass", "Jungle" };
}
