using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Nibiru.Lock;

[RegisterComponent, NetworkedComponent]
public partial class DoorLockComponent : Component
{	
	[DataField]
    public int LockCode = 00000;
	
	[DataField]
    public SoundSpecifier? LockSound;
	
	[DataField]
    public bool Locked = false;
	
	//[DataField]
    //public TimeSpan CrackDuration = TimeSpan.FromSeconds(60 * 5f);
	
	[DataField]
    public float CrackDuration = 120f;
}