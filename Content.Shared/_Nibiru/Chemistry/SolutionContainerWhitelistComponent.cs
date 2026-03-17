using Content.Shared.Chemistry.Reagent;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Nibiru.Chemistry;

/// <summary>
///     Prevents reagents not in the whitelist from being added to the solution container.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SolutionContainerWhitelistComponent : Component
{
    /// <summary>
    ///     Allowed reagent IDs.
    /// </summary>
    [DataField]
    public List<ProtoId<ReagentPrototype>>? Reagents;

    /// <summary>
    ///     Allowed reagent groups.
    /// </summary>
    [DataField]
    public List<string>? Groups;

    /// <summary>
    ///     Message shown when a transfer is blocked.
    /// </summary>
    [DataField]
    public LocId? WhitelistReason = "solution-container-whitelist-fail";
}
