using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Timing;
using System.Collections.Generic;
using System;

namespace Content.Server.FTL;

/// <summary>
/// This system applies crushing damage to entities that fall into FTL maps without being on a grid
/// after a short delay
/// </summary>
public sealed partial class FTLDamageSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageableSystem = default!;
    [Dependency] private IGameTiming _timing = default!;

    // Dictionary to track entities that are in FTL space without a grid and their timers
    private readonly Dictionary<EntityUid, TimeSpan> _pendingCrushes = new();
    
    // Time delay before applying crush damage (2.5 seconds)
    private const float CrushDelay = 2.5f;

    public override void Initialize()
    {
        base.Initialize();

        // Subscribe to the event that's raised when an entity's map changes
        SubscribeLocalEvent<TransformComponent, EntParentChangedMessage>(OnEntParentChanged);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        
        // Current time
        var curTime = _timing.CurTime;
        var toRemove = new List<EntityUid>();
        
        // Create a copy of the entries to safely iterate over
        var pendingCopy = new Dictionary<EntityUid, TimeSpan>(_pendingCrushes);
        
        // Check all pending entities
        foreach (var (entity, crushTime) in pendingCopy)
        {
            // Skip if the entity is deleted or queued for deletion
            if (EntityManager.Deleted(entity) || EntityManager.IsQueuedForDeletion(entity))
            {
                toRemove.Add(entity);
                continue;
            }
            
            // Check if transform component still exists
            if (!TryComp<TransformComponent>(entity, out var transform))
            {
                toRemove.Add(entity);
                continue;
            }
            
            // If the entity is now on a grid or no longer in FTL space, remove it from pending
            if (!transform.MapUid.HasValue || 
                !HasComp<FTLMapComponent>(transform.MapUid.Value) || 
                transform.GridUid.HasValue)
            {
                toRemove.Add(entity);
                continue;
            }
            
            // Check if it's time to apply crush damage
            if (curTime >= crushTime)
            {
                ApplyCrushDamage(entity);
                toRemove.Add(entity);
            }
        }
        
        // Remove processed entities
        foreach (var entity in toRemove)
        {
            _pendingCrushes.Remove(entity);
        }
    }

    private void OnEntParentChanged(EntityUid uid, TransformComponent transform, ref EntParentChangedMessage args)
    {
        // Skip if the entity is deleted or queued for deletion
        if (EntityManager.Deleted(uid) || EntityManager.IsQueuedForDeletion(uid))
            return;
            
        if (!transform.MapUid.HasValue)
            return;

        var mapUid = transform.MapUid.Value;

        // Check if the entity has moved to an FTL map
        if (HasComp<FTLMapComponent>(mapUid))
        {
            // Only schedule damage if the entity is not on a valid grid
            if (!transform.GridUid.HasValue)
            {
                // Schedule crush damage after delay
                _pendingCrushes[uid] = _timing.CurTime + TimeSpan.FromSeconds(CrushDelay);
            }
            else if (_pendingCrushes.ContainsKey(uid))
            {
                // Entity is now on a grid, remove from pending
                _pendingCrushes.Remove(uid);
            }
        }
        else if (_pendingCrushes.ContainsKey(uid))
        {
            // Entity is no longer in FTL space, remove from pending
            _pendingCrushes.Remove(uid);
        }
    }

    private void ApplyCrushDamage(EntityUid uid)
    {
        // Skip the damage if the entity doesn't have a damageable component
        if (!HasComp<DamageableComponent>(uid))
            return;

        // Create damage specification for 1000 blunt damage
        var damage = new DamageSpecifier();
        damage.DamageDict.Add("Blunt", FixedPoint2.New(1000));

        // Apply the damage to the entity
        _damageableSystem.TryChangeDamage(uid, damage, true);
    }
}
