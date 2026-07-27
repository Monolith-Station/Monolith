using System.Linq;
using Content.Server.Popups;
using Content.Shared._NF.Storage; /// Forge-Change
using Content.Shared.Construction.Components;
using Content.Shared.Mind.Components;
using Content.Shared.Nyanotrasen.Item.PseudoItem;
using Content.Shared.Storage;
using JetBrains.Annotations;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Map.Components;

namespace Content.Server._NF.Storage;

/// <summary>
/// This is used for restricting anchor operations on storage (one bag max per tile)
/// and ejecting living contents on anchor.
/// </summary>
public sealed partial class AnchorableStorageSystem : EntitySystem
{
    [Dependency] private MapSystem _map = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private TransformSystem _xform = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!; /// Forge-Change

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();                                                                      /// Forge-Change

        SubscribeLocalEvent<AnchorableStorageComponent, ComponentStartup>(OnComponentStartup);  /// Forge-Change
        SubscribeLocalEvent<AnchorableStorageComponent, AnchorStateChangedEvent>(OnAnchorStateChanged);
        SubscribeLocalEvent<AnchorableStorageComponent, AnchorAttemptEvent>(OnAnchorAttempt);
        SubscribeLocalEvent<AnchorableStorageComponent, ContainerIsInsertingAttemptEvent>(OnInsertAttempt);
    }
    /// Forge-Change-Start
    private void OnComponentStartup(Entity<AnchorableStorageComponent> ent, ref ComponentStartup args)
    {
        SetAnchoredVisuals(ent.Owner, Transform(ent.Owner).Anchored);
    }
    /// Forge-Change-End

    private void OnAnchorStateChanged(Entity<AnchorableStorageComponent> ent, ref AnchorStateChangedEvent args)
    {
        if (!args.Anchored)
            return;

        if (CheckOverlap((ent, ent.Comp, Transform(ent))))
        {
            _popup.PopupEntity(Loc.GetString("anchored-storage-already-present"), ent);
            _xform.Unanchor(ent, Transform(ent));
            return;
        }

        /// Forge-Change-Start
        SetAnchoredVisuals(ent.Owner, args.Anchored);

        if (!args.Anchored)
            return;
        /// Forge-Change-End
        // Eject any sapient creatures inside the storage.
        // Does not recurse down into bags in bags - player characters are the largest concern, and they'll only fit in duffelbags.
        // if (!TryComp(ent.Owner, out StorageComponent? storage))              /// Forge-Change-Del
        //     return;                                                          /// Forge-Change-Del

        // var entsToRemove = storage.StoredItems.Keys.Where(storedItem =>      /// Forge-Change-Del
        //         HasComp<MindContainerComponent>(storedItem)                  /// Forge-Change-Del
        //         || HasComp<PseudoItemComponent>(storedItem)                  /// Forge-Change-Del
        //     ).ToList();                                                      /// Forge-Change-Del

        // foreach (var removeUid in entsToRemove)                              /// Forge-Change-Del
        //     _container.RemoveEntity(ent.Owner, removeUid);                   /// Forge-Change-Del
    }

    /// Forge-Change-Start
    private void SetAnchoredVisuals(EntityUid uid, bool anchored)
    {
        _appearance.SetData(uid, AnchorableStorageVisuals.Anchored, anchored);
    }

    /// Forge-Change-End
    private void OnAnchorAttempt(Entity<AnchorableStorageComponent> ent, ref AnchorAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        // Nothing around? We can anchor without issue.
        if (!CheckOverlap((ent, ent.Comp, Transform(ent))))
            return;

        _popup.PopupEntity(Loc.GetString("anchored-storage-already-present"), ent, args.User);
        args.Cancel();
    }

    private void OnInsertAttempt(Entity<AnchorableStorageComponent> ent, ref ContainerIsInsertingAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        // Check for living things, they should not insert when anchored.
        // if (!HasComp<MindContainerComponent>(args.EntityUid) && !HasComp<PseudoItemComponent>(args.EntityUid))   /// Forge-Change-Del
        //     return;                                                                                              /// Forge-Change-Del

        // if (Transform(ent.Owner).Anchored)                                                                       /// Forge-Change-Del
        //     args.Cancel();                                                                                       /// Forge-Change-Del
    }

    [PublicAPI]
    public bool CheckOverlap(EntityUid uid)
    {
        if (!TryComp(uid, out AnchorableStorageComponent? comp))
            return false;

        return CheckOverlap((uid, comp, Transform(uid)));
    }

    public bool CheckOverlap(Entity<AnchorableStorageComponent, TransformComponent> ent)
    {
        if (ent.Comp2.GridUid is not { } grid || !TryComp<MapGridComponent>(grid, out var gridComp))
            return false;

        var indices = _map.TileIndicesFor(grid, gridComp, ent.Comp2.Coordinates);
        var enumerator = _map.GetAnchoredEntitiesEnumerator(grid, gridComp, indices);

        while (enumerator.MoveNext(out var otherEnt))
        {
            // Don't match yourself.
            if (otherEnt == ent.Owner)
                continue;

            // Is another storage entity is already anchored here?
            if (HasComp<AnchorableStorageComponent>(otherEnt))
                return true;
        }

        return false;
    }
}
