/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Numerics;
using Robust.Shared.GameStates;

namespace Content.Shared._CE.ZLevels.Core.Components;

/// <summary>
/// Allows an entity to move up and down the z-levels by gravity or jumping
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true, fieldDeltas: true)]
public sealed partial class CEZPhysicsComponent : Component
{
    /// <summary>
    /// The current speed of movement between z-levels.
    /// If greater than 0, the entity moves upward. If less than 0, the entity moves downward.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Velocity;

    /// <summary>
    /// The current height of the entity within the current Z-level.
    /// Takes values from 0 to 1. If the value rises above 1, the entity moves up to the next level and the value is normalized.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float LocalPosition;

    [AutoNetworkedField]
    public bool ProjectileFlight;

    /// <summary>
    /// Seconds left in a ground-liftoff spool-up, for the shuttle console countdown.
    /// Zero when not launching.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float LaunchCountdown;

    /// <summary>Ground contact, independent of body type. Terrain may be outside client PVS.</summary>
    [DataField, AutoNetworkedField]
    public bool GroundContact;

    /// <summary>
    /// Cached value of the current z-level map height
    /// </summary>
    [DataField, AutoNetworkedField]
    public int CurrentZLevel;


    [DataField, AutoNetworkedField]
    public float Bounciness;

    [DataField, AutoNetworkedField]
    public float GravityMultiplier = 1f;

    [DataField, AutoNetworkedField]
    public bool Fallable = true;

    /// <summary>Draw depth before applying Z height.</summary>
    [DataField]
    public int DrawDepthDefault;

    /// <summary>Sprite offset before applying Z height.</summary>
    [DataField]
    public Vector2 SpriteOffsetDefault = Vector2.Zero;

    /// <summary>Whether the entity automatically steps up onto high ground.</summary>
    [DataField]
    public bool AutoStep = true;

    #region Gravity

    [DataField]
    public bool VelocityGravity = true;

    [DataField]
    public bool VelocityRaiseEvent;

    #endregion

    #region Cache

    [ViewVariables]
    public Vector2i? CachedTile;

    /// <summary>
    /// Cached value of the current distance to the ground in the current z-level. Updates only on MoveEvent and when tiles below change.
    /// </summary>
    [ViewVariables]
    public float CachedGroundHeight;

    /// <summary>
    /// Cached value of whether the entity is currently on sticky ground (ladders).
    /// </summary>
    [ViewVariables]
    public bool CachedStickyGround;

    #endregion

    #region Sleep

    [DataField]
    public float SleepTimer;

    [ViewVariables]
    public bool Sleeping;

    [DataField]
    public float SleepThreshold = 0.3f;

    [DataField]
    public float TimeToSleep = 2f;

    #endregion
}
