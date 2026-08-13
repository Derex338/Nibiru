namespace Content.Client._CE.Localization;

[RegisterComponent]
[Access(typeof(CELocalizationVisualsSystem))]
public sealed partial class CELocalizationVisualsComponent : Component
{
    [DataField(required: true)]
    public Dictionary<string, Dictionary<string, string>> MapStates = new();
}
