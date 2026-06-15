using System.Numerics;
using Robust.Shared.Maths;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;


namespace Content.Server._Nibiru.SaveLoad;

/// <summary>
/// Temporary component used during save/load to preserve parent map information for entities saved outside of map files.
/// </summary>
[RegisterComponent]
public sealed partial class NibiruSaveParentComponent : Component
{
    [DataField("mapId")]
    public int MapId;

    [DataField("position")]
    public Vector2 Position;

    [DataField("rotation")]
    public Angle Rotation;
}
