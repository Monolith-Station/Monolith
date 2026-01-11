using Content.Shared._Mono.ShipRepair.Components;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using System.Numerics;

namespace Content.Shared._Mono.ShipRepair;

public abstract partial class SharedShipRepairSystem : EntitySystem
{
    private List<DoAfterId> _toRemoveIds = new();

    private void InitTool()
    {
        SubscribeLocalEvent<ShipRepairToolComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<ShipRepairToolComponent, ShipRepairDoAfterEvent>(OnRepairDoAfter);
    }

    private void OnAfterInteract(Entity<ShipRepairToolComponent> ent, ref AfterInteractEvent args)
    {
        if (!args.CanReach)
            return;

        var ourXform = Transform(ent);
        var clickPos = args.ClickLocation;
        var clickWorld = _transform.ToWorldPosition(clickPos);
        var grids = new List<Entity<MapGridComponent>>();
        _mapMan.FindGridsIntersecting(ourXform.MapID, Box2.CenteredAround(clickWorld, new Vector2(1f, 1f)), ref grids, false, false);
        if (grids.Count == 0 && ourXform.GridUid == null)
            return;

        var targetGrid = ourXform.GridUid == null ? grids[0] : (ourXform.GridUid.Value, Comp<MapGridComponent>(ourXform.GridUid.Value));

        if (TryComp<ShipRepairRestrictComponent>(targetGrid, out var restrict)
            && _whitelist.IsWhitelistFail(restrict.ToolWhitelist, ent))
        {
            _popup.PopupClient(Loc.GetString("ship-repair-tool-fail-whitelist"), ent, args.User, PopupType.MediumCaution);
            return;
        }

        if (!TryComp<ShipRepairDataComponent>(targetGrid, out var repairData))
        {
            _popup.PopupClient(Loc.GetString("ship-repair-tool-no-data"), ent, args.User, PopupType.MediumCaution);
            return;
        }

        var gridIndices = _map.CoordinatesToTile(targetGrid, targetGrid.Comp, clickPos);

        if (!TryGetChunk(repairData, gridIndices, out var chunk))
            return;

        // first try repair tile if we can
        if (ent.Comp.EnableTileRepair)
        {
            var relativeIndices = GetRelativeIndices(gridIndices, repairData.ChunkSize);
            var index = relativeIndices.X + relativeIndices.Y * repairData.ChunkSize;

            var storedTile = chunk.Tiles[index];
            var currentTile = _map.GetTileRef(targetGrid, targetGrid.Comp, gridIndices).Tile;

            if (storedTile != currentTile.TypeId)
            {
                StartRepair(ent, args.User, targetGrid, gridIndices, ent.Comp.TileRepairTime * ent.Comp.RepairTimeMultiplier, ent.Comp.TileRepairCost);
                return; // do not attempt anything else
            }
        }

        var alreadyExists = false;
        var notEnoughCharges = false;
        // try entity repair if we haven't done tile repair
        if (ent.Comp.EnableEntityRepair)
        {
            foreach (var (id, spec) in chunk.Entities)
            {
                // just fail the repair if it doesn't have the comp
                if (!_proto.TryIndex(repairData.EntityPalette[spec.ProtoIndex], out var entProto)
                    || !entProto.TryGetComponent<ShipRepairableComponent>(out var repairable, Factory)
                    || entProto.TryGetComponent<ShipRepairableRestrictComponent>(out var entRestrict, Factory)
                        && _whitelist.IsWhitelistFail(entRestrict.ToolWhitelist, ent)
                )
                    continue;

                var delay = repairable.RepairTime * ent.Comp.RepairTimeMultiplier;
                var cost = repairable.RepairCost;

                // only consider it if it's close enough
                if ((spec.LocalPosition - clickPos.Position).Length() > ent.Comp.EntitySearchRadius)
                    continue;

                var needsRepair = true;
                var origUid = spec.OriginalEntity == null ? (EntityUid?)null : GetEntity(spec.OriginalEntity.Value);
                if (origUid != null && !TerminatingOrDeleted(origUid))
                {
                    var ev = new ShipRepairReinstateQueryEvent(true);
                    RaiseLocalEvent(origUid.Value, ref ev);

                    if (!ev.Handled)
                    {
                        // if it's still on a grid, don't repair, else delete it
                        var origXform = Transform(origUid.Value);
                        if (origXform.GridUid != null)
                        {
                            alreadyExists = true;
                            continue;
                        }
                        else if (_net.IsServer)
                        {
                            QueueDel(origUid); // Big PVS does not want us to predict this
                        }
                    }
                    needsRepair = ev.Repairable;
                }

                // try repair another if we're already trying to repair this entity
                if (TryComp<DoAfterComponent>(args.User, out var doAfterComp))
                {
                    var hasIdentical = false;
                    _toRemoveIds.Clear();
                    foreach (var doAfterId in ent.Comp.DoAfters)
                    {
                        if (!doAfterComp.DoAfters.TryGetValue(doAfterId.Index, out var doAfter)
                            || doAfter.Args.Event is not ShipRepairDoAfterEvent repairEv)
                        {
                            _toRemoveIds.Add(doAfterId);
                            continue;
                        }

                        if (repairEv.TargetGridIndices == gridIndices && repairEv.RepairId == id)
                        {
                            hasIdentical = true;
                            break;
                        }
                    }
                    foreach (var remove in _toRemoveIds)
                        ent.Comp.DoAfters.Remove(remove);

                    if (hasIdentical)
                        continue;
                }

                var enough = !_charges.HasInsufficientCharges(ent, cost);
                notEnoughCharges |= !enough;
                if (needsRepair && enough)
                {
                    StartRepair(ent, args.User, targetGrid, gridIndices, delay, cost, id);
                    return;
                }
            }
        }
        if (notEnoughCharges)
            _popup.PopupClient(Loc.GetString("ship-repair-tool-insufficient-ammo"), ent, args.User);
        else if (alreadyExists && _net.IsServer) // else we show it once or twice depending on whether it's in PVS
            _popup.PopupEntity(Loc.GetString("ship-repair-tool-entity-exists"), ent, args.User, PopupType.SmallCaution);
    }

    private void StartRepair(Entity<ShipRepairToolComponent> tool, EntityUid user, Entity<MapGridComponent> grid, Vector2i tileIndices, float delay, int cost, int? repairId = null)
    {
        var ev = new ShipRepairDoAfterEvent
        {
            TargetGridIndices = tileIndices,
            RepairId = repairId,
            Cost = cost
        };

        var args = new DoAfterArgs(EntityManager, user, delay, ev, tool, grid)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            // only block if we're trying the exact same
            DuplicateCondition = DuplicateConditions.SameEvent
        };

        if (_doAfter.TryStartDoAfter(args, out var id))
        {
            tool.Comp.DoAfters.Add(id.Value);
            _audio.PlayPredicted(tool.Comp.RepairSound, tool, user);
            // we don't need to spawn it on server, however this makes serverside failures make the effect anyway
            if (_net.IsClient && _timing.IsFirstTimePredicted)
            {
                // needed so it doesn't fall off if in space
                var effect = Spawn(tool.Comp.ConstructEffect, new EntityCoordinates(grid, Vector2.Zero));
                _parent.SetForceParent(effect, new EntityCoordinates(grid, _map.TileCenterToVector(grid, tileIndices)));
            }
        }
    }

    private void OnRepairDoAfter(Entity<ShipRepairToolComponent> ent, ref ShipRepairDoAfterEvent args)
    {
        ent.Comp.DoAfters.Remove(args.DoAfter.Id);

        if (args.Cancelled || args.Handled)
            return;

        if (args.Target is not { } targetGrid || !TryComp<ShipRepairDataComponent>(targetGrid, out var repairData))
            return;

        if (!TryGetChunk(repairData, args.TargetGridIndices, out var chunk))
            return;

        if (_charges.HasInsufficientCharges(ent, args.Cost))
        {
            _popup.PopupEntity(Loc.GetString("ship-repair-tool-insufficient-ammo"), ent, args.User);
            return;
        }

        if (args.RepairId != null)
        {
            if (_net.IsClient || !chunk.Entities.TryGetValue(args.RepairId.Value, out var spec))
                return;

            // this is technically copypaste code but it's different each time
            var origUid = spec.OriginalEntity == null ? (EntityUid?)null : GetEntity(spec.OriginalEntity.Value);
            if (origUid != null && !TerminatingOrDeleted(origUid.Value))
            {
                var ev = new ShipRepairReinstateQueryEvent(true);
                RaiseLocalEvent(origUid.Value, ref ev);
                // abort if we can't repair now
                if (!ev.Handled || !ev.Repairable)
                    return;
            }

            var protoId = repairData.EntityPalette[spec.ProtoIndex];
            var coords = new EntityCoordinates(targetGrid, spec.LocalPosition);

            var spawned = Spawn(protoId, coords);
            _transform.SetLocalRotation(spawned, spec.Rotation);

            spec.OriginalEntity = GetNetEntity(spawned);

            var dirtMsg = new RepairEntityMessage(GetNetEntity(targetGrid), args.TargetGridIndices, args.RepairId.Value, spec);
            RaiseNetworkEvent(dirtMsg);
        }
        else
        {
            TryRepairTileTile((targetGrid, repairData), args.TargetGridIndices);
        }

        _charges.UseCharges(ent, args.Cost);
        args.Handled = true;
    }
}
