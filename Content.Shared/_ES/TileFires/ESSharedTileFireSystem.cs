using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using JetBrains.Annotations;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared._ES.TileFires;

/// <summary>
///     Shared API for spawning tile fires.
///     See serverside system for actual growth logic.
/// </summary>
public abstract class ESSharedTileFireSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] protected readonly SharedMapSystem MapSys = default!;
    [Dependency] protected readonly SharedTransformSystem XformSys = default!;

    public override void Initialize()
    {
        base.Initialize();

        // appearance on startup
        SubscribeLocalEvent<FlammableComponent, ComponentStartup>(OnStartup);
    }

    #region Events
    private void OnStartup(Entity<FlammableComponent> ent, ref ComponentStartup args)
    {
        var flammable = ent.Comp;
        // not done in flammablesys because no shared and i want this in entity spawn menu man idk
        if (!TryComp<AppearanceComponent>(ent, out var appearance))
            return;

        _appearance.SetData(ent, FireVisuals.OnFire, flammable.OnFire, appearance);
        _appearance.SetData(ent, FireVisuals.FireStacks, (int) MathF.Floor(flammable.FireStacks / flammable.FirestackVisualDivisor), appearance);
    }
    #endregion

    #region API

    /// <summary>
    ///     Spawns a tile fire at stage <see cref="stage"/> at the given entity.
    /// </summary>
    [PublicAPI]
    public bool TryDoTileFire(Entity<TransformComponent?> entity, EntityUid? originatingUser = null, int stage = 1)
    {
        return TryDoTileFire(entity.Comp?.Coordinates ?? Transform(entity.Owner).Coordinates, originatingUser, stage);
    }

    /// <summary>
    ///     Spawns a tile fire at stage <see cref="stage"/> at the given entity's coordinates.
    /// </summary>
    [PublicAPI]
    public virtual bool TryDoTileFire(EntityCoordinates coords, EntityUid? originatingUser = null, int stage = 1)
    {
        // See server logic
        return false;
    }

    #endregion
}

[ByRefEvent]
public record struct ESTileFireCreatedEvent(
    EntityCoordinates Coordinates,
    EntityUid? OriginatingUser = null,
    int Stage = 1);
