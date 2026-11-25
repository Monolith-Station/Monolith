using System.Numerics;
using Content.Shared.Interaction;
using Content.Shared.Projectiles;
using Content.Server._Mono.FireControl;
using Content.Shared._Mono.FireControl;
using Robust.Server.GameObjects;
using EntityCoordinates = Robust.Shared.Map.EntityCoordinates;

namespace Content.Server._Mono.Projectiles.TargetGuided;

/// <summary>
/// Handles the logic for cursor-guided projectiles.
/// </summary>
public sealed partial class TargetGuidedSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _transform = null!;
    [Dependency] private RotateToFaceSystem _rotateToFace = null!;
    [Dependency] private PhysicsSystem _physics = null!;
    [Dependency] private FireControlSystem _fireControl = null!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TargetGuidedComponent, ProjectileHitEvent>(OnProjectileHit);
    }

    /// <summary>
    /// Called when a guided projectile hits something.
    /// </summary>
    private void OnProjectileHit(EntityUid uid, TargetGuidedComponent component, ref ProjectileHitEvent args)
    {
        // No special handling needed after removing target seeking
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<TargetGuidedComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var guidedComp, out var xform))
        {
            // Update lifetime
            guidedComp.CurrentLifetime += frameTime;
            if (guidedComp.CurrentLifetime >= guidedComp.MaxLifetime)
            {
                QueueDel(uid);
                continue;
            }

            // Update cursor tracking time
            guidedComp.TimeSinceLastUpdate += frameTime;

            // Update time since cursor has actually moved
            guidedComp.TimeSinceLastCursorMovement += frameTime;

            // Initialize speed if needed
            if (guidedComp.CurrentSpeed < guidedComp.LaunchSpeed)
                guidedComp.CurrentSpeed = guidedComp.LaunchSpeed;

            // Accelerate up to max speed
            if (guidedComp.CurrentSpeed < guidedComp.MaxSpeed)
                guidedComp.CurrentSpeed += guidedComp.Acceleration * frameTime;
            else
                guidedComp.CurrentSpeed = guidedComp.MaxSpeed;

            // Check if we should stop guiding and just maintain current direction
            bool lostConnection = HasLostConnection(uid, guidedComp);

            // If we newly lost connection, store current direction and mark control as permanently lost
            if (lostConnection && !guidedComp.ConnectionLost)
            {
                // Record current direction when connection is first lost
                guidedComp.FixedDirection = _transform.GetWorldRotation(xform);
                guidedComp.ConnectionLost = true;
                guidedComp.ControlPermanentlyLost = true;
            }

            // Guidance is only available if control hasn't been permanently lost
            if (!guidedComp.ControlPermanentlyLost && guidedComp.TargetPosition.HasValue)
                // Normal cursor guidance
                GuideToTarget(uid, guidedComp, xform, frameTime);

            // Apply velocity in the appropriate direction
            if (guidedComp.ControlPermanentlyLost && guidedComp.FixedDirection.HasValue)
            {
                // Use the fixed direction when control is permanently lost
                _physics.SetLinearVelocity(uid, guidedComp.FixedDirection.Value.ToWorldVec() * guidedComp.CurrentSpeed);

                // Also set the transform rotation to match the fixed direction to prevent visual stuttering
                _transform.SetWorldRotation(xform, guidedComp.FixedDirection.Value);
            }
            else
            {
                // Normal operation - use current rotation
                _physics.SetLinearVelocity(uid, _transform.GetWorldRotation(xform).ToWorldVec() * guidedComp.CurrentSpeed);
            }
        }
    }

    /// <summary>
    /// Sets the target position for a guided projectile.
    /// </summary>
    public void SetTargetPosition(EntityUid uid, EntityCoordinates coordinates, TargetGuidedComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        // If control has been permanently lost, ignore new targeting commands
        if (component.ControlPermanentlyLost)
            return;

        // Mouse path must not fight SARH.
        if (component.UseRadarTrack)
            return;

        // Store the previous position to check if it's changed
        component.PreviousCursorPosition = component.TargetPosition;

        // If the position has changed, reset fallback timers
        if (component.TargetPosition.HasValue)
        {
            // Convert both coordinates to map positions to compare them
            var currentMapPos = coordinates.ToMap(EntityManager, _transform);
            var previousMapPos = component.TargetPosition.Value.ToMap(EntityManager, _transform);

            // Check if they're on the same map and calculate distance
            if (currentMapPos.MapId == previousMapPos.MapId)
            {
                var distance = Vector2.Distance(currentMapPos.Position, previousMapPos.Position);
                if (distance > 0.1f)
                    // Reset the timer only when the cursor actually moves
                    component.TimeSinceLastCursorMovement = 0f;
            }
            else
            {
                // Different maps - definitely moved
                component.TimeSinceLastCursorMovement = 0f;
            }
        }

        // Always reset the update time as we're receiving input
        component.TimeSinceLastUpdate = 0f;
        component.TargetPosition = coordinates;
    }

    public void SetRadarTrackTarget(EntityUid uid, EntityCoordinates coordinates, TargetGuidedComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;
        if (component.ControlPermanentlyLost)
            return;
        if (!component.UseRadarTrack)
            return;

        component.PreviousCursorPosition = component.TargetPosition;
        component.TimeSinceLastUpdate = 0f;
        component.TimeSinceLastCursorMovement = 0f;
        component.TargetPosition = coordinates;
    }

    public void SetGuidanceMode(EntityUid uid, bool useRadarTrack, TargetGuidedComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        if (useRadarTrack && !component.AllowRadarTrack)
            useRadarTrack = false;

        component.UseRadarTrack = useRadarTrack;
        if (useRadarTrack)
        {
            component.ControlPermanentlyLost = false;
            component.ConnectionLost = false;
            component.FixedDirection = null;
            component.TimeSinceLastUpdate = 0f;
            component.TimeSinceLastCursorMovement = 0f;
        }
    }

    /// <summary>
    /// Guides the projectile toward its target position.
    /// </summary>
    private void GuideToTarget(EntityUid uid, TargetGuidedComponent guidedComp, TransformComponent xform, float frameTime)
    {
        if (!guidedComp.TargetPosition.HasValue)
            return;

        // Get the positions in map coordinates
        var targetPos = guidedComp.TargetPosition.Value.ToMap(EntityManager, _transform);
        var missilePos = _transform.ToMapCoordinates(xform.Coordinates);

        // Skip if on different maps
        if (targetPos.MapId != missilePos.MapId)
            return;

        // Calculate angle to the target position
        var angleToTarget = (targetPos.Position - missilePos.Position).ToWorldAngle();

        // Rotate toward that angle at our turn rate
        _rotateToFace.TryRotateTo(
            uid,
            angleToTarget,
            frameTime,
            new Angle(MathF.PI * 2), // Full rotation allowed
            guidedComp.TurnRate?.Theta ?? MathF.PI * 2,
            xform
        );
    }

    /// <summary>
    /// Determines if the missile has lost connection to its controlling console.
    /// When connection is lost, the missile should maintain its current direction.
    /// </summary>
    private bool HasLostConnection(EntityUid uid, TargetGuidedComponent component)
    {
        if (component.UseRadarTrack)
        {
            if (component.ControllingConsole is not { } c || !Exists(c))
                return true;
            if (!TryComp<FireControlConsoleComponent>(c, out var console) || console.ConnectedServer == null)
                return true;
            return false;
        }

        if (component.TimeSinceLastUpdate > component.FallbackTime)
            return true;

        if (component.ControllingConsole is { } consoleUid)
        {
            if (!Exists(consoleUid))
                return true;

            if (!TryComp<FireControlConsoleComponent>(consoleUid, out var console) ||
                console.ConnectedServer == null)
                return true;
        }

        return false;
    }
}
