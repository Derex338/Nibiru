using Content.Client._Nibiru.PlanetMap.UI;
using Content.Shared._Nibiru.PlanetMap;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Client._Nibiru.PlanetMap;

/// <summary>
/// Client-side system handling planet map UI and progressive chunk loading.
///
/// Key optimisations vs the original:
/// • Received chunk batches are queued and applied progressively in Update()
///   (ClientBatchSize chunks per frame) to avoid freezing the client.
/// • PlanetMapOpenMessage is now a signal-only packet; actual data arrives
///   as one or more PlanetMapChunkBatchMessage packets.
/// </summary>
public sealed class PlanetMapSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager     _playerManager = default!;
    [Dependency] private readonly SharedTransformSystem _xform      = default!;
    [Dependency] private readonly SharedMapSystem    _mapSys        = default!;

    private PlanetMapWindow? _window;
    private NetEntity?       _activeMap;
    private float            _scanRequestCooldown;

    /// <summary>
    /// Maximum number of chunks to apply from the pending queue per frame.
    /// Lower values = smoother loading but slower total time.
    /// At 30 chunks/frame × 60 fps → 1000 chunks loads in ~0.55 s with no perceptible stutter.
    /// </summary>
    private const int ClientBatchSize = 30;

    // Queue of chunk batches waiting to be merged into the map control
    private readonly Queue<PendingBatch> _pendingBatches = new();

    private sealed class PendingBatch
    {
        public readonly Dictionary<Vector2i, uint[]> Chunks;
        public readonly Dictionary<Vector2i, uint[]> Objects;
        public readonly List<string>                 ObjectPrototypes;

        public PendingBatch(
            Dictionary<Vector2i, uint[]> chunks,
            Dictionary<Vector2i, uint[]> objects,
            List<string>                 protos)
        {
            Chunks           = chunks;
            Objects          = objects;
            ObjectPrototypes = protos;
        }
    }

    // -----------------------------------------------------------------------
    // Lifecycle
    // -----------------------------------------------------------------------

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<PlanetMapOpenMessage>(OnMapOpened);
        SubscribeNetworkEvent<PlanetMapChunkBatchMessage>(OnChunkBatch);

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
                _window    = null;
                _pendingBatches.Clear();
            };
            _window.OnScanPressed += () =>
            {
                if (_activeMap != null && _scanRequestCooldown <= 0)
                {
                    RaiseNetworkEvent(new PlanetMapScanRequestMessage(_activeMap.Value));
                    _scanRequestCooldown = 3.0f;
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
            _window    = null;
            _activeMap = null;
            _pendingBatches.Clear();
        }
    }

    // -----------------------------------------------------------------------
    // Update: player position + progressive chunk application
    // -----------------------------------------------------------------------

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_scanRequestCooldown > 0)
            _scanRequestCooldown -= frameTime;

        if (_window == null || !_window.IsOpen)
        {
            if (_pendingBatches.Count > 0)
                _pendingBatches.Clear();
            return;
        }

        // Sync player tile position every frame
        var player = _playerManager.LocalEntity;
        if (player != null)
        {
            var xform = Transform(player.Value);
            if (xform.MapID != MapId.Nullspace && TryComp<MapGridComponent>(xform.GridUid, out var grid))
            {
                var playerTile = _mapSys.LocalToTile(xform.GridUid!.Value, grid, xform.Coordinates);
                _window.UpdatePlayerPosition(playerTile);
            }
        }

        // Apply pending chunk batches — at most ClientBatchSize chunks per frame
        var appliedChunks = 0;
        while (_pendingBatches.Count > 0 && appliedChunks < ClientBatchSize)
        {
            var batch = _pendingBatches.Dequeue();
            _window.MergeChunks(batch.Chunks, batch.Objects, batch.ObjectPrototypes);
            // Count chunks to keep the budget meaningful; treat empty batch as 1 to avoid spin
            appliedChunks += Math.Max(1, batch.Chunks.Count);
        }
    }

    // -----------------------------------------------------------------------
    // Network events
    // -----------------------------------------------------------------------

    /// <summary>
    /// Received when the server signals "start of map data stream".
    /// Clear local data immediately; batches will follow.
    /// </summary>
    private void OnMapOpened(PlanetMapOpenMessage msg)
    {
        if (_activeMap != msg.MapEntity || _window == null)
            return;

        // Cancel any still-pending batches from a previous open
        _pendingBatches.Clear();

        // Clear the visual map immediately so the player sees the map is being refreshed
        _window.ClearChunks();
        _window.CenterOnPlayer();
    }

    /// <summary>
    /// Received for every chunk batch (initial load OR scan result).
    /// Enqueued for progressive application in Update().
    /// </summary>
    private void OnChunkBatch(PlanetMapChunkBatchMessage msg)
    {
        if (_activeMap != msg.MapEntity || _window == null)
            return;

        _pendingBatches.Enqueue(new PendingBatch(msg.Chunks, msg.Objects, msg.ObjectPrototypes));
    }
}
