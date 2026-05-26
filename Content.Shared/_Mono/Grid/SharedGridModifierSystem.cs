namespace Content.Shared._Mono.Grid;

/// <summary>
/// Methods required from this system to be used in GridModifiers logic.
/// </summary>
public abstract class SharedGridModifierSystem : EntitySystem
{
    public void GetGridEntities(EntityUid gridUid, HashSet<Entity<IComponent>> entities, Type compType)
    {
        foreach (var (uid, comp) in EntityManager.GetAllComponents(compType, true))
        {

            var xform = Transform(uid);

            if (xform.GridUid != gridUid)
                continue;

            entities.Add((uid, comp));
        }
    }
}
