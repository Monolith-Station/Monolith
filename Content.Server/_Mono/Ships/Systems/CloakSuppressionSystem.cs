using System.Linq;
using Content.Shared._Mono.Company;
using Content.Shared._Mono.Ships.Components;
using Content.Shared._NF.Shipyard.Prototypes;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Mono.Ships.Systems;

/// <summary>
/// System that handles IFF suppression by ships with CloakHunter capability. Very performant, like everything I make.
/// </summary>
public sealed class CloakSuppressionSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedShuttleSystem _shuttle = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    /// <summary>
    /// Range in meters within which CloakHunter ships suppress IFF Hide flags.
    /// </summary>
    private const float SuppressionRange = 256f; // Hardcode? Yes please!

    /// <summary>
    /// How often to check for IFF suppression (in seconds).
    /// </summary>
    private const float UpdateInterval = 2f; // Whatever

    private TimeSpan _nextUpdate = TimeSpan.Zero;

    public override void Initialize()
    {
        base.Initialize();

        _nextUpdate = _timing.CurTime + TimeSpan.FromSeconds(UpdateInterval);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        if (curTime < _nextUpdate)
            return;

        _nextUpdate = curTime + TimeSpan.FromSeconds(UpdateInterval);

        ProcessIffSuppression();
    }

    /// <summary>
    /// Checks all ships for IFF suppression.
    /// </summary>
    private void ProcessIffSuppression()
    {
        // Get all ships with VesselComponent
        var vesselQuery = AllEntityQuery<VesselComponent, TransformComponent>();
        var cloakHunterShips = new List<(EntityUid uid, TransformComponent xform, VesselPrototype prototype)>();
        var allShips = new List<(EntityUid uid, TransformComponent xform, VesselComponent vessel)>();

        // Collect all ships and identify CloakHunter ships
        while (vesselQuery.MoveNext(out var uid, out var vessel, out var xform))
        {
            // Skip ships not on a valid map
            if (!xform.MapUid.HasValue)
                continue;

            allShips.Add((uid, xform, vessel));

            // Check if this is a CloakHunter ship
            if (IsCloakHunterShip(vessel, out var prototype) && prototype != null)
            {
                cloakHunterShips.Add((uid, xform, prototype));
            }
        }

        // Check each CloakHunter ship for nearby ships to suppress
        foreach (var (hunterUid, hunterXform, hunterPrototype) in cloakHunterShips)
        {
            ProcessCloakHunterSuppression(hunterUid, hunterXform, hunterPrototype, allShips);
        }

        // Check for ships that should no longer be suppressed
        ProcessSuppressionCleanup(cloakHunterShips.Select(x => (x.uid, x.xform)).ToList());
    }

    /// <summary>
    /// Checks if a ship is a CloakHunter ship based on its VesselPrototype.
    /// </summary>
    private bool IsCloakHunterShip(VesselComponent vessel, out VesselPrototype? prototype)
    {
        prototype = null;
        if (!_prototypeManager.TryIndex(vessel.VesselId, out prototype))
            return false;

        return prototype.CloakHunter;
    }

    /// <summary>
    /// Processes IFF suppression for a specific CloakHunter ship.
    /// </summary>
    private void ProcessCloakHunterSuppression(EntityUid hunterUid, TransformComponent hunterXform,
        VesselPrototype hunterPrototype, List<(EntityUid uid, TransformComponent xform, VesselComponent vessel)> allShips)
    {
        var hunterPos = _transform.GetMapCoordinates(hunterUid, xform: hunterXform);

        foreach (var (shipUid, shipXform, _) in allShips)
        {
            // Skip the CloakHunter ship itself
            if (shipUid == hunterUid)
                continue;

            // Skip ships on different maps
            if (shipXform.MapUid != hunterXform.MapUid)
                continue;

            // Check if ship has IFF Hide flag
            if (!TryComp<IFFComponent>(shipUid, out var iffComp) ||
                (iffComp.Flags & IFFFlags.Hide) == 0)
                continue;

            if (HasComp<TemporaryFtlIffStorageComponent>(shipUid))
                continue;

            // Check if the target ship has a matching company
            if (ShouldNotSuppressShip(shipUid, hunterPrototype))
                continue;

            var shipPos = _transform.GetMapCoordinates(shipUid, xform: shipXform);
            var distance = (hunterPos.Position - shipPos.Position).Length();

            // If within suppression range, suppress the IFF
            if (distance <= SuppressionRange)
            {
                SuppressShipIff(shipUid, hunterUid, iffComp);
            }
        }
    }

    /// <summary>
    /// Checks if a ship should be suppressed based on company matching.
    /// </summary>
    private bool ShouldNotSuppressShip(EntityUid shipUid, VesselPrototype hunterPrototype)
    {
        // If no companies specified, suppress all ships
        if (hunterPrototype.Company.Count == 0)
            return false;

        // Check if the ship has a company component that matches any of the hunter's allied companies
        if (TryComp<CompanyComponent>(shipUid, out var companyComp))
        {
            return hunterPrototype.Company.Contains(companyComp.CompanyName);
        }

        // If ship has no company component, suppress it
        return false;
    }

    /// <summary>
    /// Suppresses a ship's IFF Hide flag by changing it to None.
    /// </summary>
    private void SuppressShipIff(EntityUid shipUid, EntityUid hunterUid, IFFComponent iffComp)
    {
        // Check if already suppressed by this or another CloakHunter ship
        if (HasComp<CloakSuppressionComponent>(shipUid))
            return;

        // Add suppression component to track this suppression
        var suppressionComp = AddComp<CloakSuppressionComponent>(shipUid);
        suppressionComp.SuppressingShip = hunterUid;
        suppressionComp.SuppressionStartTime = _timing.CurTime;
        suppressionComp.OriginalReadOnlyState = iffComp.ReadOnly;

        // Remove the Hide flag, which will make the ship visible
        _shuttle.RemoveIFFFlag(shipUid, IFFFlags.Hide, iffComp);

        // Set IFF to ReadOnly to prevent the ship from turning Hide flag back on
        _shuttle.SetIFFReadOnly(shipUid, true, iffComp);
    }

    /// <summary>
    /// Cleans up suppression for ships that are no longer in range of any CloakHunter ship.
    /// </summary>
    private void ProcessSuppressionCleanup(List<(EntityUid uid, TransformComponent xform)> cloakHunterShips)
    {
        var suppressedQuery = AllEntityQuery<CloakSuppressionComponent, TransformComponent>();

        while (suppressedQuery.MoveNext(out var suppressedUid, out var suppressionComp, out var suppressedXform))
        {
            bool stillInRange = false;

            // Check if still in range of any CloakHunter ship
            foreach (var (hunterUid, hunterXform) in cloakHunterShips)
            {
                // Skip if on different maps
                if (suppressedXform.MapUid != hunterXform.MapUid)
                    continue;

                var hunterPos = _transform.GetMapCoordinates(hunterUid, xform: hunterXform);
                var suppressedPos = _transform.GetMapCoordinates(suppressedUid, xform: suppressedXform);
                var distance = (hunterPos.Position - suppressedPos.Position).Length();

                if (distance <= SuppressionRange)
                {
                    stillInRange = true;
                    break;
                }
            }

            // If no longer in range, restore the Hide flag
            if (!stillInRange)
            {
                RestoreShipIff(suppressedUid, suppressionComp);
            }
        }
    }

    /// <summary>
    /// Restores a ship's IFF Hide flag and ReadOnly state when it's no longer being suppressed.
    /// </summary>
    private void RestoreShipIff(EntityUid shipUid, CloakSuppressionComponent suppressionComp)
    {
        // Restore the original ReadOnly state first
        if (TryComp<IFFComponent>(shipUid, out var iffComp))
        {
            _shuttle.SetIFFReadOnly(shipUid, suppressionComp.OriginalReadOnlyState, iffComp);
        }

        // Add the Hide flag back
        _shuttle.AddIFFFlag(shipUid, IFFFlags.Hide);

        // Remove the suppression component
        RemComp<CloakSuppressionComponent>(shipUid);
    }
}
