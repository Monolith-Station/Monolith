using Content.Shared._Mono.Grid;
using Content.Shared._Mono.ShipRepair;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._Mono.Grid;

/// <summary>
/// This handles grid modification on initialization.
/// </summary>
public sealed class GridModifierSystem : SharedGridModifierSystem
{
    [Dependency] private readonly IPrototypeManager _protoMan = default!;
    [Dependency] private readonly IComponentFactory _factory = default!;
    [Dependency] private readonly SharedShipRepairSystem _repair = default!;

    private List<EntityUid> _snapQueue = [];

    public override void Initialize()
    {
        SubscribeLocalEvent<GridModifierComponent, MapInitEvent>(OnInit);
    }

    public override void Update(float frameTime)
    {
        foreach (var uid in _snapQueue)
        {
            _repair.GenerateRepairData(uid);
        }
        _snapQueue.Clear();
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
                modifier.Modify(uid, EntityManager, _factory);
            }
        }

        _snapQueue.Add(uid);
    }
}
