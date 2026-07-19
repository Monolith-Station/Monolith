using Content.Shared._Mono.SectorCapture.Components;
using Content.Shared._Mono.SectorCapture.Prototypes;
using Content.Shared._Mono.Company;
using Content.Shared.Containers.ItemSlots;
using Content.Server.Radio.EntitySystems;
using Content.Shared.Popups;

namespace Content.Shared._Mono.SectorCapture;
public abstract partial class SectorCaptureSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager PrototypeManager = default!;

    [Dependency] private ItemSlotsSystem _itemSlotsSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
    }



}
