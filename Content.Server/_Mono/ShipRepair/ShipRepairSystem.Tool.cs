using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared._Mono.ShipRepair;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Content.Shared.Popups;

namespace Content.Server._Mono.ShipRepair;

public sealed partial class ShipRepairSystem : EntitySystem
{
    private void InitTool()
    {
        SubscribeLocalEvent<ShipRepairToolComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<ShipRepairToolComponent, ShipRepairDoAfterEvent>(OnRepairDoAfter);
    }

    private void OnAfterInteract(Entity<ShipRepairToolComponent> ent, ref AfterInteractEvent args)
    {
        if (!args.CanReach)
            return;

        // TODO: find grids near click instead of using user grid
        var maybeTargetGrid = Transform(ent).GridUid;
        if (!TryComp<MapGridComponent>(maybeTargetGrid, out var gridComp))
            return;
        var targetGrid = maybeTargetGrid.Value;

        if (!TryComp<ShipRepairDataComponent>(targetGrid, out var repairData))
        {
            GenerateRepairData(targetGrid);
            return;
        }

        var clickPos = args.ClickLocation;
        var gridIndices = _map.CoordinatesToTile(targetGrid, gridComp, clickPos);

        if (!TryGetChunk(repairData, gridIndices, out var chunk))
            return;

        // first try repair tile if we can
        if (ent.Comp.EnableTileRepair)
        {
            var relativeIndices = GetRelativeIndices(gridIndices, repairData.ChunkSize);
            var index = relativeIndices.X + relativeIndices.Y * repairData.ChunkSize;

            var storedTile = chunk.Tiles[index];
            var currentTile = _map.GetTileRef(targetGrid, gridComp, gridIndices).Tile;

            // don't repair to space or a tile that existss
            if (storedTile != Tile.Empty.TypeId && currentTile.IsEmpty)
            {
                StartRepair(ent, args.User, targetGrid, gridIndices, ent.Comp.TileRepairTime * ent.Comp.RepairTimeMultiplier, ent.Comp.TileRepairCost);
                return; // do not attempt anything else
            }
        }

        // try entity repair if we haven't done tile repair
        if (ent.Comp.EnableEntityRepair)
        {
            foreach (var (id, spec) in chunk.Entities)
            {
                if (!_proto.TryIndex(repairData.EntityPalette[spec.ProtoIndex], out var entProto)
                    || !entProto.TryGetComponent<ShipRepairableComponent>(out var repairable, Factory)
                )
                    continue;

                var delay = repairable.RepairTime * ent.Comp.RepairTimeMultiplier;
                var cost = repairable.RepairCost;

                // only consider it if it's close enough
                if ((spec.LocalPosition - clickPos.Position).Length() > ent.Comp.EntitySearchRadius)
                    continue;

                var needsRepair = true;
                if (spec.OriginalEntity != null && !TerminatingOrDeleted(spec.OriginalEntity))
                {
                    var ev = new ShipRepairReinstateQueryEvent(true);
                    RaiseLocalEvent(spec.OriginalEntity.Value, ref ev);

                    if (!ev.Handled)
                    {
                        // if it's still on a grid, don't repair, else delete it
                        var origXform = Transform(spec.OriginalEntity.Value);
                        if (origXform.GridUid != null)
                        {
                            _popup.PopupEntity(Loc.GetString("ship-repair-tool-entity-exists"), ent, args.User, PopupType.SmallCaution);
                            continue;
                        }
                        else
                        {
                            QueueDel(spec.OriginalEntity);
                        }
                    }

                    needsRepair = ev.Repairable;
                }

                if (needsRepair)
                {
                    StartRepair(ent, args.User, targetGrid, gridIndices, delay, cost, id);
                    return;
                }
            }
        }
    }

    private void StartRepair(Entity<ShipRepairToolComponent> tool, EntityUid user, EntityUid grid, Vector2i tileIndices, float delay, int cost, int? repairId = null)
    {
        if (_charges.HasInsufficientCharges(tool, cost))
        {
            _popup.PopupEntity(Loc.GetString("ship-repair-tool-insufficient-ammo"), tool, user);
            return;
        }

        _audio.PlayPvs(tool.Comp.RepairSound, tool);

        var ev = new ShipRepairDoAfterEvent
        {
            TargetGridIndices = tileIndices,
            RepairId = repairId,
            Cost = cost
        };

        var args = new DoAfterArgs(EntityManager, user, delay, ev, tool, grid)
        {
            BreakOnMove = true,
            BreakOnDamage = true
        };

        _doAfter.TryStartDoAfter(args);
    }

    private void OnRepairDoAfter(Entity<ShipRepairToolComponent> ent, ref ShipRepairDoAfterEvent args)
    {
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
            if (!chunk.Entities.TryGetValue(args.RepairId.Value, out var spec))
                return;

            var protoId = repairData.EntityPalette[spec.ProtoIndex];
            var coords = new EntityCoordinates(targetGrid, spec.LocalPosition);

            var spawned = Spawn(protoId, coords);
            _transform.SetLocalRotation(spawned, spec.Rotation);

            spec.OriginalEntity = spawned;
        }
        else
        {
            TryRepairTileTile((targetGrid, repairData), args.TargetGridIndices);
        }

        _charges.UseCharges(ent, args.Cost);
        args.Handled = true;
    }
}
