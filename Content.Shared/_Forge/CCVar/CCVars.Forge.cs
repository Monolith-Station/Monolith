using Robust.Shared.Configuration;

namespace Content.Shared._Forge.CCVar;

/// <summary>
/// Contains CVars used by Forge.
/// </summary>
[CVarDefs]
public sealed partial class ForgeCVars
{
    public static readonly CVarDef<float> StationAiSsdGracePeriod =
        CVarDef.Create("station_ai.ssd_grace_period", 300f, CVar.SERVERONLY);

    /// <summary>
    ///     Duration of a POI capture operation, in minutes.
    /// </summary>
    public static readonly CVarDef<float> PoiCaptureDurationMinutes =
        CVarDef.Create("forge.poi_capture.duration_minutes", 10f, CVar.SERVERONLY);

    /// <summary>
    ///     Minimum time after a completed capture before a new capture may begin, in hours.
    /// </summary>
    public static readonly CVarDef<float> PoiCaptureRecaptureCooldownHours =
        CVarDef.Create("forge.poi_capture.recapture_cooldown_hours", 1f, CVar.SERVERONLY);

    /// <summary>
    ///     Default radius (in tiles) of the captured POI overlay shown on shuttle radars.
    /// </summary>
    /// <summary>
    ///     Capture zone radius in tiles (≈ meters on radar). Default 1024 matches the
    ///     outer rings on long-range helm radar so the owned POI area is visible.
    ///     Per-grid overrides (e.g. faction bases) may use up to 2048 via YAML.
    /// </summary>
    public static readonly CVarDef<float> PoiCaptureZoneRadiusTiles =
        CVarDef.Create("forge.poi_capture.zone_radius_tiles", 1024f, CVar.SERVERONLY);

    /// <summary>
    ///     Hard cap for POI capture zone radius in tiles (YAML roundstart + marker overrides).
    /// </summary>
    public static readonly CVarDef<float> PoiCaptureZoneRadiusMaxTiles =
        CVarDef.Create("forge.poi_capture.zone_radius_max_tiles", 2048f, CVar.SERVERONLY);

    /// <summary>
    ///     Default interval between POI treasury reward rolls, in minutes.
    ///     Per-treasury override via <c>rewardIntervalMinutes</c> in YAML.
    /// </summary>
    public static readonly CVarDef<float> PoiCaptureRewardIntervalMinutes =
        CVarDef.Create("forge.poi_capture.reward_interval_minutes", 10f, CVar.SERVERONLY);

    /// <summary>
    ///     Fraction of a sale price routed to the owning POI treasury (in addition to sector taxes).
    /// </summary>
    public static readonly CVarDef<float> PoiCaptureSalesTaxRate =
        CVarDef.Create("forge.poi_capture.sales_tax_rate", 0.1f, CVar.SERVERONLY);

    // Forge-Change-Start
    /// <summary>
    ///     Chat log font size in pixels. Independent from the global UI scale.
    /// </summary>
    public static readonly CVarDef<int> ChatFontSize =
        CVarDef.Create("forge.ui.chat_font_size", 12, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    ///     Examine tooltip font size in pixels. Independent from the global UI scale.
    /// </summary>
    public static readonly CVarDef<int> ExamineFontSize =
        CVarDef.Create("forge.ui.examine_font_size", 12, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    ///     Scale for HUD chrome: hotbar slots, hand slots, and action buttons.
    ///     1 is the default 64px slot size.
    /// </summary>
    public static readonly CVarDef<float> HudScale =
        CVarDef.Create("forge.ui.hud_scale", 1f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    ///     Scale for bag/belt storage grid tiles. Independent from HUD scale.
    /// </summary>
    public static readonly CVarDef<float> StorageScale =
        CVarDef.Create("forge.ui.storage_scale", 1f, CVar.CLIENTONLY | CVar.ARCHIVE);
    // Forge-Change-End
}
