namespace Content.Shared._Nibiru.Research.Components;

[RegisterComponent]
public sealed partial class PointsFromDestructionComponent : Component
{
    [DataField("points")]
    public int Points = 100;
}
