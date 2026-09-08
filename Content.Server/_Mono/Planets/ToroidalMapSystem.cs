using System.Numerics;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._FarHorizons.StarSystem;
using Content.Shared._Mono.Planets;
using Robust.Shared.Map.Components;

namespace Content.Server._Mono.Planets;

public sealed partial class ToroidalMapSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Update(float frameTime)
    {
        var maps = EntityQueryEnumerator<ToroidalMapComponent>();
        while (maps.MoveNext(out var mapUid, out var torus))
            WrapMap(mapUid, torus.Size);

        var transits = EntityQueryEnumerator<CEZTransitMapComponent>();
        while (transits.MoveNext(out var mapUid, out var transit))
        {
            if (TryGetSize(transit.LowerMap, out var size) || TryGetSize(transit.UpperMap, out size))
                WrapMap(mapUid, size);
        }

        var planetTransits = EntityQueryEnumerator<PlanetTransitMapComponent>();
        while (planetTransits.MoveNext(out var mapUid, out var transit))
        {
            if (TryGetSize(transit.OriginMap, out var size))
                WrapMap(mapUid, size);
        }
    }

    private bool TryGetSize(EntityUid? mapUid, out float size)
    {
        if (mapUid is { } uid && TryComp<ToroidalMapComponent>(uid, out var torus))
        {
            size = torus.Size;
            return true;
        }

        size = 0f;
        return false;
    }

    private void WrapMap(EntityUid mapUid, float size)
    {
        if (size <= 0f)
            return;

        var half = size / 2f;
        var query = EntityQueryEnumerator<TransformComponent>();
        while (query.MoveNext(out var uid, out var xform))
        {
            if (uid == mapUid || xform.MapUid != mapUid || xform.Anchored)
                continue;

            if (!HasComp<MapGridComponent>(uid) && xform.ParentUid != mapUid)
                continue;

            var position = xform.LocalPosition;
            var wrapped = new Vector2(Wrap(position.X, half, size), Wrap(position.Y, half, size));
            if (wrapped != position)
                _transform.SetLocalPositionNoLerp(uid, wrapped, xform);
        }
    }

    private static float Wrap(float value, float half, float size)
        => value - MathF.Floor((value + half) / size) * size;
}
