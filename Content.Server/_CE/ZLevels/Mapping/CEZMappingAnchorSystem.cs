/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Server._CE.ZLevels.Core;
using Content.Shared._CE.ZLevels.Core.Components;
using Robust.Shared.Map.Components;

namespace Content.Server._CE.ZLevels.Mapping;

/// <summary>
/// Maintains the grid-level <see cref="CEZMappingAnchorGridComponent"/> tag from the mapping
/// anchor entities on each grid. The z-level fall gate and grid sync read that tag to hold the
/// grid's network aloft and lock it in place; keeping the tag in sync here means removing the
/// last anchor from a grid drops the lift and the static lock, and re-runs the connector
/// recalc so any network it held together re-derives.
/// </summary>
public sealed partial class CEZMappingAnchorSystem : EntitySystem
{
    [Dependency] private CEZGridConnectorSystem _connectors = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEZMappingAnchorComponent, MapInitEvent>(OnAnchorMapInit);
        SubscribeLocalEvent<CEZMappingAnchorComponent, EntParentChangedMessage>(OnAnchorParentChanged);
        SubscribeLocalEvent<CEZMappingAnchorComponent, ComponentShutdown>(OnAnchorShutdown);
    }

    private void OnAnchorMapInit(Entity<CEZMappingAnchorComponent> ent, ref MapInitEvent args)
    {
        RefreshGridTag(Transform(ent).GridUid);
    }

    private void OnAnchorParentChanged(Entity<CEZMappingAnchorComponent> ent, ref EntParentChangedMessage args)
    {
        // Old grid may have lost its last anchor; new grid may have gained its first.
        RefreshGridTag(args.OldParent);
        RefreshGridTag(Transform(ent).GridUid);
    }

    private void OnAnchorShutdown(Entity<CEZMappingAnchorComponent> ent, ref ComponentShutdown args)
    {
        // The anchor is still enumerable during shutdown, so exclude it from the recount.
        RefreshGridTag(Transform(ent).GridUid, ignore: ent.Owner);
    }

    /// <summary>
    /// Ensures a grid carries the tag iff it still hosts at least one live mapping anchor,
    /// and pokes the connector recalc so the static/lift change takes effect promptly.
    /// </summary>
    private void RefreshGridTag(EntityUid? gridUid, EntityUid? ignore = null)
    {
        if (gridUid is not { } grid || !HasComp<MapGridComponent>(grid))
            return;

        var hasAnchor = false;
        var children = Transform(grid).ChildEnumerator;
        while (children.MoveNext(out var child))
        {
            if (child != ignore && HasComp<CEZMappingAnchorComponent>(child))
            {
                hasAnchor = true;
                break;
            }
        }

        if (hasAnchor == HasComp<CEZMappingAnchorGridComponent>(grid))
            return;

        if (hasAnchor)
            EnsureComp<CEZMappingAnchorGridComponent>(grid);
        else
            RemComp<CEZMappingAnchorGridComponent>(grid);

        // The network's support/static state just changed; recompute it.
        _connectors.MarkDirty();
    }
}
