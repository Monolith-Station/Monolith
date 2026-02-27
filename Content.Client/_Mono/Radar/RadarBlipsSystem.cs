using System.Numerics;
using Content.Shared._Mono.Radar;
using Robust.Shared.Map;
using Robust.Shared.Timing;
using System.Linq;

namespace Content.Client._Mono.Radar;

public sealed partial class RadarBlipsSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IMapManager _map = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    private const double BlipStaleSeconds = 3.0;
    private static readonly List<(Vector2 Start, Vector2 End, float Thickness, Color Color)> EmptyHitscanList = new();
    private TimeSpan _lastRequestTime = TimeSpan.Zero;
    private static readonly TimeSpan RequestThrottle = TimeSpan.FromMilliseconds(250);

    private TimeSpan _lastUpdatedTime;
    private List<BlipNetData> _blips = new();
    private List<(Vector2 Start, Vector2 End, float Thickness, Color Color)> _hitscans = new();

    // cached results to avoid allocating on every draw/frame
    private readonly List<BlipData> _cachedBlipData = new();
    private readonly List<(Vector2 Start, Vector2 End, float Thickness, Color Color)> _cachedHitscanData = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<GiveBlipsEvent>(HandleReceiveBlips);
        SubscribeNetworkEvent<BlipRemovalEvent>(RemoveBlip);
    }

    private void HandleReceiveBlips(GiveBlipsEvent ev, EntitySessionEventArgs args)
    {
        if (ev?.Blips == null)
        {
            _blips.Clear();
        }
        else
        {
            _blips = ev.Blips;
        }

        if (ev?.HitscanLines == null)
        {
            _hitscans = EmptyHitscanList;
        }
        else
        {
            _hitscans = ev.HitscanLines;
        }

        _lastUpdatedTime = _timing.CurTime;
    }

    private void RemoveBlip(BlipRemovalEvent args)
    {
        var blipid = _blips.FirstOrDefault(x => x.Uid == args.NetBlipUid);
        _blips.Remove(blipid);
    }

    public void RequestBlips(EntityUid console)
    {
        // Only request if we have a valid console
        if (!Exists(console))
            return;

        // Add request throttling to avoid network spam
        if (_timing.CurTime - _lastRequestTime < RequestThrottle)
            return;

        _lastRequestTime = _timing.CurTime;

        var netConsole = GetNetEntity(console);
        var ev = new RequestBlipsEvent(netConsole);
        RaiseNetworkEvent(ev);
    }

    /// <summary>
    /// Gets the current blips as world positions with their scale, color and shape.
    /// </summary>
    public List<BlipData> GetCurrentBlips()
    {
        // clear the cache and bail early if the data is stale
        _cachedBlipData.Clear();
        if (_timing.CurTime.TotalSeconds - _lastUpdatedTime.TotalSeconds > BlipStaleSeconds)
            return _cachedBlipData;

        // populate the cached list instead of allocating a new one each frame
        foreach (var blip in _blips)
        {
            var coord = GetCoordinates(blip.Position);

            if (!coord.IsValid(EntityManager))
                continue;

            var predictedPos = new EntityCoordinates(coord.EntityId, coord.Position + blip.Vel * (float)(_timing.CurTime - _lastUpdatedTime).TotalSeconds);

            var predictedMap = _xform.ToMapCoordinates(predictedPos);

            var config = blip.Config;
            // hijack our shape if we're on a grid and we want to do that
            if (_map.TryFindGridAt(predictedMap, out var grid, out _) && grid != EntityUid.Invalid && blip.OnGridConfig != null)
                config = blip.OnGridConfig.Value;
            var maybeGrid = grid != EntityUid.Invalid ? grid : (EntityUid?)null;

            _cachedBlipData.Add(new(blip.Uid, predictedPos, blip.Rotation, maybeGrid, config));
        }

        return _cachedBlipData;
    }

    /// <summary>
    /// Gets the hitscan lines to be rendered on the radar
    /// </summary>
    public List<(Vector2 Start, Vector2 End, float Thickness, Color Color)> GetHitscanLines()
    {
        _cachedHitscanData.Clear();
        if (_timing.CurTime.TotalSeconds - _lastUpdatedTime.TotalSeconds > BlipStaleSeconds)
            return _cachedHitscanData;

        foreach (var hitscan in _hitscans)
        {
            _cachedHitscanData.Add((hitscan.Start, hitscan.End, hitscan.Thickness, hitscan.Color));
        }

        return _cachedHitscanData;
    }
}

public record struct BlipData
(
    NetEntity NetUid,
    EntityCoordinates Position,
    Angle Rotation,
    EntityUid? GridUid,
    BlipConfig Config
);
