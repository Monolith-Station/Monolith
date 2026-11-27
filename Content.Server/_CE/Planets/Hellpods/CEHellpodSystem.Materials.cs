using System.Linq;
using Content.Server.Materials;
using Content.Shared._CE.Planets.Hellpods;
using Content.Shared.Materials;
using Content.Shared.Materials.OreSilo;
using Robust.Shared.Prototypes;

namespace Content.Server._CE.Planets.Hellpods;

public sealed partial class CEHellpodSystem
{
    [Dependency] private OreSiloSystem _silos = default!;
    [Dependency] private MaterialStorageSystem _materials = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    private sealed record VolleyReservation(EntityUid Silo, EntProtoId Pod,
        Dictionary<ProtoId<MaterialPrototype>, int> MaterialsPerPod, List<EntityUid> Hatches)
    {
        public int Unfired = Hatches.Count;
    }

    private EntityUid? GetAvailableSilo(EntityUid console)
    {
        if (!TryComp<OreSiloClientComponent>(console, out var client) || client.Silo is not { } silo ||
            TerminatingOrDeleted(silo) || !HasComp<MaterialStorageComponent>(silo) ||
            !_silos.CanTransmitMaterials(silo, console))
            return null;

        return silo;
    }

    private bool CanFundVolley(EntityUid? silo, CEHellpodTypePrototype type, int hatchCount)
    {
        if (silo is not { } source || hatchCount == 0)
            return false;

        foreach (var (material, amount) in type.Materials)
        {
            if (!_materials.CanChangeMaterialAmount(source, material.Id, -amount * hatchCount, localOnly: true))
                return false;
        }

        return true;
    }

    private bool TryReserveVolley(Drop drop, List<EntityUid> hatches)
    {
        if (!TryComp<CEHellpodConsoleComponent>(drop.Console, out var console) ||
            !_prototypes.TryIndex(console.PodType, out var type) ||
            GetAvailableSilo(drop.Console) is not { } silo ||
            !CanFundVolley(silo, type, hatches.Count))
            return false;

        var cost = type.Materials.ToDictionary(pair => pair.Key, pair => -pair.Value * hatches.Count);
        if (!_materials.TryChangeMaterialAmount(silo, cost, localOnly: true))
            return false;

        drop.Reservation = new VolleyReservation(silo, type.Pod, new(type.Materials), hatches);
        return true;
    }

    private void RefundVolley(EntityUid grid, Drop drop, VolleyReservation reservation)
    {
        if (reservation.Unfired == 0)
            return;

        var refund = reservation.MaterialsPerPod.ToDictionary(pair => pair.Key, pair => pair.Value * reservation.Unfired);
        if (!TerminatingOrDeleted(reservation.Silo) && HasComp<MaterialStorageComponent>(reservation.Silo) &&
            _materials.TryChangeMaterialAmount(reservation.Silo, refund, localOnly: true))
            return;

        var fallback = !TerminatingOrDeleted(drop.Console) ? drop.Console : grid;
        if (TerminatingOrDeleted(fallback) || Transform(fallback).MapUid == null)
            return;

        foreach (var (material, amount) in refund)
            _materials.SpawnMultipleFromMaterial(amount, material.Id, Transform(fallback).Coordinates);
    }

    private void UnloadPod(EntityUid hatch)
    {
        if (TerminatingOrDeleted(hatch) || !HasComp<CEHellpodHatchComponent>(hatch))
            return;

        var seat = Seat(hatch);
        if (seat.ContainedEntity is { } occupant && !TerminatingOrDeleted(occupant))
            _containers.Remove(occupant, seat);
    }
}
