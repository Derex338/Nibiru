using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Nibiru.Key;

[RegisterComponent, NetworkedComponent]
public partial class KeyComponent : Component
{
	[DataField]
    public int LockCode = 00000;
}