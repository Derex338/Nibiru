using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;

namespace Content.Shared._Nibiru.Chemistry;

public sealed partial class SolutionContainerWhitelistSystem : EntitySystem
{
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SolutionContainerWhitelistComponent, SolutionTransferAttemptEvent>(OnTransferAttempt);
    }

    private void OnTransferAttempt(Entity<SolutionContainerWhitelistComponent> ent, ref SolutionTransferAttemptEvent args)
    {
        // We only care about transfers TO this entity
        if (args.To != ent.Owner)
            return;

        // Get the source solution
        if (!_solution.TryGetDrainableSolution(args.From, out _, out var sourceSol))
            return;

        foreach (var (reagent, _) in sourceSol.Contents)
        {
            if (!IsAllowed(ent.Comp, reagent.Prototype))
            {
                var reason = ent.Comp.WhitelistReason ?? "solution-container-whitelist-fail";
                args.Cancel(Loc.GetString(reason, ("owner", ent.Owner), ("reagent", reagent.Prototype)));
                return;
            }
        }
    }

    public bool IsAllowed(SolutionContainerWhitelistComponent comp, string reagentId)
    {
        if (comp.Reagents != null && comp.Reagents.Contains(reagentId))
            return true;

        if (comp.Groups != null && _prototype.TryIndex<ReagentPrototype>(reagentId, out var proto))
        {
            if (comp.Groups.Contains(proto.Group))
                return true;
        }

        return false;
    }
}
