//using Robust.Shared.Input.Binding;
//using Robust.Shared.Input;
//using static Robust.Shared.Input.Binding.PointerInputCmdHandler;
//using Robust.Shared.Timing;
//using Content.Client.Guidebook.Richtext;
//using Content.Client.Viewport;
//using Content.Client.UserInterface.Systems.Faction;
//using Robust.Client.Player;
//using Content.Shared._Nibiru.Factions;

//namespace Content.Client._Nibiru.Faction;

//public sealed class FactionSystem : EntitySystem
//{
//    [Dependency] private readonly IGameTiming _gameTiming = default!;
//    [Dependency] private readonly FactionUIController _factionUI = default!;
//    [Dependency] private readonly IPlayerManager _playerManager = default!;

//    public override void Initialize()
//    {
//        base.Initialize();

//        CommandBinds.Builder
//                .Bind(EngineKeyFunctions.Use, new PointerInputCmdHandler(OnUse, true, true))
//                .Register<FactionSystem>();
//    }

//    private bool OnUse(in PointerInputCmdArgs args)
//    {
//        if (!_gameTiming.IsFirstTimePredicted)
//            return false;

//        if (args.State == BoundKeyState.Down)
//            return OnMouseDown(args);

//        return false;
//    }

//    private bool OnMouseDown(in PointerInputCmdArgs args)
//    {
//        // Return if no player entity
//        if (_playerManager.LocalEntity is not { } playerEntity || _factionUI.FactionButton!.Pressed)
//            return false;

//        var entity = args.EntityUid;

//        // Return if can not see table or stunned/no hands
//        //if (!CanSeeTable(playerEntity, _table) || !CanDrag(playerEntity, entity, out _))
//        //{
//        //    return false;
//        //}

//        // Try to get the viewport under the cursor
//        //if (_uiManger.MouseGetControl(args.ScreenCoordinates) as ScalingViewport is not { } viewport)
//        //{
//        //    return false;
//        //}

//        RaisePredictiveEvent(new HeirChooseMessage
//        {
//            Heir = GetNetEntity(entity)
//        });
//        return true;
//    }
//}
