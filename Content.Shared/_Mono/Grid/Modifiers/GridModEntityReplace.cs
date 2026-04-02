using Content.Shared.Whitelist;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared._Mono.Grid.Modifiers;

[UsedImplicitly]
public sealed partial class GridModEntityReplace : GridModifier
{
    [DataField(required: true)]
    public List<ReplaceData> Data = [];

    [Dependency] private readonly IRobustRandom _random = new RobustRandom();

    public override void Modify(EntityUid gridUid, EntityManager system, IComponentFactory? factory = null)
    {
        if (factory == null)
            return;

        var whitelistSystem = system.System<EntityWhitelistSystem>();
        var gridModSystem = system.System<SharedGridModifierSystem>();

        var comp = factory.GetComponent(Comp);
        var ents = new HashSet<Entity<IComponent>>();

        gridModSystem.GetGridEntities(gridUid, ents, comp.GetType());

        foreach (var ent in ents)
        {
            var meta = system.MetaQuery.GetComponent(ent);
            var xform = system.TransformQuery.GetComponent(ent);

            if (meta.EntityPrototype == null)
                continue;

            foreach (var rD in Data)
            {
                if (whitelistSystem.IsWhitelistFailOrNull(rD.Whitelist, ent) && meta.EntityPrototype.ID != rD.ToReplace)
                    continue;

                if (!_random.Prob(rD.Chance))
                    continue;

                var pos = xform.Coordinates;

                system.QueueDeleteEntity(ent);
                system.SpawnAtPosition(rD.ReplaceWith, pos);

                break;
            }
        }
    }

}

[DataDefinition]
[Serializable]
public sealed partial class ReplaceData
{
    [DataField]
    public EntityWhitelist? Whitelist;

    [DataField]
    public EntProtoId? ToReplace;

    [DataField(required: true)]
    public EntProtoId ReplaceWith;

    [DataField]
    public float Chance = 0.2f;
}
