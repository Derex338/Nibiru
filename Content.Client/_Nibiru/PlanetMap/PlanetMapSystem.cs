using Content.Client._Nibiru.PlanetMap.UI;
using Content.Shared._Nibiru.PlanetMap;
using Content.Shared.Anomaly;
using Content.Shared.Anomaly.Components;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Client._Nibiru.PlanetMap;

/// <summary>
/// Client-side system that handles the planet map UI and data syncing.
/// </summary>
public sealed class PlanetMapSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SharedMapSystem _mapSys = default!;

    private PlanetMapWindow? _window;
    private NetEntity? _activeMap;
    private float _scanRequestCooldown = 0f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<PlanetMapChunkDataMessage>(OnChunkData);
        SubscribeNetworkEvent<PlanetMapOpenMessage>(OnMapOpened);

        Subs.BuiEvents<PlanetMapComponent>(
            PlanetMapUiKey.Key,
            subs => subs.Event<BoundUIOpenedEvent>(OnBuiOpened)
        );
        Subs.BuiEvents<PlanetMapComponent>(
            PlanetMapUiKey.Key,
            subs => subs.Event<BoundUIClosedEvent>(OnBuiClosed)
        );
    }

    private void OnBuiOpened(EntityUid uid, PlanetMapComponent component, BoundUIOpenedEvent args)
    {
        _activeMap = GetNetEntity(uid);

        if (_window == null)
        {
            _window = new PlanetMapWindow();
            _window.OnClose += () => 
            {
                _activeMap = null;
                _window = null;
            };
            _window.OnScanPressed += () =>
            {
                if (_activeMap != null && _scanRequestCooldown <= 0)
                {
                    RaiseNetworkEvent(new PlanetMapScanRequestMessage(_activeMap.Value));
                    _scanRequestCooldown = 3.0f; // 1 second cooldown
                }
            };
            _window.OnCenterPressed += () => _window.CenterOnPlayer();
        }

        _window.OpenCentered();
    }

    private void OnBuiClosed(EntityUid uid, PlanetMapComponent component, BoundUIClosedEvent args)
    {
        if (_activeMap == GetNetEntity(uid) && _window != null)
        {
            _window.Dispose();
            _window = null;
            _activeMap = null;
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Decrement scan request cooldown
        if (_scanRequestCooldown > 0)
            _scanRequestCooldown -= frameTime;

        if (_window == null || !_window.IsOpen)
            return;

        // Update player position on the map
        var player = _playerManager.LocalEntity;
        if (player == null)
            return;

        var xform = Transform(player.Value);
        var mapId = xform.MapID;
        var playerPos = _xform.GetWorldPosition(player.Value);

        if (mapId == MapId.Nullspace)
            return;

        if (TryComp<MapGridComponent>(xform.GridUid, out var grid))
        {
            var playerTile = _mapSys.LocalToTile(xform.GridUid.Value, grid, xform.Coordinates);
            _window.UpdatePlayerPosition(playerTile);
        }
    }




    private void OnMapOpened(PlanetMapOpenMessage msg)
    {
        if (_activeMap != msg.MapEntity || _window == null)
            return;

        _window.LoadSavedChunks(msg.SavedChunks, msg.SavedObjects, msg.ObjectPrototypes);
        _window.CenterOnPlayer();
    }

    private void OnChunkData(PlanetMapChunkDataMessage msg)
    {
        if (_activeMap != msg.MapEntity || _window == null)
            return;

        _window.MergeChunks(msg.Chunks, msg.Objects, msg.ObjectPrototypes);
    }
}
