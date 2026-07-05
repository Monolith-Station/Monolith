
using Content.Shared._NF.Shipyard.Prototypes;
using Content.Shared.Mind;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Utility;

namespace Content.Shared._Mono.Shipyard;

public abstract class SharedShipyardPreviewSystem : EntitySystem
{
    [Dependency] private IMapManager _mapManager = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private SharedMindSystem _mind = default!;

    protected MapId _previewMap = MapId.Nullspace;
    public override void Initialize()
    {

    }

    public bool TryPreviewEntity(EntityUid player)
    {
        if (_mind.GetMind(player) is not { } mind)
            return false;

        var observer = Spawn("PreviewObserver", _xform.GetMapCoordinates(player));
        _xform.SetMapCoordinates(observer, new MapCoordinates(0, 0, _previewMap));

        _mind.Visit(mind, observer);

        return true;
    }

    public void CachePreviewMap()
    {
        if (_previewMap != MapId.Nullspace)
            return;

        var eQe = AllEntityQuery<PreviewMapComponent, MapComponent>();

        while (eQe.MoveNext(out var map, out _, out var comp))
        {
            _previewMap = comp.MapId;
        }
    }
}
