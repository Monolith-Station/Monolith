using System.Numerics;
using Content.Server.Chat.Managers;
using Content.Shared.Chat;
using Content.Shared.Localizations;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._Mono.Shipyard;

/// <summary>
/// A system that tells players which direction their newly purchased ship is located
/// </summary>
public sealed class ShipyardDirectionSystem : EntitySystem
{
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly TransformSystem _transformSystem = default!;

    /// <summary>
    /// Sends a message to the player indicating the compass direction of their newly purchased ship
    /// </summary>
    public void SendShipDirectionMessage(EntityUid player, EntityUid ship)
    {
        if (!TryComp(player, out TransformComponent? playerTransform) ||
            !TryComp(ship, out TransformComponent? shipTransform))
            return;

        TryComp(ship, out MetaDataComponent? metaData);

        // Make sure both entities are on the same map
        if (playerTransform.MapID != shipTransform.MapID)
            return;

        var gridOffset = Angle.Zero;
        var grid = playerTransform.ParentUid;

        if (HasComp<MapGridComponent>(grid))
            gridOffset = Transform(grid).LocalRotation;

        // get the world position of ship and player
        var playerPos = _transformSystem.GetWorldPosition(player);
        var shipPos = _transformSystem.GetWorldPosition(ship);

        // get the angle between the two positions, adjusted for the grid rotation so that
        // we properly preserve north in relation to the grid.
        var direction = playerPos - shipPos;
        var directionAngle = direction.ToWorldAngle();
        var adjustedDir = (directionAngle - gridOffset).GetDir();
        var length = (playerPos - shipPos).LengthSquared();

        // Skip if they're at the same position (very unlikely but just in case)
        if (length < 0.01f)
            return;

        // Get compass direction
        var directionName = ContentLocalizationManager.FormatDirection(adjustedDir).ToLower(); // lua localization
        var distance = Math.Round(direction.Length(), 1);
        var shipName = metaData?.EntityName ?? "ship";

        // Send message to player
        var message = Loc.GetString("shipyard-direction-message",
            ("shipName", shipName),
            ("direction", directionName),
            ("distance", distance));

        if (_playerManager.TryGetSessionByEntity(player, out var session))
        {
            _chatManager.ChatMessageToOne(ChatChannel.Server, message, message, EntityUid.Invalid, false, session.Channel);
        }
    }

    //lua start
    ///// <summary>
    ///// Converts a direction vector to a compass direction
    ///// </summary>
    //private string GetCompassDirection(Vector2 direction)
    //{
    //    var angle = new Angle(direction);
    //    var dir = angle.GetDir();

    //    return dir switch
    //    {
    //        Direction.North => "North",
    //        Direction.NorthEast => "North East",
    //        Direction.East => "East",
    //        Direction.SouthEast => "South East",
    //        Direction.South => "South",
    //        Direction.SouthWest => "South West",
    //        Direction.West => "West",
    //        Direction.NorthWest => "North West",
    //        _ => "Unknown"
    //    };
    //}
    //lua end
}
