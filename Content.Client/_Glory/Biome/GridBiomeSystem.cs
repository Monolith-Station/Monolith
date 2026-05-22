using Content.Shared._Glory.Biome;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Client._Glory.Biome;

/// <summary>
/// This handles...
/// </summary>
public sealed partial class GridBiomeSystem : EntitySystem
{
    /// <inheritdoc/>

    [Dependency] private readonly IPrototypeManager _protoMan = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    public override void Initialize()
    {
        SubscribeLocalEvent<BiomeEntityComponent, ComponentStartup>(OnComponentInit);
    }

    private void OnComponentInit(Entity<BiomeEntityComponent> ent, ref ComponentStartup args)
    {

        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        var grid = Transform(ent).GridUid;
        if (!grid.HasValue ||
            !TryComp<GridBiomeComponent>(grid.Value, out var gridB) ||
            !_protoMan.TryIndex(
                ent.Comp.BiomePrototypes.Find(proto => proto.Id == gridB.BiomePrototype.Id),
                out var biomeProto
            ))
            return;

        _sprite.LayerSetRsiState((ent.Owner, sprite), 0, biomeProto.StateName);

        if (!ent.Comp.ProcessSpriteDirections)
            return;

        _sprite.LayerSetRsiState((ent.Owner, sprite), 1, biomeProto.StateName + "_south");
        _sprite.LayerSetRsiState((ent.Owner, sprite), 2, biomeProto.StateName + "_east");
        _sprite.LayerSetRsiState((ent.Owner, sprite), 3, biomeProto.StateName + "_north");
        _sprite.LayerSetRsiState((ent.Owner, sprite), 4, biomeProto.StateName + "_west");
    }
}
