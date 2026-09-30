using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared._NF.Shipyard.Components;
using Content.Shared.Interaction;
using Content.Server.Popups;
using Robust.Shared.Serialization.Manager;

namespace Content.Server._Mono.DeedCopy;

/// <summary>
/// System for copying deeds between ID cards through interaction.
/// </summary>
public sealed partial class DeedCopySystem : EntitySystem
{
    [Dependency] private SharedIdCardSystem _idCardSystem = default!;
    [Dependency] private PopupSystem _popupSystem = default!;
    [Dependency] private ISerializationManager _serializationManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<IdCardComponent, AfterInteractUsingEvent>(OnAfterInteractUsing);
    }

    /// <summary>
    /// Handle interaction when using one ID card on another to copy deed.
    /// </summary>
    private void OnAfterInteractUsing(EntityUid uid, IdCardComponent component, AfterInteractUsingEvent args)
    {
        // Only proceed if the used item has a deed
        if (!TryComp<ShuttleDeedComponent>(args.Used, out var sourceDeed) || !args.CanReach)
            return;

        // Check if target is an ID card
        if (!HasComp<IdCardComponent>(uid))
            return;

        // Check if target already has a deed
        if (HasComp<ShuttleDeedComponent>(uid))
        {
            _popupSystem.PopupEntity(
                Loc.GetString("deed-copy-target-has-deed"),
                uid,
                args.User
            );
            return;
        }

        // Perform the deed copy from the used item to the target
        CopyDeedToTarget(sourceDeed, uid, args.User);
    }

    /// <summary>
    /// Copy deed properties from source to target ID card.
    /// </summary>
    private void CopyDeedToTarget(ShuttleDeedComponent sourceDeed, EntityUid targetId, EntityUid user)
    {
        // Remove any existing deed component first
        RemComp<ShuttleDeedComponent>(targetId);

        // Create a deep copy of the source deed component using serialization
        var copiedDeed = _serializationManager.CreateCopy(sourceDeed, notNullableOverride: true);

        // Add the copied component to the target entity
        EntityManager.AddComponent(targetId, copiedDeed, overwrite: true);

        // Success message
        _popupSystem.PopupEntity(
            Loc.GetString("deed-copy-success", ("ship", sourceDeed.ShuttleName ?? "Unknown")),
            targetId,
            user
        );
    }
}
