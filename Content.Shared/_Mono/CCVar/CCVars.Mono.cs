using Robust.Shared.Configuration;

namespace Content.Shared._Mono.CCVar;

/// <summary>
/// Contains CVars used by Mono.
/// </summary>
[CVarDefs]
public sealed partial class MonoCVars
{
    #region Cleanup

    /// <summary>
    ///     Whether to enable cleanup debug mode, making it run much more often.
    /// </summary>
    public static readonly CVarDef<bool> CleanupDebug =
        CVarDef.Create("mono.cleanup.debug", false, CVar.SERVERONLY);

    /// <summary>
    ///     Whether to log every single entity cleanup deletes.
    /// </summary>
    public static readonly CVarDef<bool> CleanupLog =
        CVarDef.Create("mono.cleanup.log", true, CVar.SERVERONLY);

    /// <summary>
    ///     Don't delete non-grids at most this close to a grid.
    /// </summary>
    public static readonly CVarDef<float> CleanupMaxGridDistance =
        CVarDef.Create("mono.cleanup.max_grid_distance", 20.0f, CVar.SERVERONLY);

    /// <summary>
    ///     How far away from any players can a mob be until it gets cleaned up.
    /// </summary>
    public static readonly CVarDef<float> MobCleanupDistance =
        CVarDef.Create("mono.cleanup.mob.distance", 1280.0f, CVar.SERVERONLY);

    /// <summary>
    ///     How far away from any players can a grid be until it gets cleaned up.
    /// </summary>
    public static readonly CVarDef<float> GridCleanupDistance =
        CVarDef.Create("mono.cleanup.grid.distance", 628.0f, CVar.SERVERONLY);

    /// <summary>
    ///     How much can a grid at most be worth for it to be cleaned up.
    /// </summary>
    public static readonly CVarDef<float> GridCleanupMaxValue =
        CVarDef.Create("mono.cleanup.grid.max_value", 30000.0f, CVar.SERVERONLY);

    /// <summary>
    ///     Duration, in seconds, for how long a grid has to fulfill cleanup conditions to get cleaned up.
    /// </summary>
    public static readonly CVarDef<float> GridCleanupDuration =
        CVarDef.Create("mono.grid_cleanup_duration", 60f * 30f, CVar.SERVERONLY);

    /// <summary>
    ///     How far away from any players does a spaced entity have to be in order to get cleaned up.
    /// </summary>
    public static readonly CVarDef<float> SpaceCleanupDistance =
        CVarDef.Create("mono.cleanup.space.distance", 628f, CVar.SERVERONLY);

    /// <summary>
    ///     How much can a spaced entity at most be worth for it to be cleaned up.
    /// </summary>
    public static readonly CVarDef<float> SpaceCleanupMaxValue =
        CVarDef.Create("mono.cleanup.space.max_value", 10000.0f, CVar.SERVERONLY);

    #endregion

    /// <summary>
    ///     Whether to play radio static/noise sounds when receiving radio messages on headsets.
    /// </summary>
    public static readonly CVarDef<bool> RadioNoiseEnabled =
        CVarDef.Create("mono.radio_noise_enabled", true, CVar.ARCHIVE | CVar.CLIENTONLY);


    #region Audio

    /// <summary>
    ///     Whether the client should hear combat music triggered by ship artillery.
    /// </summary>
    public static readonly CVarDef<bool> CombatMusicEnabled =
        CVarDef.Create("mono.combat_music.enabled", true, CVar.ARCHIVE | CVar.CLIENTONLY);

    /// <summary>
    ///     Whether to render sounds with echo when they are in 'large' open, rooved areas.
    /// </summary>
    /// <seealso cref="AreaEchoSystem"/>
    public static readonly CVarDef<bool> AreaEchoEnabled =
        CVarDef.Create("mono.area_echo.enabled", true, CVar.ARCHIVE | CVar.CLIENTONLY);

    /// <summary>
    ///     If false, area echos calculate with 4 directions (NSEW).
    ///         Otherwise, area echos calculate with all 8 directions.
    /// </summary>
    /// <seealso cref="AreaEchoSystem"/>
    public static readonly CVarDef<bool> AreaEchoHighResolution =
        CVarDef.Create("mono.area_echo.alldirections", false, CVar.ARCHIVE | CVar.CLIENTONLY);


    /// <summary>
    ///     How many times a ray can bounce off a surface for an echo calculation.
    /// </summary>
    /// <seealso cref="AreaEchoSystem"/>
    public static readonly CVarDef<int> AreaEchoReflectionCount =
        CVarDef.Create("mono.area_echo.max_reflections", 1, CVar.ARCHIVE | CVar.CLIENTONLY);

    /// <summary>
    ///     Distantial interval, in tiles, in the rays used to calculate the roofs of an open area for echos,
    ///         or the ray's distance to space, at which the tile at that point of the ray is processed.
    ///
    ///     The lower this is, the more 'predictable' and computationally heavy the echoes are.
    /// </summary>
    /// <seealso cref="AreaEchoSystem"/>
    public static readonly CVarDef<float> AreaEchoStepFidelity =
        CVarDef.Create("mono.area_echo.step_fidelity", 5f, CVar.CLIENTONLY);

    /// <summary>
    ///     Interval between updates for every audio entity.
    /// </summary>
    /// <seealso cref="AreaEchoSystem"/>
    public static readonly CVarDef<TimeSpan> AreaEchoRecalculationInterval =
        CVarDef.Create("mono.area_echo.recalculation_interval", TimeSpan.FromSeconds(15), CVar.ARCHIVE | CVar.CLIENTONLY);

    #endregion

}
