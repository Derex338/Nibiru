using Content.Shared.Audio;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Nibiru.NPC.Behavior.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NibiruNpcAudioComponent : Component
{
    [DataField, AutoNetworkedField]
    public SoundSpecifier? AggroSound;

    [DataField, AutoNetworkedField]
    public SoundSpecifier? AttackSound;

    [DataField, AutoNetworkedField]
    public SoundSpecifier? HurtSound;

    [DataField, AutoNetworkedField]
    public SoundSpecifier? DeathSound;

    [DataField, AutoNetworkedField]
    public SoundSpecifier? AmbientSound;

    [DataField, ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public float AmbientSoundInterval = 15f;

    [ViewVariables, AutoNetworkedField]
    public float AmbientSoundAccumulator;

    [DataField, AutoNetworkedField]
    public SoundSpecifier? FleeSound;
}
