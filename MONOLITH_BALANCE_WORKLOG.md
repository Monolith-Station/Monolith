# Monolith distance and ship balance work

**Date:** 2026-11-24

This document summarizes the sector-scale, ship movement, ship weapon, and POI placement changes made in this work session.

## Ship movement and speed ranking

- Added an explicit `maximumPilotSpeed` to every vessel prototype (105 vessel blocks), using 5-tile-per-second brackets from 15 to 40.
- Removed the hull-size runtime speed estimate. Shipyard spawning now copies the prototype's fixed speed directly to the shuttle component.
- Re-ranked ships by hull size and role to separate heavy ships, general-purpose craft, patrol ships, and fighters. Civilian vessels now span a wider range; the heavy Pelican is set to 15 tiles per second. The highest bracket remains 40.
- Raised thruster acceleration as requested through successive balance passes. The current component defaults are 900 for both `Thrust` and `BaseThrust`; explicit standard, NF, Mono, stealth, debug, and capital thruster prototypes were scaled with them. The drive-mode normalization still respects which thrusters are present on each side and is controlled by its enable and scale CVars.

## Ship weapons

- Reduced shipgun fire rates and ammunition capacity to limit saturation fire, and adjusted shipgun recoil impulses so ships can handle the weapons.
- Reduced shipgun projectile speeds, including kinetic, energy, missile, drone, and specialty launchers.
- Updated affected ammunition descriptions to match their capacities. Infantry weapon prototypes were not changed in these shipgun balance passes.

## Sector distances and detection

- Reworked POI placement distances so travel across the useful sector takes no more than about two minutes at the intended top ship speed.
- Kept Hokkaido at least 1.5 km from Caelestinus Central; the Underground and PDV outposts use 6 km minimum ranges. POI placement also keeps generated locations away from the central origin.
- Rescaled terrain and procedural placement distances to fit the revised sector scale, and set shortband radio reach to 500 tiles.
- Scaled back drone spawning zones and deadzones around selected outposts.
- Reduced ship sensor detection radii. Console lookout views use 512 tiles for non-radar consoles and mass scanners, 1,024 for radar, and 256 for handheld mass scanners.

## FTL drives

- Reduced bluespace drive ranges, renamed drive prototypes and descriptions to match their meter ranges (for example, CTLA-25 became CTLA-750), and added a migration mapping old prototype IDs to the renamed IDs, including related machine-board references.
- Increased the ship-to-ship FTL lock exclusion distance to 250 meters.

## POI separation enforcement

- `nf14.worldgen.min_poi_distance` sets generated POI separation and defaults to 750 meters.
- All procedural POI groups, including cargo depots, share one placement path. It checks the requested position and the loaded grid's actual position against the central origin and previously placed POIs on the same map.
- Required POIs get first pick, with narrow spawn bands prioritized. Sites that cannot be placed enter the next pass with a wider search band. Each of the first three passes allows up to 41 sampled coordinates per site by default. The last pass deterministically computes a clear position beyond all occupied points and checks the loaded grid at that position.
- Widened spawn bands across the Monolith and Frontier POI pools. Zvezda now spawns 3,000 to 4,500 meters from the origin. Stellar Blessing spawns near `(5000, -6000)`, with a 200-meter placement radius. Edison moved inward to a tighter 4,000 to 5,000-meter band.
- Added deterministic tests for 750-meter layouts within authored spawn bands and for the outer pass when narrow bands are fully blocked.

## Validation

- `git diff --check` reported no whitespace errors during the work session.
- Focused Roslyn compilation passed for the POI runtime, prototypes, CVar, and new integration-test fixture.
- `PointOfInterestPlacementTest` passed all three tests with a single MSBuild node. The first full compile took about 64 seconds and test server/client startup about 50 seconds; the geometry checks took under a second.
