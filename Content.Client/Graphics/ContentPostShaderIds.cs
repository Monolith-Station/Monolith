namespace Content.Client.Graphics;

internal static class ContentPostShaderIds
{
    public const string Stealth = "stealth";
    public const string Holopad = "holopad";
    public const string FloorOcclusion = "floor-occlusion";
    public const string Shielding = "shielding";
    public const string InteractionOutline = "interaction-outline";
    public const string TargetOutline = "target-outline";
    public const string DragDropOutline = "drag-drop-outline";

    public static readonly string[] BeforeOutlines = [InteractionOutline, TargetOutline, DragDropOutline, Shielding];
    public static readonly string[] AfterBaseEffects = [Stealth, FloorOcclusion, Holopad];
}
