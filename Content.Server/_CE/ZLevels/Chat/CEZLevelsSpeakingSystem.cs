/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System;
using System.Numerics;
using Content.Server.Chat.Systems;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Content.Shared.Chat;
using Content.Shared.IdentityManagement;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;

namespace Content.Server._CE.ZLevels.Chat;

public sealed partial class CEZLevelsSpeakingSystem : EntitySystem
{
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private CESharedZLevelsSystem _zLevel = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedMapSystem _map = default!;

    private EntityQuery<MapComponent> _mapQuery;
    private EntityQuery<MapGridComponent> _gridQuery;

    private const float TransmitterLifetime = 3f;
    private const int MessageDelayMilliseconds = 333;
    private const float SearchRadius = 3f;

    public override void Initialize()
    {
        base.Initialize();

        _mapQuery = GetEntityQuery<MapComponent>();
        _gridQuery = GetEntityQuery<MapGridComponent>();

        SubscribeLocalEvent<CEZLevelViewerComponent, EntitySpokeEvent>(OnSpoke);
    }

    private void OnSpoke(Entity<CEZLevelViewerComponent> ent, ref EntitySpokeEvent args)
    {
        var xform = Transform(ent);
        var sourceMap = xform.MapUid;
        if (sourceMap is null)
            return;

        if (args.ObfuscatedMessage is not null) //Curse of chatcode: this is only way detect whispers
            return;

        var globalPosition = _transform.GetWorldPosition(xform);
        var message = args.Message;

        //Try transmit message to 1 zlevel down (Floor of our map blocks it, so we need a hole on source map)
        if (_zLevel.TryMapDown(sourceMap.Value, out var belowMapUid) &&
            _mapQuery.TryComp(belowMapUid, out var belowMapComp))
        {
            if (TryFindNearestOpenTile(sourceMap.Value, globalPosition, SearchRadius, out var openTilePos))
            {
                TransmitMessageToZLevel(
                    belowMapComp,
                    openTilePos,
                    message,
                    Loc.GetString("ce-zlevel-voice-from-up", ("name", Identity.Name(ent, EntityManager))));
            }
        }

        //Try transmit message to 1 zlevel up (Floor of above map blocks it, so we need a hole on above map)
        if (_zLevel.TryMapUp(sourceMap.Value, out var aboveMapUid) &&
            _mapQuery.TryComp(aboveMapUid, out var aboveMapComp))
        {
            if (TryFindNearestOpenTile(aboveMapUid.Value, globalPosition, SearchRadius, out var openTilePos))
            {
                TransmitMessageToZLevel(
                    aboveMapComp,
                    openTilePos,
                    message,
                    Loc.GetString("ce-zlevel-voice-from-down", ("name", Identity.Name(ent, EntityManager))));
            }
        }
    }

    private bool TryFindNearestOpenTile(EntityUid floorMapUid, Vector2 startWorldPos, float radius, out Vector2 foundPos)
    {
        foundPos = startWorldPos;

        if (!_gridQuery.TryComp(floorMapUid, out var grid))
        {
            // If there's no grid, the space is open.
            return true;
        }

        float bestSqDist = float.MaxValue;
        bool found = false;

        float step = 0.5f;
        int steps = (int)(radius / step);

        for (int x = -steps; x <= steps; x++)
        {
            for (int y = -steps; y <= steps; y++)
            {
                var offset = new Vector2(x * step, y * step);
                var distSq = offset.LengthSquared();
                if (distSq > radius * radius)
                    continue;

                var worldPos = startWorldPos + offset;

                bool isOpen = true;
                if (_map.TryGetTileRef(floorMapUid, grid, worldPos, out var tileRef) && !tileRef.Tile.IsEmpty)
                {
                    isOpen = false;
                }

                if (isOpen && distSq < bestSqDist)
                {
                    bestSqDist = distSq;
                    foundPos = worldPos;
                    found = true;
                }
            }
        }

        return found;
    }

    private void TransmitMessageToZLevel(MapComponent mapComp, Vector2 position, string message, string nameOverride)
    {
        var targetPos = new MapCoordinates(position, mapComp.MapId);
        var transmit = Spawn(null, targetPos);
        EnsureComp<TimedDespawnComponent>(transmit).Lifetime = TransmitterLifetime;

        //It's not the most elegant solution, but as far as I understand, the entity doesn't have time to enter
        //the client's PVS after spawning, and we already start communicating through it. A slight delay solves the problem.
        Timer.Spawn(MessageDelayMilliseconds,
            () =>
            {
                _chat.TrySendInGameICMessage(
                    transmit,
                    message,
                    InGameICChatType.Whisper,
                    false,
                    nameOverride: nameOverride,
                    ignoreActionBlocker: true);
            });
    }
}
