using System.Numerics;

namespace Content.Server.Parallax;

[RegisterComponent]
public sealed partial class BiomeMobComponent : Component
{
    [DataField]
    public Vector2 SpawnPosition;
}
