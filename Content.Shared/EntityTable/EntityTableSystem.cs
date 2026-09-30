using Content.Shared.EntityTable.EntitySelectors;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared.EntityTable;

public sealed partial class EntityTableSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private IRobustRandom _random = default!;

    public IEnumerable<EntProtoId> GetSpawns(EntityTableSelector? table, System.Random? rand = null)
    {
        if (table == null)
            return new List<EntProtoId>();

        rand ??= _random.GetRandom();
        return table.GetSpawns(rand, EntityManager, _prototypeManager);
    }
}
