using Content.Server.Research.Systems;
using Content.Shared.Research.Components;
using Content.Shared.Research.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server._Nibiru.Research;

[RegisterComponent]
public sealed partial class DebugUnlockAllTechnologiesComponent : Component
{
}

public sealed class DebugUnlockAllTechnologiesSystem : EntitySystem
{
    [Dependency] private readonly ResearchSystem _research = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DebugUnlockAllTechnologiesComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(EntityUid uid, DebugUnlockAllTechnologiesComponent component, ComponentStartup args)
    {
        if (!TryComp<TechnologyDatabaseComponent>(uid, out var db))
            return;

        foreach (var proto in _prototypeManager.EnumeratePrototypes<TechnologyPrototype>())
        {
            _research.AddTechnology(uid, proto, db);
        }
    }
}
