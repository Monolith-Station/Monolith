using Content.Shared._Mono.Sprite;
using Robust.Client.GameObjects;

namespace Content.Client._Mono.Sprite;

public sealed class WorldSpaceSpriteSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<WorldSpaceSpriteComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<WorldSpaceSpriteComponent, MoveEvent>(OnMove);
    }

    private void OnStartup(Entity<WorldSpaceSpriteComponent> ent, ref ComponentStartup args) => UpdateSprite(ent);

    private void OnMove(Entity<WorldSpaceSpriteComponent> ent, ref MoveEvent args)
    {
        if (args.OldRotation != args.NewRotation)
            UpdateSprite(ent);
    }

    private void UpdateSprite(EntityUid uid)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        sprite.EnableDirectionOverride = true;
        sprite.DirectionOverride = Transform(uid).LocalRotation.GetCardinalDir();
    }
}
