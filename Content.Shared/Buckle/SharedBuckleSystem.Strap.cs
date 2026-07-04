using System.Linq;
using Content.Shared.Buckle.Components;
using Content.Shared.Construction;
using Content.Shared.Destructible;
using Content.Shared.Foldable;
using Content.Shared.Storage;
using Content.Shared.Stunnable;
using Robust.Shared.Containers;

namespace Content.Shared.Buckle;

public abstract partial class SharedBuckleSystem
{
    private void InitializeStrap()
    {
        SubscribeLocalEvent<StrapComponent, ComponentStartup>(OnStrapStartup);
        SubscribeLocalEvent<StrapComponent, ComponentShutdown>(OnStrapShutdown);
        SubscribeLocalEvent<StrapComponent, ComponentRemove>((e, c, _) => StrapRemoveAll(e, c));
        SubscribeLocalEvent<StrapComponent, EntityTerminatingEvent>(OnStrapTerminating);

        SubscribeLocalEvent<StrapComponent, ContainerGettingInsertedAttemptEvent>(OnStrapContainerGettingInsertedAttempt);
        SubscribeLocalEvent<StrapComponent, DestructionEventArgs>((e, c, _) => StrapRemoveAll(e, c));
        SubscribeLocalEvent<StrapComponent, BreakageEventArgs>((e, c, _) => StrapRemoveAll(e, c));

        SubscribeLocalEvent<StrapComponent, FoldAttemptEvent>(OnAttemptFold);
        SubscribeLocalEvent<StrapComponent, MachineDeconstructedEvent>((e, c, _) => StrapRemoveAll(e, c));
    }

    private void OnStrapStartup(EntityUid uid, StrapComponent component, ComponentStartup args)
    {
        Appearance.SetData(uid, StrapVisuals.State, component.BuckledEntities.Count != 0);
    }

    private void OnStrapShutdown(EntityUid uid, StrapComponent component, ComponentShutdown args)
    {
        if (!TerminatingOrDeleted(uid))
            StrapRemoveAll(uid, component);
    }

    /// <summary>
    /// Handle the case when a strap entity is being terminated.
    /// This ensures buckled entities are properly unbuckled before the strap is deleted.
    /// </summary>
    private void OnStrapTerminating(EntityUid uid, StrapComponent component, ref EntityTerminatingEvent args)
    {
        StrapRemoveAll(uid, component);
    }

    private void OnStrapContainerGettingInsertedAttempt(EntityUid uid, StrapComponent component, ContainerGettingInsertedAttemptEvent args)
    {
        // If someone is attempting to put this item inside of a backpack, ensure that it has no entities strapped to it.
        if (args.Container.ID == StorageComponent.ContainerId && component.BuckledEntities.Count != 0)
            args.Cancel();
    }

    private void OnAttemptFold(EntityUid uid, StrapComponent component, ref FoldAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        args.Cancelled = component.BuckledEntities.Count != 0;
    }

    /// <summary>
    /// Remove everything attached to the strap
    /// </summary>
    private void StrapRemoveAll(EntityUid uid, StrapComponent strapComp)
    {
        // Making a copy since we'll be modifying the collection while iterating
        var buckledEntities = strapComp.BuckledEntities.ToArray();

        foreach (var entity in buckledEntities)
        {
            // If this strap is being terminated, we need special handling to ensure
            // entities are safely moved before it's fully deleted
            if (Terminating(uid))
            {
                if (TryComp<BuckleComponent>(entity, out var buckleComp) &&
                    buckleComp.BuckledTo == uid &&
                    !Terminating(entity))
                {
                    // Manually set the buckle status without trying to reposition
                    SetBuckledTo((entity, buckleComp), null);

                    // Manually place the entity next to the strap if possible
                    var entityXform = Transform(entity);
                    var strapXform = Transform(uid);

                    if (entityXform.ParentUid == uid)
                    {
                        // Try to place the entity nearby if we can
                        if (strapXform.ParentUid.IsValid() && !Terminating(strapXform.ParentUid))
                        {
                            var worldPos = _transform.GetWorldPosition(uid);
                            _transform.SetWorldPosition(entity, worldPos);
                            _transform.SetParent(entity, uid);
                            // entityXform.AttachParent(strapXform.ParentUid);
                        }

                        // Reset other states
                        _rotationVisuals.ResetHorizontalAngle(entity);
                        Appearance.SetData(entity, BuckleVisuals.Buckled, false);

                        if (HasComp<KnockedDownComponent>(entity) || _mobState.IsIncapacitated(entity))
                            _standing.Down(entity, playSound: false);
                        else
                            _standing.Stand(entity);

                        _joints.RefreshRelay(entity);

                        // Raise events to inform other systems
                        var buckleEv = new UnbuckledEvent((uid, strapComp), (entity, buckleComp));
                        RaiseLocalEvent(entity, ref buckleEv);

                        var strapEv = new UnstrappedEvent((uid, strapComp), (entity, buckleComp));
                        RaiseLocalEvent(uid, ref strapEv);
                    }
                }
            }
            else
            {
                Unbuckle(entity, entity);
            }
        }
    }

    private bool StrapHasSpace(EntityUid strapUid, BuckleComponent buckleComp, StrapComponent? strapComp = null)
    {
        if (!Resolve(strapUid, ref strapComp, false))
            return false;

        var avail = strapComp.Size;
        foreach (var buckle in strapComp.BuckledEntities)
        {
            avail -= CompOrNull<BuckleComponent>(buckle)?.Size ?? 0;
        }

        return avail >= buckleComp.Size;
    }

    /// <summary>
    /// Sets the enabled field in the strap component to a value
    /// </summary>
    public void StrapSetEnabled(EntityUid strapUid, bool enabled, StrapComponent? strapComp = null)
    {
        if (!Resolve(strapUid, ref strapComp, false) ||
            strapComp.Enabled == enabled)
            return;

        strapComp.Enabled = enabled;
        Dirty(strapUid, strapComp);

        if (!enabled)
            StrapRemoveAll(strapUid, strapComp);
    }
}
