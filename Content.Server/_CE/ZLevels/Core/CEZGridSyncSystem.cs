/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Numerics;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Controllers;
using Robust.Shared.Physics.Events;

namespace Content.Server._CE.ZLevels.Core;

//WARNING: This file is vibecoded. It WORKS, but i dunno how that works - and we need investigate that and rewrite to more propriate code human style.

/// <summary>
/// Synchronizes all grids in a z-network as a single rigid body around one stable anchor grid.
///
/// Geometry model: each network designates one <see cref="CEZGridNetworkComponent.AnchorGrid"/>
/// (static planet if present, otherwise the lowest <see cref="EntityUid"/> for determinism). Every
/// other grid stores its pose as <see cref="CEZGridComponent.NetworkOffset"/>/
/// <see cref="CEZGridComponent.NetworkRotation"/> relative to that anchor. The anchor is the single
/// source of truth — non-anchor grids are placed from anchor+offset each substep, eliminating drift
/// without averaging.
///
/// Membership changes are incremental: a newly linked grid snaps to the anchor (once), survivors are
/// never re-snapped, and when the anchor leaves the remaining grids are rebased onto a new anchor
/// in place (no movement).
/// </summary>
public sealed partial class CEZGridSyncSystem : VirtualController
{
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private CEZLevelsSystem _zlevels = default!;
    [Dependency] private CEZGridConnectorSystem _connectorSystem = default!;

    [Dependency] private EntityQuery<CEZGridComponent> _gridCompQuery = default!;
    [Dependency] private EntityQuery<PhysicsComponent> _physicsQuery = default!;
    [Dependency] private EntityQuery<MapGridComponent> _mapGridQuery = default!;
    [Dependency] private EntityQuery<MapComponent> _mapCompQuery = default!;

    private bool _inPhysicsTick;
    private bool _syncing;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEZGridComponent, CEGridAddedIntoZNetworkEvent>(OnGridLinked);
        SubscribeLocalEvent<CEZGridComponent, CEGridRemovedFromZNetworkEvent>(OnGridUnlinked);

        SubscribeLocalEvent<CEZGridComponent, MoveEvent>(OnGridMoved);
        SubscribeLocalEvent<CEZGridComponent, MassDataChangedEvent>(OnMassChanged);
    }

    private void OnGridLinked(Entity<CEZGridComponent> zGridEnt, ref CEGridAddedIntoZNetworkEvent ev)
    {
        zGridEnt.Comp.CachedMass = _physicsQuery.TryComp(zGridEnt.Owner, out var body)
            ? body.FixturesMass
            : 0f;

        if (ev.Network.Comp.Grids.Count == 1)
        {
            // First member becomes the anchor.
            zGridEnt.Comp.NetworkOffset = Vector2.Zero;
            zGridEnt.Comp.NetworkRotation = Angle.Zero;
            ev.Network.Comp.AnchorGrid = zGridEnt.Owner;
        }
        else if (IsStaticAnchor(zGridEnt.Owner) && ev.Network.Comp.AnchorGrid != zGridEnt.Owner)
        {
            //TODO: Connection grid to map?
        }
        else
        {
            // Make sure an anchor exists (recovery if it was invalidated), then snap only the new grid.
            EnsureAnchor(ev.Network.Comp);
            if (zGridEnt.Owner != ev.Network.Comp.AnchorGrid)
                SnapNewGridToAnchor(ev.Network.Comp, zGridEnt);
        }

        RecalculateNetworkCache(ev.Network);
    }

    private void OnGridUnlinked(Entity<CEZGridComponent> ent, ref CEGridRemovedFromZNetworkEvent ev)
    {
        ent.Comp.NetworkOffset = Vector2.Zero;
        ent.Comp.NetworkRotation = Angle.Zero;
        ent.Comp.CachedMass = 0f;

        // If the departing grid was the anchor, rebase the survivors onto a new one (in place, no move).
        if (ev.Network.Comp.AnchorGrid == ent.Owner)
        {
            ev.Network.Comp.AnchorGrid = EntityUid.Invalid;
            var newAnchor = ChooseAnchor(ev.Network.Comp);
            if (newAnchor.IsValid())
                RebaseToAnchor(ev.Network.Comp, newAnchor);
        }

        RecalculateNetworkCache(ev.Network);
    }

    private void OnGridMoved(Entity<CEZGridComponent> ent, ref MoveEvent ev)
    {
        if (_syncing || _inPhysicsTick)
            return;

        if ((ev.NewPosition.Position - ev.OldPosition.Position).LengthSquared() < 1e-9f
            && Math.Abs((ev.NewRotation - ev.OldRotation).Theta) < 1e-6)
            return;

        if (!_zlevels.TryGetGridNetwork(ent.Owner, out var network) || network.Comp.Grids.Count < 2)
            return;

        EnsureAnchor(network.Comp);

        if (network.Comp.HasStaticAnchor)
        {
            // Locked to a planet: any member that is shoved gets pinned back to anchor+offset.
            if (!network.Comp.AnchorGrid.IsValid())
                return;
            var (sPos, sRot) = GetGridPose(network.Comp.AnchorGrid);
            _syncing = true;
            ApplyAnchorToGrid(ent.Owner, ent.Comp, sPos, sRot);
            _syncing = false;
            return;
        }

        // Flying network: the moved grid drives, the rest follow its derived anchor frame.
        var (anchorPos, anchorRot) = GetAnchorPoseFromGrid(ent.Owner);
        _syncing = true;
        foreach (var other in network.Comp.Grids)
        {
            if (other == ent.Owner || !_gridCompQuery.TryComp(other, out var otherComp))
                continue;
            ApplyAnchorToGrid(other, otherComp, anchorPos, anchorRot);
        }

        _syncing = false;
    }

    private void OnMassChanged(Entity<CEZGridComponent> ent, ref MassDataChangedEvent args)
    {
        ent.Comp.CachedMass = _physicsQuery.TryComp(ent.Owner, out var body)
            ? body.FixturesMass
            : 0f;

        if (_zlevels.TryGetGridNetwork(ent.Owner, out var network))
            RecalculateNetworkCache(network);
    }

    // === Predicates / math helpers ===

    private bool IsStaticAnchor(EntityUid gridUid)
    {
        return _mapCompQuery.HasComponent(gridUid)
               || _physicsQuery.TryComp(gridUid, out var body)
               && !IsMoveable(body);
    }

    private static bool IsMoveable(PhysicsComponent body)
    {
        return (body.BodyType & (BodyType.Dynamic | BodyType.KinematicController)) != 0x0;
    }

    private static Vector2 SnapPosition(Vector2 v, float tileSize)
    {
        return new Vector2(MathF.Round(v.X / tileSize) * tileSize,
            MathF.Round(v.Y / tileSize) * tileSize);
    }

    private static Angle SnapAngle(Angle a)
    {
        return Angle.FromDegrees(Math.Round(a.Degrees / 90.0) * 90.0);
    }

    private (Vector2 Pos, Angle Rot) GetGridPose(EntityUid grid)
    {
        return (_transform.GetWorldPosition(grid), _transform.GetWorldRotation(grid));
    }

    // Derives the virtual network anchor pose from one grid's world transform + its stored offset.
    // With consistent offsets every grid yields (almost) the same anchor; averaging removes the drift.
    private (Vector2 Pos, Angle Rot) GetAnchorPoseFromGrid(EntityUid grid)
    {
        var (pos, rot) = GetGridPose(grid);
        if (!_gridCompQuery.TryComp(grid, out var comp))
            return (pos, rot);

        var anchorRot = rot - comp.NetworkRotation;
        return (pos - anchorRot.RotateVec(comp.NetworkOffset), anchorRot);
    }

    private void ApplyAnchorToGrid(EntityUid gridUid, CEZGridComponent comp, Vector2 anchorPos, Angle anchorRot)
    {
        _transform.SetWorldPositionRotation(gridUid,
            anchorPos + anchorRot.RotateVec(comp.NetworkOffset),
            anchorRot + comp.NetworkRotation);
    }

    // === Anchor selection / cache ===

    /// <summary>Deterministic anchor: a static anchor takes priority, otherwise the lowest EntityUid.</summary>
    private EntityUid ChooseAnchor(CEZGridNetworkComponent net)
    {
        var best = EntityUid.Invalid;
        var bestStatic = false;

        foreach (var g in net.Grids)
        {
            var isStatic = IsStaticAnchor(g);
            if (!best.IsValid()
                || (isStatic && !bestStatic)
                || (isStatic == bestStatic && g.Id < best.Id))
            {
                best = g;
                bestStatic = isStatic;
            }
        }

        return best;
    }

    private bool HasValidAnchor(CEZGridNetworkComponent net)
    {
        return net.AnchorGrid.IsValid() && net.Grids.Contains(net.AnchorGrid);
    }

    /// <summary>Ensures the network has a valid anchor, rebasing every member onto it (in place) if it changed.</summary>
    private void EnsureAnchor(CEZGridNetworkComponent net)
    {
        if (HasValidAnchor(net))
            return;

        var anchor = ChooseAnchor(net);
        if (anchor.IsValid())
            RebaseToAnchor(net, anchor);
    }

    /// <summary>
    /// Re-expresses every grid's stored pose relative to <paramref name="newAnchor"/>'s current world
    /// transform. Pure bookkeeping — nothing is moved, so survivors keep their exact world poses.
    /// </summary>
    private void RebaseToAnchor(CEZGridNetworkComponent net, EntityUid newAnchor)
    {
        net.AnchorGrid = newAnchor;
        var (anchorPos, anchorRot) = GetGridPose(newAnchor);

        foreach (var g in net.Grids)
        {
            if (!_gridCompQuery.TryComp(g, out var comp))
                continue;

            if (g == newAnchor)
            {
                comp.NetworkOffset = Vector2.Zero;
                comp.NetworkRotation = Angle.Zero;
                continue;
            }

            var (gPos, gRot) = GetGridPose(g);
            comp.NetworkOffset = new Angle(-anchorRot.Theta).RotateVec(gPos - anchorPos);
            comp.NetworkRotation = gRot - anchorRot;
        }
    }

    private void RecalculateNetworkCache(Entity<CEZGridNetworkComponent> network)
    {
        var totalMass = 0f;
        var hasStatic = false;

        foreach (var g in network.Comp.Grids)
        {
            if (_gridCompQuery.TryComp(g, out var comp))
                totalMass += comp.CachedMass;
            if (IsStaticAnchor(g))
                hasStatic = true;
        }

        network.Comp.TotalCachedMass = totalMass;
        network.Comp.HasStaticAnchor = hasStatic;
    }

    /// <summary>Computes and (optionally) snaps a freshly linked grid's pose relative to the current anchor.</summary>
    private void SnapNewGridToAnchor(CEZGridNetworkComponent net, Entity<CEZGridComponent> grid)
    {
        var (anchorPos, anchorRot) = GetGridPose(net.AnchorGrid);
        var (world, worldRot) = GetGridPose(grid.Owner);

        var localOffset = new Angle(-anchorRot.Theta).RotateVec(world - anchorPos);
        var relRot = worldRot - anchorRot;

        // Static grids are never moved — record their exact pose.
        if (IsStaticAnchor(grid.Owner) || !_mapGridQuery.TryComp(grid.Owner, out var mapGrid))
        {
            grid.Comp.NetworkOffset = localOffset;
            grid.Comp.NetworkRotation = relRot;
            return;
        }

        var tileSize = mapGrid.TileSize;
        var snappedOffset = SnapPosition(localOffset, tileSize);
        var snappedRot = SnapAngle(relRot);

        // Always snap onto a static-anchored network; for flying merges only snap a small correction.
        var shouldSnap = net.HasStaticAnchor
                         || (snappedOffset - localOffset).Length() < tileSize
                         && Math.Abs((snappedRot - relRot).Theta) < Math.PI * 0.25;

        if (shouldSnap)
        {
            grid.Comp.NetworkOffset = snappedOffset;
            grid.Comp.NetworkRotation = snappedRot;
            _syncing = true;
            ApplyAnchorToGrid(grid.Owner, grid.Comp, anchorPos, anchorRot);
            _syncing = false;
            _connectorSystem.MarkDirty();
        }
        else
        {
            grid.Comp.NetworkOffset = localOffset;
            grid.Comp.NetworkRotation = relRot;
        }
    }

    // === Physics ===

    // Velocity consensus for a z-network: equalise linear momentum and spin, but do NOT apply
    // orbital angular momentum (r × v). Grids in a z-network are on separate maps that share
    // a coordinate system only in (x,y); they are physically co-located, so treating their
    // layout as a 2-D rigid body and computing torque from off-centre forces would introduce
    // spurious spin whenever force is applied to a single floor.
    private void RunRigidBodyConsensus(CEZGridNetworkComponent net)
    {
        if (net.TotalCachedMass <= 0f)
            return;

        var p = Vector2.Zero;
        var totalMass = 0f;
        var spinL = 0f;
        var spinI = 0f;

        foreach (var gUid in net.Grids)
        {
            if (!_physicsQuery.TryComp(gUid, out var body) || !_gridCompQuery.TryComp(gUid, out var gComp))
                continue;

            var mass = gComp.CachedMass;
            if (mass > 0f)
            {
                p += body.LinearVelocity * mass;
                totalMass += mass;
            }

            if (body.Inertia > 0f)
            {
                spinL += body.Inertia * body.AngularVelocity;
                spinI += body.Inertia;
            }
        }

        if (totalMass <= 0f)
            return;

        var vCom = p / totalMass;
        var omega = spinI > 0f ? spinL / spinI : 0f;

        foreach (var gUid in net.Grids)
        {
            if (!_physicsQuery.TryComp(gUid, out var body) || !IsMoveable(body))
                continue;

            if (!body.LinearVelocity.EqualsApprox(vCom, 0.0001f))
                PhysicsSystem.SetLinearVelocity(gUid, vCom, body: body);
            if (Math.Abs(body.AngularVelocity - omega) > 1e-4f)
                PhysicsSystem.SetAngularVelocity(gUid, omega, body: body);
        }
    }

    public override void UpdateBeforeSolve(bool prediction, float frameTime)
    {
        _inPhysicsTick = true;

        if (prediction)
            return;

        var netEnum = EntityQueryEnumerator<CEZGridNetworkComponent>();
        while (netEnum.MoveNext(out _, out var net))
        {
            if (net.Grids.Count < 2)
                continue;

            if (net.HasStaticAnchor)
            {
                foreach (var gUid in net.Grids)
                {
                    if (!_physicsQuery.TryComp(gUid, out var body) || !IsMoveable(body))
                        continue;
                    if (body.LinearVelocity != Vector2.Zero)
                        PhysicsSystem.SetLinearVelocity(gUid, Vector2.Zero, body: body);
                    if (Math.Abs(body.AngularVelocity) > 1e-4f)
                        PhysicsSystem.SetAngularVelocity(gUid, 0f, body: body);
                }

                continue;
            }

            RunRigidBodyConsensus(net);
        }
    }

    public override void UpdateAfterSolve(bool prediction, float frameTime)
    {
        _inPhysicsTick = false;

        if (prediction)
            return;

        var netEnum = EntityQueryEnumerator<CEZGridNetworkComponent>();
        while (netEnum.MoveNext(out _, out var net))
        {
            if (net.Grids.Count < 2)
                continue;

            // Static networks are kept still by the velocity-zeroing pass in UpdateBeforeSolve; we do
            // not reposition them here, so collisions read as real collisions rather than soft pins.
            if (net.HasStaticAnchor)
                continue;

            EnsureAnchor(net);
            if (!HasValidAnchor(net))
                continue;

            CorrectFlyingNetwork(net);
        }
    }

    // Flying network: redistribute momentum first (reads velocities before any teleport),
    // then softly average each grid's derived anchor and re-apply for position correction.
    // SetWorldPositionRotation resets the physics body velocity in Robust, so consensus
    // must run before the teleport, not after.
    private void CorrectFlyingNetwork(CEZGridNetworkComponent net)
    {
        RunRigidBodyConsensus(net);

        var sumPos = Vector2.Zero;
        var sumRot = 0.0;
        var count = 0;

        foreach (var gUid in net.Grids)
        {
            if (!_gridCompQuery.HasComp(gUid))
                continue;
            var (aPos, aRot) = GetAnchorPoseFromGrid(gUid);
            sumPos += aPos;
            sumRot += aRot.Theta;
            count++;
        }

        if (count == 0)
            return;

        var avgPos = sumPos / count;
        var avgRot = new Angle(sumRot / count);

        _syncing = true;
        foreach (var gUid in net.Grids)
        {
            if (_gridCompQuery.TryComp(gUid, out var comp))
                ApplyAnchorToGrid(gUid, comp, avgPos, avgRot);
        }

        _syncing = false;
    }
}
