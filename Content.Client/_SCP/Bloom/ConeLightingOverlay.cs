using System.Numerics;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using DrawDepth = Content.Shared.DrawDepth.DrawDepth;

namespace Content.Client._Scp.Shaders.Bloom;

public sealed class ConeLightingOverlay : Overlay
{
    public override OverlaySpace Space => OverlaySpace.WorldSpaceEntities;
    public override bool RequestScreenTexture => true;

    private readonly ShaderInstance _shader;

    private readonly Texture _maskTexture;
    private readonly Vector2 _maskOffset;

    public List<(TransformComponent xform, Matrix3x2 matrix, Vector2 worldPos, Color color)> Entities = [];
    public bool Enabled;
    public float Strength = 0.5f;

    public ConeLightingOverlay(IPrototypeManager prototypeManager, SpriteSystem spriteSystem, ProtoId<ShaderPrototype> shader)
    {
        _shader = prototypeManager.Index(shader).InstanceUnique();
        ZIndex = (int) DrawDepth.Effects;

        _maskTexture = spriteSystem.Frame0(BloomOverlayVisualsComponent.Cone);

        var xOffset = BloomOverlayVisualsComponent.ConeOffset.X - (_maskTexture.Width / 2f) / EyeManager.PixelsPerMeter;
        var yOffset = BloomOverlayVisualsComponent.ConeOffset.Y - (_maskTexture.Height / 2f) / EyeManager.PixelsPerMeter;
        _maskOffset = new Vector2(xOffset, yOffset);
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        base.BeforeDraw(in args);

        return Enabled;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null)
            return;

        var handle = args.WorldHandle;
        var bounds = args.WorldAABB.Enlarged(5f);

        _shader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _shader.SetParameter("base_haze", BloomOverlayVisualsComponent.DefaultConeBaseHaze);
        _shader.SetParameter("hueta_divisor", BloomOverlayVisualsComponent.DefaultConeHuetaDivisor / Strength);
        handle.UseShader(_shader);

        foreach (var (xform, matrix, worldPos, color) in Entities)
        {
            if (xform.MapID != args.MapId)
                continue;

            if (!bounds.Contains(worldPos))
                continue;

            handle.SetTransform(matrix);
            handle.DrawTexture(_maskTexture, _maskOffset, color);
        }

        handle.UseShader(null);
        handle.SetTransform(Matrix3x2.Identity);
    }
}
