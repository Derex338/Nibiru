using Content.Shared._Nibiru.NPC.Livestock;
using Content.Shared._Nibiru.NPC.Behavior;
using Content.Shared._Nibiru.NPC.Training;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Server._Nibiru.NPC.Systems.Utility;
using Content.Shared.Tag;
using Content.Shared.Tools.Systems;
using Content.Shared.Verbs;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server._Nibiru.NPC.Systems.Livestock;

/// <summary>
/// Processes Verbs for interaction with animals:
/// - Resource gathering (shearing/milking) via click + DoAfter
/// - Information display (mood, trust, resource growth)
/// </summary>
public sealed partial class NibiruLivestockInteractionSystem : EntitySystem
{
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private NibiruLivestockSystem _livestock = default!;
    [Dependency] private NibiruAnimalSoundSystem _sounds = default!;
    [Dependency] private TagSystem _tag = default!;
    [Dependency] private SharedToolSystem _tool = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NibiruLivestockComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<NibiruLivestockComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<NibiruLivestockComponent, LivestockHarvestDoAfterEvent>(OnHarvestDoAfter);
        SubscribeLocalEvent<NibiruLivestockComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<NibiruAnimalProductsComponent, InteractUsingEvent>(OnProductsInteractUsing);
        SubscribeLocalEvent<NibiruAnimalProductsComponent, InteractHandEvent>(OnProductsInteractHand);
        SubscribeLocalEvent<NibiruAnimalProductsComponent, LivestockHarvestDoAfterEvent>(OnProductsHarvestDoAfter);
        SubscribeLocalEvent<NibiruAnimalProductsComponent, ExaminedEvent>(OnProductsExamined);
    }

    private void OnProductsInteractUsing(EntityUid uid, NibiruAnimalProductsComponent component, InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (TryStartHarvest(uid, args.User, args.Used))
            args.Handled = true;
    }

    private void OnProductsInteractHand(EntityUid uid, NibiruAnimalProductsComponent component, InteractHandEvent args)
    {
        TryHandleBareHandHarvest(uid, args);
    }

    private void OnProductsHarvestDoAfter(EntityUid uid, NibiruAnimalProductsComponent component, LivestockHarvestDoAfterEvent args)
    {
        FinishHarvest(uid, args);
    }

    private void OnProductsExamined(EntityUid uid, NibiruAnimalProductsComponent component, ExaminedEvent args)
    {
        PushExamineInfo(uid, args);
    }

    /// <summary>
    /// Harvest with a tool (shears for shearing, etc.).
    /// </summary>
    private void OnInteractUsing(EntityUid uid, NibiruLivestockComponent component, InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (TryStartHarvest(uid, args.User, args.Used))
            args.Handled = true;
    }

    /// <summary>
    /// Harvest with bare hands (milking, etc., if no tool is required).
    /// </summary>
    private void OnInteractHand(EntityUid uid, NibiruLivestockComponent component, InteractHandEvent args)
    {
        TryHandleBareHandHarvest(uid, args);
    }

    private void TryHandleBareHandHarvest(EntityUid uid, InteractHandEvent args)
    {
        if (args.Handled)
            return;

        var resources = _livestock.GetResources(uid);
        if (resources == null)
            return;

        for (var i = 0; i < resources.Count; i++)
        {
            var resource = resources[i];
            if (!resource.ReadyToHarvest || !string.IsNullOrEmpty(resource.RequiredTool))
                continue;

            StartHarvestDoAfter(uid, args.User, i);
            args.Handled = true;
            return;
        }
    }

    /// <summary>
    /// Tries to start resource gathering, checking for the required tool.
    /// </summary>
    private bool TryStartHarvest(EntityUid animal, EntityUid user, EntityUid tool)
    {
        if (!TryComp(tool, out MetaDataComponent? toolMeta) || toolMeta.EntityPrototype == null)
            return false;

        var toolProtoId = toolMeta.EntityPrototype.ID;

        var resources = _livestock.GetResources(animal);
        if (resources == null)
            return false;

        for (var i = 0; i < resources.Count; i++)
        {
            var resource = resources[i];
            if (!resource.ReadyToHarvest)
                continue;

            if (string.IsNullOrEmpty(resource.RequiredTool))
                continue;

            // Check tool by prototype ID or tag
            if (toolProtoId.Contains(resource.RequiredTool)
                || (_proto.HasIndex<TagPrototype>(resource.RequiredTool) && _tag.HasTag(tool, resource.RequiredTool))
                || _tool.HasQuality(tool, resource.RequiredTool))
            {
                StartHarvestDoAfter(animal, user, i);
                return true;
            }
        }

        return false;
    }

    private void StartHarvestDoAfter(EntityUid animal, EntityUid user, int resourceIndex)
    {
        var ev = new LivestockHarvestDoAfterEvent(resourceIndex);
        var doAfterArgs = new DoAfterArgs(EntityManager, user, TimeSpan.FromSeconds(3), ev, animal, target: animal)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true
        };

        _doAfter.TryStartDoAfter(doAfterArgs);
    }

    private void OnHarvestDoAfter(EntityUid uid, NibiruLivestockComponent component, LivestockHarvestDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        FinishHarvest(uid, args);
    }

    private void FinishHarvest(EntityUid uid, LivestockHarvestDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        if (_livestock.TryHarvestResource(uid, args.User, args.ResourceIndex))
        {
            // Play harvest sound based on resource type
            var resources = _livestock.GetResources(uid);
            if (resources != null && args.ResourceIndex < resources.Count)
            {
                var resource = resources[args.ResourceIndex];
                if (resource.ItemPrototype.Contains("Wool") || resource.ItemPrototype.Contains("Fur"))
                    _sounds.PlayShearingSound(uid);
                else if (resource.ItemPrototype.Contains("Milk"))
                    _sounds.PlayMilkingSound(uid);
            }

            args.Handled = true;
        }
    }

    private void OnExamined(EntityUid uid, NibiruLivestockComponent component, ExaminedEvent args)
    {
        PushExamineInfo(uid, args);
    }

    private void PushExamineInfo(EntityUid uid, ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        string groupName;
        if (TryComp<NibiruLivestockComponent>(uid, out _))
            groupName = nameof(NibiruLivestockComponent);
        else if (TryComp<NibiruAnimalProductsComponent>(uid, out _))
            groupName = nameof(NibiruAnimalProductsComponent);
        else
            groupName = "NibiruLivestock";

        using (args.PushGroup(groupName))
        {
            // Show resource information
            var resources = _livestock.GetResources(uid);
            if (resources != null)
            {
                for (var i = 0; i < resources.Count; i++)
                {
                    var resource = resources[i];
                    var growthPercent = MathF.Min(100f, resource.GrowthAccumulator / resource.GrowthTime * 100f);

                    if (resource.ReadyToHarvest)
                    {
                        args.PushMarkup(Loc.GetString("nibiru-livestock-resource-ready",
                            ("resource", resource.ItemPrototype),
                            ("yield", resource.Yield)));
                    }
                    else
                    {
                        args.PushMarkup(Loc.GetString("nibiru-livestock-resource-growing",
                            ("resource", resource.ItemPrototype),
                            ("percent", growthPercent.ToString("F0"))));
                    }
                }
            }

            if (TryComp<NibiruAnimalPregnancyComponent>(uid, out var pregnancy))
            {
                var gestationPercent = pregnancy.GestationAccumulator / pregnancy.GestationTime * 100f;
                args.PushMarkup(Loc.GetString("nibiru-livestock-pregnant",
                    ("percent", gestationPercent.ToString("F0"))));
            }

            if (TryComp<NibiruAnimalBreederComponent>(uid, out var breeder) && breeder.Enabled)
            {
                args.PushMarkup(Loc.GetString("nibiru-livestock-sex",
                    ("sex", _livestock.GetSex(uid).ToString())));
            }
            else if (TryComp<NibiruLivestockComponent>(uid, out var component) && component.CanBreed)
            {
                if (component.IsPregnant)
                {
                    var gestationPercent = component.GestationAccumulator / component.GestationTime * 100f;
                    args.PushMarkup(Loc.GetString("nibiru-livestock-pregnant",
                        ("percent", gestationPercent.ToString("F0"))));
                }

                args.PushMarkup(Loc.GetString("nibiru-livestock-sex",
                    ("sex", component.Sex.ToString())));
            }

            // Tamability information, if available
            if (TryComp<NibiruTamableComponent>(uid, out var tamable))
            {
                if (tamable.IsTamed)
                {
                    var trustPercent = tamable.TrustLevel / tamable.MaxTrust * 100f;
                    args.PushMarkup(Loc.GetString("nibiru-animal-tamed",
                        ("trust", trustPercent.ToString("F0"))));
                }
                else
                {
                    args.PushMarkup(Loc.GetString("nibiru-animal-wild"));
                }
            }

            // Mood information
            if (TryComp<NibiruAnimalMoodComponent>(uid, out var mood))
            {
                args.PushMarkup(Loc.GetString("nibiru-animal-mood",
                    ("mood", mood.MoodState.ToString())));
            }

            // Mount fear information
            if (TryComp<NibiruMountFearComponent>(uid, out var fear))
            {
                var fearPercent = fear.FearLevel / fear.MaxFear * 100f;
                var trainingPercent = fear.StressTraining / fear.MaxStressTraining * 100f;
                args.PushMarkup(Loc.GetString("nibiru-mount-fear",
                    ("fear", fearPercent.ToString("F0")),
                    ("training", trainingPercent.ToString("F0"))));
            }
        }
    }
}
