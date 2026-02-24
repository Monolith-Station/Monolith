using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._Mono.Grid;

/// <summary>
/// This handles grid modification on initialization.
/// </summary>
public sealed class GridModifierSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _protoMan = default!;
    [Dependency] private readonly IComponentFactory _factory = default!;

    private EntityQuery<TransformComponent> _xformQuery;
    private EntityQuery<MetaDataComponent> _metaQuery;
    public override void Initialize()
    {
        SubscribeLocalEvent<GridModifierComponent, MapInitEvent>(OnInit);

        _xformQuery = GetEntityQuery<TransformComponent>();
        _metaQuery = GetEntityQuery<MetaDataComponent>();
    }

    private void OnInit(EntityUid uid, GridModifierComponent component, MapInitEvent args)
    {
        ModifyGrid(uid, component.Modifications);
    }

    public void ModifyGrid(EntityUid uid, List<ProtoId<GridModificationPrototype>> modifiers)
    {
        if (!HasComp<MapGridComponent>(uid))
            return;

        foreach (var modProto  in modifiers)
        {
            if (!_protoMan.TryIndex(modProto, out var mod))
                continue;

            foreach (var modifier in mod.Modifiers)
            {
                var comp = _factory.GetComponent(modifier.Comp);
                var ents = new HashSet<Entity<IComponent>>();

                GetGridEntities(uid, ents, comp.GetType());

                foreach (var ent in ents)
                {
                    modifier.Modify(ent, _metaQuery.Get(ent), _xformQuery.Get(ent), EntityManager);
                }
            }
        }
    }

    private void GetGridEntities(EntityUid gridUid, HashSet<Entity<IComponent>> entities, Type compType)
    {
        foreach (var (uid, comp) in EntityManager.GetAllComponents(compType, true))
        {

            var xform = _xformQuery.GetComponent(uid);

            if (xform.GridUid != gridUid)
                continue;

            entities.Add((uid, comp));
        }
    }
}
