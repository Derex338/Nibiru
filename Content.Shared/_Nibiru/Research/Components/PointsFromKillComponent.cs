using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;

namespace Content.Shared._Nibiru.Research.Components;

[RegisterComponent]
public sealed partial class PointsFromKillComponent : Component
{
    [DataField("points")]
    public int Points = 100;
}
