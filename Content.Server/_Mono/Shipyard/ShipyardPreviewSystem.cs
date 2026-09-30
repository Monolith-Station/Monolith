using Content.Server.GameTicking;
using Content.Server.Mind;
using Content.Server.Mind.Toolshed;
using Content.Shared._Mono.Shipyard;
using Content.Shared._NF.Shipyard.Components;
using Content.Shared._NF.Shipyard.Events;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Robust.Server.GameObjects;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Utility;

using static Content.Shared._Mono.Shipyard.SharedPreview;

namespace Content.Server._Mono.Shipyard;

/// <summary>
/// This handles preview map and preview observer.
/// </summary>
public sealed class ShipyardPreviewSystem : SharedShipyardPreviewSystem
{
    [Dependency] private MapSystem _map = default!;
    [Dependency] private MetaDataSystem _meta = default!;
    [Dependency] private TransformSystem _xform = default!;
    [Dependency] private MindSystem _mind = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PostGameMapLoad>(OnPostGameMapLoad);
        SubscribeLocalEvent<ShipyardConsoleComponent, ShipyardConsolePreviewMessage>(OnPreview);
        SubscribeLocalEvent<ShipyardPreviewExitMessage>(OnDispose);
    }

    private void OnPostGameMapLoad(PostGameMapLoad ev)
    {
        EnsureMap();
    }

    private void OnPreview(Entity<ShipyardConsoleComponent> ev, ref ShipyardConsolePreviewMessage evMsg)
    {
        CachePreviewMap();

        TryPreviewEntity(evMsg.Actor);
    }

    private void EnsureMap()
    {
        var mapUid = _map.CreateMap();
        var pMap = EnsureComp<PreviewMapComponent>(mapUid);

        _meta.SetEntityName(mapUid, "Shuttle preview map.");

        Dirty(mapUid, pMap);
    }

    private void OnDispose(ShipyardPreviewExitMessage ev)
    {
        var observer = ev.Actor;

        // Make sure entity sending this message is ACTUALLY PreviewObserver
        if (MetaData(observer).EntityPrototype?.ID != "PreviewObserver")
            return;

        if (!TryComp<VisitingMindComponent>(observer, out var mindComp) || !mindComp.MindId.HasValue)
            return;

        _mind.UnVisit(mindComp.MindId.Value);

        QueueDel(observer);
    }
}
