using Content.Shared._Goobstation.MouseSwarm;
using Robust.Client.GameObjects;
using Robust.Shared.Analyzers;
using Robust.Shared.Utility;

namespace Content.Client._Goobstation.MouseSwarm;

public sealed partial class MouseSwarmSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;

    private const string LayerPrefix = "mouse-swarm-";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MouseSwarmComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<MouseSwarmComponent, AfterAutoHandleStateEvent>(OnHandleState);
    }

    private void OnStartup(Entity<MouseSwarmComponent> ent, ref ComponentStartup args)
    {
        UpdateLayers(ent);
    }

    private void OnHandleState(Entity<MouseSwarmComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        UpdateLayers(ent);
    }

    private void UpdateLayers(Entity<MouseSwarmComponent> ent)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        var spriteEnt = (ent.Owner, sprite);

        // Remove all previously generated swarm layers, including optional tails.
        // RemoveLayer returns false when the mapped layer does not exist.
        for (var i = 0; i < ent.Comp.MaxMembers; i++)
        {
            _sprite.RemoveLayer(spriteEnt, LayerPrefix + i + "-tail", false);
            _sprite.RemoveLayer(spriteEnt, LayerPrefix + i, false);
        }

        // Rebuild layers for every absorbed swarm member.
        for (var i = 0; i < ent.Comp.Members.Count; i++)
        {
            var member = ent.Comp.Members[i];

            // Main body layer.
            var index = _sprite.AddLayer(
                spriteEnt,
                new SpriteSpecifier.Rsi(member.Sprite, member.State));

            _sprite.LayerMapSet(spriteEnt, LayerPrefix + i, index);
            _sprite.LayerSetOffset(spriteEnt, index, member.Offset);

            // Optional extra layer, such as OwOroach's animated tail.
            if (member.ExtraState is { } extraState)
            {
                var extraIndex = _sprite.AddLayer(
                    spriteEnt,
                    new SpriteSpecifier.Rsi(member.Sprite, extraState));

                _sprite.LayerMapSet(spriteEnt, LayerPrefix + i + "-tail", extraIndex);
                _sprite.LayerSetOffset(spriteEnt, extraIndex, member.Offset);
            }
        }
    }
}