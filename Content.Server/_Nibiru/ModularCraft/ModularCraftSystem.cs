using Content.Shared._Nibiru.ModularCraft;
using Content.Shared._Nibiru.ModularCraft.Components;
using Content.Shared._Nibiru.ModularCraft.Prototypes;
using Content.Shared.Kitchen;
using Content.Shared.Popups;
using Content.Shared.FixedPoint;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Content.Shared.Interaction;
using Robust.Shared.Utility;
using System.Linq;

namespace Content.Server._Nibiru.ModularCraft;

public sealed partial class ModularCraftSystem : EntitySystem
{
    [Dependency] private UserInterfaceSystem _ui        = default!;
    [Dependency] private SharedPopupSystem   _popup     = default!;
    [Dependency] private IPrototypeManager   _proto     = default!;
    [Dependency] private SharedAudioSystem   _audio     = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ModularCraftComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<ModularCraftComponent, ModularCraftSelectTypeMessage>(OnSelectType);
        SubscribeLocalEvent<ModularCraftComponent, ModularCraftSelectSlotMessage>(OnSelectSlot);
        SubscribeLocalEvent<ModularCraftComponent, ModularCraftDoCraftMessage>(OnCraft);
    }

    private void OnActivate(EntityUid uid, ModularCraftComponent comp, ActivateInWorldEvent args)
    {
        if (args.Handled) return;

        if (comp.CurrentItemType == null)
        {
            var first = _proto.EnumeratePrototypes<ModularItemPrototype>().FirstOrDefault();
            if (first != null) comp.CurrentItemType = first.ID;
        }

        if (!_ui.TryOpenUi(uid, ModularCraftUiKey.Key, args.User))
            return;

        var stateComp = EnsureComp<ModularCraftStateComponent>(uid);
        ApplyDefaults(stateComp, comp.CurrentItemType);
        SendState(uid);
        args.Handled = true;
    }

    private void ApplyDefaults(ModularCraftStateComponent state, string? itemTypeId)
    {
        if (itemTypeId == null ||
            !_proto.TryIndex<ModularItemPrototype>(itemTypeId, out var itemProto))
            return;

        // First material for all slots
        var defaultMat = _proto.EnumeratePrototypes<ModularMaterialPrototype>()
            .OrderBy(m => m.Quality)
            .FirstOrDefault();

        foreach (var partProtoId in itemProto.RequiredParts)
        {
            var partId = partProtoId.Id;
            if (state.SlotConfigs.TryGetValue(partId, out var existing) &&
                existing.ModuleId != null && existing.MaterialId != null)
                continue; // already filled

            var defaultMod = _proto.EnumeratePrototypes<ModularModulePrototype>()
                .Where(m => m.PartType.Id == partId &&
                            (m.CompatibleItemTypes.Count == 0 ||
                             m.CompatibleItemTypes.Any(c => c.Id == itemTypeId)))
                .OrderBy(m => m.Name)
                .FirstOrDefault();

            state.SlotConfigs[partId] = new ModularSlotConfigNet(
                defaultMod?.ID,
                defaultMat?.ID
            );
        }
    }


    private void OnSelectType(EntityUid uid, ModularCraftComponent comp, ModularCraftSelectTypeMessage msg)
    {
        comp.CurrentItemType = msg.ItemTypeId;
        var stateComp = EnsureComp<ModularCraftStateComponent>(uid);
        stateComp.SlotConfigs.Clear();
        ApplyDefaults(stateComp, msg.ItemTypeId);
        SendState(uid);
    }

    private void OnSelectSlot(EntityUid uid, ModularCraftComponent comp, ModularCraftSelectSlotMessage msg)
    {
        var stateComp = EnsureComp<ModularCraftStateComponent>(uid);
        stateComp.SlotConfigs[msg.PartId] = new ModularSlotConfigNet(msg.ModuleId, msg.MaterialId);
        SendState(uid);
    }

    private void OnCraft(EntityUid uid, ModularCraftComponent comp, ModularCraftDoCraftMessage msg)
    {
        if (comp.CurrentItemType == null || !_proto.TryIndex<ModularItemPrototype>(comp.CurrentItemType.Value, out var itemProto))
            return;

        var stateComp = EnsureComp<ModularCraftStateComponent>(uid);

        if (!CanCraft(itemProto, stateComp))
        {
            _popup.PopupEntity(Loc.GetString("weapon-smithy-cant-craft"), uid);
            return;
        }

        var baseEnt = itemProto.BaseEntity;
        if (string.IsNullOrEmpty(baseEnt) && comp.BaseEntityPrototype != null)
            baseEnt = comp.BaseEntityPrototype;

        var crafted = Spawn(baseEnt, Transform(uid).Coordinates);
        var modular = EnsureComp<ModularItemComponent>(crafted);

        foreach (var kvp in stateComp.SlotConfigs)
        {
            modular.SlotConfigs[new ProtoId<ModularPartPrototype>(kvp.Key)] = new ModularSlotConfig(
                (ProtoId<ModularModulePrototype>?)kvp.Value.ModuleId,
                (ProtoId<ModularMaterialPrototype>?)kvp.Value.MaterialId
            );
        }

        BakeStats(modular);

        stateComp.SlotConfigs.Clear();
        SendState(uid);

        _popup.PopupEntity(Loc.GetString("weapon-smithy-crafted"), uid);
    }

    private void SendState(EntityUid uid)
    {
        var comp = Comp<ModularCraftComponent>(uid);
        var stateComp = EnsureComp<ModularCraftStateComponent>(uid);

        var state = new ModularCraftBUIState(comp.CurrentItemType, stateComp.SlotConfigs);
        _ui.SetUiState(uid, ModularCraftUiKey.Key, state);
    }

    private bool CanCraft(ModularItemPrototype itemProto, ModularCraftStateComponent state)
    {
        foreach (var req in itemProto.RequiredParts)
        {
            if (!state.SlotConfigs.TryGetValue(req.Id, out var cfg)) return false;
            if (cfg.ModuleId == null || cfg.MaterialId == null) return false;
        }
        return true;
    }

    private void BakeStats(ModularItemComponent comp)
    {
        var damage = FixedPoint2.Zero;
        var reach = FixedPoint2.Zero;
        var pen = FixedPoint2.Zero;
        var weight = FixedPoint2.Zero;
        var speed = FixedPoint2.New(1f);

        foreach (var (_, cfg) in comp.SlotConfigs)
        {
            if (cfg.ModuleId == null || cfg.MaterialId == null) continue;

            if (!_proto.TryIndex(cfg.ModuleId.Value, out var module)) continue;
            if (!_proto.TryIndex(cfg.MaterialId.Value, out var mat))  continue;

            var dmgMult = mat.DamageMultiplier;
            damage  += (module.DamageBonus  + FixedPoint2.New(2f)) * dmgMult;
            reach   += module.ReachBonus;
            pen     += (module.PenetrationBonus) * mat.PenetrationMultiplier;
            weight  += module.Weight * mat.WeightMultiplier;
            speed   *= module.AttackSpeedMultiplier;
        }

        comp.TotalDamage      = FixedPoint2.Max(damage, FixedPoint2.New(1f));
        comp.TotalReach       = reach;
        comp.TotalPenetration = pen;
        comp.TotalWeight      = weight;
        comp.AttackSpeed      = speed;
    }
}

// Temporary component to hold active crafting state
[RegisterComponent]
public sealed partial class ModularCraftStateComponent : Component
{
    public Dictionary<string, ModularSlotConfigNet> SlotConfigs = new();
}
