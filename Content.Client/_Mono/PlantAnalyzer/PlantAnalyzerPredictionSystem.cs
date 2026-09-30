using Content.Client.Botany.Components;
using Content.Shared._Mono.PlantAnalyzer;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Robust.Shared.Audio.Systems;

namespace Content.Client._Mono.PlantAnalyzer;

public sealed partial class PlantAnalyzerPredictionSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlantAnalyzerComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<PlantAnalyzerComponent, PlantAnalyzerDoAfterEvent>(OnDoAfter);
        Subs.BuiEvents<PlantAnalyzerComponent>(PlantAnalyzerUiKey.Key, subs =>
        {
            subs.Event<PlantAnalyzerSetMode>(OnSetMode);
            subs.Event<PlantAnalyzerSelectGene>(OnSelectGene);
            subs.Event<PlantAnalyzerSelectDatabankEntry>(OnSelectDatabankEntry);
        });
    }

    private void OnAfterInteract(Entity<PlantAnalyzerComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Target is not { } target || !args.CanReach || ent.Comp.DoAfter != null || !HasComp<SeedComponent>(target))
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
        if (args.Handled || args.Cancelled || !CanPredictSound(args.Mode, args.Gene))
            return;

        var sound = args.Mode == PlantAnalyzerMode.Scan
            ? ent.Comp.ScanningEndSound
            : ent.Comp.ExtractEndSound;
        _audio.PlayPredicted(sound, ent, args.User);
    }

    private void OnSetMode(Entity<PlantAnalyzerComponent> ent, ref PlantAnalyzerSetMode args)
    {
        if (ent.Comp.DoAfter != null || !Enum.IsDefined(args.Mode))
            return;

        ent.Comp.Mode = args.Mode;
        DirtyField(ent, ent.Comp, nameof(PlantAnalyzerComponent.Mode));
    }

    private void OnSelectGene(Entity<PlantAnalyzerComponent> ent, ref PlantAnalyzerSelectGene args)
    {
        if (ent.Comp.DoAfter != null || !Enum.IsDefined(args.Gene))
            return;

        ent.Comp.Gene = args.Gene;
        DirtyField(ent, ent.Comp, nameof(PlantAnalyzerComponent.Gene));
    }

    private void OnSelectDatabankEntry(Entity<PlantAnalyzerComponent> ent, ref PlantAnalyzerSelectDatabankEntry args)
    {
        if (ent.Comp.DoAfter != null || args.Index < 0)
            return;

        ent.Comp.DatabankIndex = args.Index;
        DirtyField(ent, ent.Comp, nameof(PlantAnalyzerComponent.DatabankIndex));
    }

    private static bool CanPredictSound(PlantAnalyzerMode mode, PlantGeneId gene)
        => mode == PlantAnalyzerMode.Scan ||
            mode == PlantAnalyzerMode.Extract && gene < PlantGeneId.ConsumeGases;
}
