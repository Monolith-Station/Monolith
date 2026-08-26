using System.Linq;
using Content.Server.Construction;
using Content.Server.Construction.Completions;
using Content.Shared.Construction;
using Content.Shared.Materials;
using Content.Shared.Popups;

namespace Content.Server._Mono.Construction;

/// <summary>
/// mald system
/// </summary>
public sealed partial class FissileDeconstructionSystem : EntitySystem
{
    [Dependency] private SharedMaterialStorageSystem _materialStorage = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    private const string FissileUranium = "UraniumFissile";
    private const int CriticalMass = 600;
    private const float TimeMultiplier = 10f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MaterialStorageComponent, GetConstructionToolUseDurationEvent>(OnGetDuration);
        SubscribeLocalEvent<MaterialStorageComponent, ConstructionToolUseStartedEvent>(OnToolUseStarted);
    }

    private void OnGetDuration(Entity<MaterialStorageComponent> ent, ref GetConstructionToolUseDurationEvent args)
    {
        if (IsHazardous(ent, args.Edge))
            args.Duration *= TimeMultiplier;
    }

    private void OnToolUseStarted(Entity<MaterialStorageComponent> ent, ref ConstructionToolUseStartedEvent args)
    {
        if (!IsHazardous(ent, args.Edge))
            return;

        _popup.PopupEntity(Loc.GetString("construction-fissile-deconstruction-warning"),
            ent,
            args.User,
            PopupType.LargeCaution);
    }

    private bool IsHazardous(Entity<MaterialStorageComponent> ent, ConstructionGraphEdge edge)
    {
        return edge.Completed.Any(action => action is RaiseEvent { Event: MachineDeconstructedEvent })
            && ent.Comp.DropOnDeconstruct
            && _materialStorage.GetMaterialAmount(ent, FissileUranium, ent.Comp, true) >= CriticalMass;
    }
}
