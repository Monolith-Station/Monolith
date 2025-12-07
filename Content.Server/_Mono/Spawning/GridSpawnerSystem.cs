using Content.Shared.Random.Helpers;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server._Mono.Spawning;

public sealed partial class GridSpawnerSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MapLoaderSystem _loader = default!;
    [Dependency] private readonly MetaDataSystem _metadata = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GridSpawnerComponent, MapInitEvent>(OnInit);
    }

    private void OnInit(Entity<GridSpawnerComponent> ent, ref MapInitEvent args)
    {
        var xform = Transform(ent.Owner);

        if (_loader.TryLoadGrid(xform.MapID, ent.Comp.Path, out var grid, offset: _transform.GetWorldPosition(xform)))
        {
            if (ent.Comp.NameGrid)
            {
                if (_proto.TryIndex(ent.Comp.NameDataset, out var dataset))
                {
                    _metadata.SetEntityName(grid.Value, _random.Pick(dataset));
                }
                else
                {
                    var name = ent.Comp.Path.FilenameWithoutExtension;
                    _metadata.SetEntityName(grid.Value, name);
                }
            }

            EntityManager.AddComponents(grid.Value, ent.Comp.AddComponents);
        }
    }
}
