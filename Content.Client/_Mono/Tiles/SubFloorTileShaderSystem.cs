using Content.Shared._Mono.Tiles;
using Content.Shared.SubFloor;

namespace Content.Client._Mono.Tiles;

/// <summary>
/// It's 30 lines, figure it out.
/// </summary>
public sealed partial class SubFloorTileShaderSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SubFloorHideComponent, ComponentInit>(OnInit);
    }

    private void OnInit(Entity<SubFloorHideComponent> ent, ref ComponentInit args)
    {
        if (!IsBuried(ent))
            return;

        EnsureComp<TileShaderTargetComponent>(ent);
    }

    private bool IsBuried(Entity<SubFloorHideComponent> ent)
    {
        return ent.Comp.VisibleLayers.Count == 0;
    }
}
