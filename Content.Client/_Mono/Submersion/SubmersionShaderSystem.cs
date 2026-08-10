using Content.Client._Mono.Tiles;
using Content.Shared._Mono.Submersion;
using Content.Shared._Mono.Tiles;
using Content.Shared.Standing;
using Content.Shared.SubFloor;

namespace Content.Client._Mono.Submersion;

/// <summary>
/// Tells a liquid tile's shader how far under the surface the entity on it is.
/// </summary>
public sealed partial class SubmersionShaderSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SubmergedComponent, ComponentInit>(OnSubmerged);
        SubscribeLocalEvent<TileShaderTargetComponent, TileShaderParametersEvent>(OnParameters);
    }

    /// <summary>
    /// This system relies on tileshaders.
    /// </summary>
    private void OnSubmerged(Entity<SubmergedComponent> ent, ref ComponentInit args)
    {
        EnsureComp<TileShaderTargetComponent>(ent);
    }

    private void OnParameters(Entity<TileShaderTargetComponent> ent, ref TileShaderParametersEvent args)
    {
        var under = HasComp<SubmergedComponent>(ent);

        var hiding = under && HasComp<StandingStateComponent>(ent);

        args.Shader.SetParameter("SUBMERGED", hiding ? 1f : 0f);
        args.Shader.SetParameter("COVERED", !hiding && (under || HasComp<SubFloorHideComponent>(ent)) ? 1f : 0f);
    }
}
