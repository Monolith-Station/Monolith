using Robust.Shared.Configuration;

namespace Content.Shared._Scp.ScpCCVars;

[CVarDefs]
public sealed partial class ScpCCVars
{
    /**
     * Shader
     */

    /// <summary>
    /// Выключен ли шейдер зернистости? // Shitty translation, dont take as fact - "Is the grain shader disabled?"
    /// </summary>
    public static readonly CVarDef<bool> GrainToggleOverlay =
        CVarDef.Create("shader.grain_toggle_overlay", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Сила шейдера зернистости // Shitty translation, dont take as fact - "The power of the grain shader"
    /// </summary>
    public static readonly CVarDef<int> GrainStrength =
        CVarDef.Create("shader.grain_strength", 140, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Будет ли использовать альтернативный метод просчета сущностей для поля зрения // Shitty translation, dont take as fact - "Will an alternative method be used to calculate entities within the field of view?"
    /// </summary>
    /*
     * Свечение лампочек // Shitty translation, dont take as fact - "The Glow of Lightbulbs" - mystical as shit man
     */

    /// <summary>
    /// Будет ли использоваться эффект свечения у лампочек? // Shitty translation, dont take as fact - "Will the light bulbs feature a glow effect?"
    /// Отвечает за главный рубильник для двух опций настройки. // Shitty translation, dont take as fact - "Responsible for the master switch for two configuration options."
    /// </summary>
    public static readonly CVarDef<bool> LightBloomEnable =
        CVarDef.Create("scp.light_bloom_enable", false, CVar.CLIENTONLY | CVar.ARCHIVE); // Mono - false by default, keeping it incase anyone actually wants to use this; I personally think it looks really weird.

    /// <summary>
    /// Будет ли отображаться конус у эффекта свечения лампочек? // Shitty translation, dont take as fact - "Will the light cone be displayed for the light bulb glow effect?"
    /// </summary>
    public static readonly CVarDef<bool> LightBloomConeEnable =
        CVarDef.Create("scp.light_bloom_cone_enable", false, CVar.CLIENTONLY | CVar.ARCHIVE); // Mono - false by default

    /// <summary>
    /// При включении не рисуется на невидимых для игрока позициях эффект, что увеличивает производительность ТОЛЬКО на слабых видеокартах. // Shitty translation, dont take as fact - "When enabled, effects are not rendered at positions invisible to the player; this improves performance ONLY on low-end graphics cards."
    /// В остальных случаях снижает FPS из-за проверок на видимость. Поэтому это опционально. // Shitty translation, dont take as fact - "In other cases, it reduces FPS due to visibility checks. Therefore, it is optional."
    /// </summary>
    public static readonly CVarDef<bool> LightBloomOptimizations =
        CVarDef.Create("scp.light_bloom_optimizations", true, CVar.CLIENTONLY | CVar.ARCHIVE); // Mono - true by default

    /// <summary>
    /// Определяет силу эффекта свечения. // Shitty translation, dont take as fact - "Determines the strength of the glow effect."
    /// Чем выше сила, тем сильнее эффект. Отображается в процентах от 0% до 100% // Shitty translation, dont take as fact - "The higher the power, the stronger the effect. Displayed as a percentage ranging from 0% to 100%."
    /// </summary>
    public static readonly CVarDef<float> LightBloomStrength =
        CVarDef.Create("scp.light_bloom_strength", 0.25f, CVar.CLIENTONLY | CVar.ARCHIVE); // Mono - change from 0.5
}
