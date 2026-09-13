/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Robust.Shared.GameStates;

namespace Content.Shared._CE.ZLevels.Core.Components;

/// <summary>
/// A mapping-only "gravity generator lite": while present on a grid, it holds that grid's
/// entire z-grid network aloft with an unlimited mass rating (the network never falls) and
/// pins it in place statically (no drift), but does NOT provide actual gravity to entities on
/// it. Implemented by <c>CEZMappingAnchorSystem</c>, which tags the parent grid with
/// <see cref="CEZMappingAnchorGridComponent"/>; the z-level gravity and grid-sync systems read
/// that tag. Removing the entity drops the tag, so the lift and static lock release.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CEZMappingAnchorComponent : Component;

/// <summary>
/// Runtime tag placed on a grid that currently hosts a <see cref="CEZMappingAnchorComponent"/>.
/// The z-level fall gate treats such a grid as unlimited gravgen lift, and the grid-sync system
/// treats it as a static anchor (locking its whole network in place). Reconstructed from the
/// anchor entities, never persisted.
/// </summary>
[RegisterComponent, NetworkedComponent, UnsavedComponent]
public sealed partial class CEZMappingAnchorGridComponent : Component;
