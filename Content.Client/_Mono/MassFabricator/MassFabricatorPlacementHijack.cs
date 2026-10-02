using System.Linq;
using Content.Shared._Mono.MassFabricator;
using Content.Shared.Maps;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Placement;
using Robust.Client.ResourceManagement;
using Robust.Shared.ContentPack;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Client._Mono.MassFabricator;

public sealed class MassFabricatorPlacementHijack(
    MassFabricatorClientSystem system,
    MassFabricatorBlueprintPrototype blueprint,
    string target,
    IPrototypeManager prototypes,
    IResourceCache resources,
    SpriteSystem sprites) : PlacementHijack
{
    public override bool CanRotate => blueprint.Kind == MassFabricatorBlueprintKind.Entity;

    public override bool HijackPlacementRequest(EntityCoordinates coordinates)
    {
        system.Place(coordinates, CanRotate ? Manager.Direction.ToAngle() : Angle.Zero);
        return true;
    }

    public override void StartHijack(PlacementManager manager)
    {
        base.StartHijack(manager);
        if (!CanRotate)
            manager.Direction = Direction.South;
        if (blueprint.Kind == MassFabricatorBlueprintKind.Tile &&
            prototypes.TryIndex<ContentTileDefinition>(target, out var tile) && tile.Sprite is { } path)
        {
            manager.CurrentTextures = new List<IDirectionalTextureProvider> { resources.GetResource<TextureResource>(path).Texture };
        }
        else if (blueprint.Kind == MassFabricatorBlueprintKind.Entity &&
                 prototypes.TryIndex<EntityPrototype>(target, out var proto))
        {
            manager.CurrentTextures = sprites.GetPrototypeTextures(proto).ToList();
        }
    }
}
