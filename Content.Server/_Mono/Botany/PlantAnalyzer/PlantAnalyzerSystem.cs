using Content.Server.Botany;
using Content.Server.Botany.Components;
using Content.Shared._Mono.PlantAnalyzer;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Mono.Botany.PlantAnalyzer;

public sealed partial class PlantAnalyzerSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly ItemToggleSystem _toggle = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly TransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlantAnalyzerComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<PlantAnalyzerComponent, PlantAnalyzerDoAfterEvent>(OnDoAfter);
        SubscribeLocalEvent<PlantAnalyzerComponent, EntGotInsertedIntoContainerMessage>(OnStored);
        SubscribeLocalEvent<PlantAnalyzerComponent, ItemToggledEvent>(OnToggled);
        SubscribeLocalEvent<PlantAnalyzerComponent, DroppedEvent>(OnDropped);
        Subs.BuiEvents<PlantAnalyzerComponent>(PlantAnalyzerUiKey.Key, subs =>
        {
            subs.Event<BoundUIClosedEvent>(OnUiClosed);
            subs.Event<PlantAnalyzerSetMode>(OnSetMode);
            subs.Event<PlantAnalyzerSelectGene>(OnSelectGene);
            subs.Event<PlantAnalyzerSelectDatabankEntry>(OnSelectDatabankEntry);
            subs.Event<PlantAnalyzerDeleteDatabankEntry>(OnDeleteDatabankEntry);
            subs.Event<PlantAnalyzerRequestState>(OnRequestState);
        });
    }

    private void OnAfterInteract(Entity<PlantAnalyzerComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Target is not { } target || !args.CanReach || ent.Comp.DoAfter != null || !IsPlantTarget(target))
            return;

        var operation = new PlantAnalyzerDoAfterEvent(ent.Comp.Mode, ent.Comp.Gene, ent.Comp.DatabankIndex);
        var delay = operation.Mode == PlantAnalyzerMode.Scan
            ? ent.Comp.Settings.ScanDelay
            : ent.Comp.Settings.ModeDelay;
        var doAfter = new DoAfterArgs(EntityManager, args.User, delay, operation, ent, target: target, used: ent)
        {
            NeedHand = true,
            BreakOnDamage = true,
            BreakOnMove = true,
            MovementThreshold = 0.01f
        };
        _doAfter.TryStartDoAfter(doAfter, out ent.Comp.DoAfter);
    }

    private void OnDoAfter(Entity<PlantAnalyzerComponent> ent, ref PlantAnalyzerDoAfterEvent args)
    {
        ent.Comp.DoAfter = null;
        if (args.Handled || args.Cancelled || args.Args.Target is not { } target)
            return;

        var success = args.Mode switch
        {
            PlantAnalyzerMode.Scan => Scan(ent, target),
            PlantAnalyzerMode.DeleteMutations => ClearMutations(target),
            PlantAnalyzerMode.Extract => ExtractGene(ent, target, args.Gene),
            PlantAnalyzerMode.Implant => ImplantGene(ent, target, args.DatabankIndex),
            _ => false
        };

        if (!success)
            return;

        var sound = args.Mode switch
        {
            PlantAnalyzerMode.Scan => ent.Comp.ScanningEndSound,
            PlantAnalyzerMode.DeleteMutations => ent.Comp.DeleteMutationEndSound,
            PlantAnalyzerMode.Extract => ent.Comp.ExtractEndSound,
            PlantAnalyzerMode.Implant => ent.Comp.InjectEndSound,
            _ => null
        };
        if (HasComp<SeedComponent>(target) && CanPredictSound(args.Mode, args.Gene))
            _audio.PlayPredicted(sound, ent, args.User);
        else
            _audio.PlayPvs(sound, ent);
        OpenUserInterface(args.User, ent);
        if (args.Mode == PlantAnalyzerMode.Scan && TryGetSeed(target, out var seed, out var tray))
            SendScanState(ent, seed, target, tray);
        SendControlState(ent);
        args.Handled = true;
    }

    private bool Scan(Entity<PlantAnalyzerComponent> ent, EntityUid target)
    {
        if (!TryGetSeed(target, out _, out _))
            return false;

        ent.Comp.ScannedEntity = target;
        ent.Comp.NextUpdate = TimeSpan.Zero;
        _toggle.TryActivate(ent.Owner);
        return true;
    }

    private void OpenUserInterface(EntityUid user, EntityUid analyzer)
    {
        if (!TryComp<ActorComponent>(user, out var actor) || !_ui.HasUi(analyzer, PlantAnalyzerUiKey.Key))
            return;

        _ui.OpenUi(analyzer, PlantAnalyzerUiKey.Key, actor.PlayerSession);
    }

    private void OnUiClosed(Entity<PlantAnalyzerComponent> ent, ref BoundUIClosedEvent args)
    {
        if (!_ui.IsUiOpen(ent.Owner, PlantAnalyzerUiKey.Key))
            _toggle.TryDeactivate(ent.Owner);
    }

    private void OnStored(Entity<PlantAnalyzerComponent> ent, ref EntGotInsertedIntoContainerMessage args)
    {
        if (ent.Comp.ScannedEntity != null)
            _toggle.TryDeactivate(ent.Owner);
    }

    private void OnDropped(Entity<PlantAnalyzerComponent> ent, ref DroppedEvent args)
    {
        if (ent.Comp.ScannedEntity != null)
            _toggle.TryDeactivate(ent.Owner);
    }

    private void OnToggled(Entity<PlantAnalyzerComponent> ent, ref ItemToggledEvent args)
    {
        if (!args.Activated && ent.Comp.ScannedEntity != null)
            StopScanning(ent);
    }

    private void StopScanning(Entity<PlantAnalyzerComponent> ent)
    {
        ent.Comp.ScannedEntity = null;
        _ui.CloseUi(ent.Owner, PlantAnalyzerUiKey.Key);
        _toggle.TryDeactivate(ent.Owner);
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<PlantAnalyzerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (comp.ScannedEntity is not { } target || _timing.CurTime < comp.NextUpdate)
                continue;

            comp.NextUpdate = _timing.CurTime + TimeSpan.FromSeconds(1);
            if (!Exists(target) || !IsPlantTarget(target) ||
                !_transform.InRange(Transform(target).Coordinates, xform.Coordinates, comp.MaxScanRange))
            {
                StopScanning((uid, comp));
                continue;
            }

            if (TryGetSeed(target, out var seed, out var tray))
                SendScanState((uid, comp), seed, target, tray);
        }
    }

    private bool IsPlantTarget(EntityUid target)
        => HasComp<SeedComponent>(target) ||
            TryComp<PlantHolderComponent>(target, out var holder) && holder.Seed != null;

    private static bool CanPredictSound(PlantAnalyzerMode mode, PlantGeneId gene)
        => mode == PlantAnalyzerMode.Scan ||
            mode == PlantAnalyzerMode.Extract && gene < PlantGeneId.ConsumeGases;

    private bool TryGetSeed(EntityUid target, out SeedData seed, out bool tray)
    {
        tray = false;
        if (TryComp<SeedComponent>(target, out var packet))
        {
            if (packet.Seed != null)
            {
                seed = packet.Seed;
                return true;
            }

            if (packet.SeedId != null && _prototypeManager.TryIndex(packet.SeedId, out SeedPrototype? prototype))
            {
                seed = prototype;
                return true;
            }
        }
        else if (TryComp<PlantHolderComponent>(target, out var holder) && holder.Seed != null)
        {
            seed = holder.Seed;
            tray = true;
            return true;
        }

        seed = default!;
        return false;
    }

    private void OnSetMode(Entity<PlantAnalyzerComponent> ent, ref PlantAnalyzerSetMode args)
    {
        if (ent.Comp.DoAfter != null || ent.Comp.Mode == args.Mode || !Enum.IsDefined(args.Mode))
        {
            SendControlState(ent, args.RequestId);
            return;
        }

        ent.Comp.Mode = args.Mode;
        if (args.Mode != PlantAnalyzerMode.Scan && ent.Comp.ScannedEntity != null)
        {
            ent.Comp.ScannedEntity = null;
            _toggle.TryDeactivate(ent.Owner);
        }
        DirtyField(ent, ent.Comp, nameof(PlantAnalyzerComponent.Mode));
        SendControlState(ent, args.RequestId);
    }

    private void OnSelectGene(Entity<PlantAnalyzerComponent> ent, ref PlantAnalyzerSelectGene args)
    {
        if (ent.Comp.DoAfter != null || !Enum.IsDefined(args.Gene))
        {
            SendControlState(ent, geneRequestId: args.RequestId);
            return;
        }

        ent.Comp.Gene = args.Gene;
        DirtyField(ent, ent.Comp, nameof(PlantAnalyzerComponent.Gene));
        SendControlState(ent, geneRequestId: args.RequestId);
    }

    private void OnSelectDatabankEntry(Entity<PlantAnalyzerComponent> ent, ref PlantAnalyzerSelectDatabankEntry args)
    {
        if (ent.Comp.DoAfter != null || args.Index < 0 || args.Index >= DatabankCount(ent.Comp))
        {
            SendControlState(ent, databankRequestId: args.RequestId);
            return;
        }

        ent.Comp.DatabankIndex = args.Index;
        DirtyField(ent, ent.Comp, nameof(PlantAnalyzerComponent.DatabankIndex));
        SendControlState(ent, databankRequestId: args.RequestId);
    }

    private void OnDeleteDatabankEntry(Entity<PlantAnalyzerComponent> ent, ref PlantAnalyzerDeleteDatabankEntry args)
    {
        if (ent.Comp.DoAfter != null)
            return;

        DeleteDatabankEntry(ent);
        SendControlState(ent);
    }

    private void OnRequestState(Entity<PlantAnalyzerComponent> ent, ref PlantAnalyzerRequestState args)
        => SendControlState(ent);

    private void SendControlState(
        Entity<PlantAnalyzerComponent> ent,
        uint modeRequestId = 0,
        uint geneRequestId = 0,
        uint databankRequestId = 0)
    {
        var state = new PlantAnalyzerControlState(
            ent.Comp.Mode,
            modeRequestId,
            ent.Comp.Gene,
            geneRequestId,
            ent.Comp.DatabankIndex,
            databankRequestId,
            ent.Comp.GeneBank.ToArray(),
            ent.Comp.ConsumeGasBank.ToArray(),
            ent.Comp.ExudeGasBank.ToArray(),
            ent.Comp.ChemicalBank.ToArray());
        _ui.ServerSendUiMessage(ent.Owner, PlantAnalyzerUiKey.Key, state);
    }
}
