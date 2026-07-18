using Content.Shared._Mono.SectorCapture.Components;
using Content.Shared._Mono.SectorCapture.Prototypes;
using Content.Shared.Research;
using Content.Shared._Mono.Company;
using Content.Shared._NF.Bank;
using Content.Shared.Containers.ItemSlots;
using System.Runtime.CompilerServices;
using Content.Shared.Lathe;

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
