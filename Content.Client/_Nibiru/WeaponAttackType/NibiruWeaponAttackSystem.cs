using Content.Client.Gameplay;
using Content.Client.Nibiru.WeaponAttackType.UI;
using Content.Shared._Nibiru.WeaponAttackType;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Input;
using Content.Shared.Weapons.Melee;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Client.State;
using Robust.Client.UserInterface;
using Robust.Shared.Input;
using Robust.Shared.Timing;

namespace Content.Client.Nibiru.WeaponAttackType;

public sealed class NibiruWeaponAttackSystem : SharedNibiruWeaponAttackSystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IStateManager _stateManager = default!;
    [Dependency] private readonly IUserInterfaceManager _ui = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;

    private AttackTypeGrid? _grid;
    private bool _keyWasDown;
    private int _lastIndex = -1;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NibiruWeaponAttackComponent, HandSelectedEvent>(OnWeaponSelected);
        SubscribeLocalEvent<NibiruWeaponAttackComponent, HandDeselectedEvent>(OnWeaponDeselected);
    }

    private void OnWeaponSelected(EntityUid uid, NibiruWeaponAttackComponent component, HandSelectedEvent args)
    {
        EnsureGrid();
        _grid?.Show(uid, component.AvailableAttacks, component.CurrentAttackIndex);
        _lastIndex = component.CurrentAttackIndex;
        ApplyAnimPredict(uid, component);
    }

    private void OnWeaponDeselected(EntityUid uid, NibiruWeaponAttackComponent component, HandDeselectedEvent args)
    {
        _grid?.Hide();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_timing.IsFirstTimePredicted)
            return;

        var entity = _player.LocalEntity;
        if (entity == null) return;

        // Edge detection для Z
        var inputSys = EntityManager.System<InputSystem>();
        var keyDown = inputSys.CmdStates.GetState(ContentKeyFunctions.CycleAttackType) == BoundKeyState.Down;

        if (keyDown && !_keyWasDown)
        {
            if (_hands.TryGetActiveItem(entity.Value, out var held) &&
                TryComp<NibiruWeaponAttackComponent>(held.Value, out var attackComp) &&
                attackComp.Cycleable && attackComp.AvailableAttacks.Count > 1)
            {
                // Меняем индекс сразу на клиенте предсказательно
                attackComp.CurrentAttackIndex = (attackComp.CurrentAttackIndex + 1) % attackComp.AvailableAttacks.Count;
                _grid?.UpdateHighlight(attackComp.CurrentAttackIndex);
                ApplyAnimPredict(held.Value, attackComp);

                RaisePredictiveEvent(new CycleAttackTypeMessage(GetNetEntity(held.Value)));
            }
        }
        _keyWasDown = keyDown;

        // Синхронизация с серверным стейтом — если сервер подтвердил, локальный индекс уже правильный
        if (_hands.TryGetActiveItem(entity.Value, out var heldCheck) &&
            TryComp<NibiruWeaponAttackComponent>(heldCheck.Value, out var comp))
        {
            if (_lastIndex != comp.CurrentAttackIndex)
            {
                _lastIndex = comp.CurrentAttackIndex;
                _grid?.UpdateHighlight(comp.CurrentAttackIndex);
                ApplyAnimPredict(heldCheck.Value, comp);
            }
        }
    }

    // Меняем анимацию прямо на MeleeWeaponComponent — синкается само
    private void ApplyAnimPredict(EntityUid uid, NibiruWeaponAttackComponent component)
    {
        if (!TryComp<MeleeWeaponComponent>(uid, out var melee))
            return;

        if (!TryGetCurrentAttackType(component, out var proto))
            return;

        if (!string.IsNullOrEmpty(proto.Animation))
        {
            melee.Animation = proto.Animation;
            melee.WideAnimation = proto.Animation;
        }

        if (proto.AngleOverride.HasValue)
            melee.Angle = proto.AngleOverride.Value;
    }

    private void EnsureGrid()
    {
        if (_grid != null) return;

        if (_stateManager.CurrentState is GameplayStateBase or GameplayState)
        {
            _grid = new AttackTypeGrid();
            _grid.OnAttackTypeSelected += _ =>
            {
                if (_player.LocalEntity is { } ent &&
                    _hands.TryGetActiveItem(ent, out var held))
                {
                    RaisePredictiveEvent(new CycleAttackTypeMessage(GetNetEntity(held.Value)));
                }
            };

            if (_ui.RootControl is { } root)
            {
                root.AddChild(_grid);
            }
        }
    }
}
