using Content.Shared._Mono.PlantAnalyzer;
using JetBrains.Annotations;

namespace Content.Client._Mono.PlantAnalyzer.UI;

[UsedImplicitly]
public sealed class PlantAnalyzerBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private PlantAnalyzerWindow? _window;
    private uint _nextRequestId;
    private uint? _pendingModeRequestId;
    private uint? _pendingGeneRequestId;
    private uint? _pendingDatabankRequestId;

    public PlantAnalyzerBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = new PlantAnalyzerWindow(this)
        {
            Title = Loc.GetString("plant-analyzer-interface-title"),
        };
        _window.OnClose += Close;
        _window.OpenCenteredLeft();
        SetMode(PlantAnalyzerMode.Scan);
        SendMessage(new PlantAnalyzerRequestState());
    }

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        if (_window == null)
            return;

        switch (message)
        {
            case PlantAnalyzerScannedSeedPlantInformation scan:
                _window.Populate(scan);
                break;
            case PlantAnalyzerControlState state:
                var applyMode = ApplyState(state.ModeRequestId, ref _pendingModeRequestId);
                var applyGene = ApplyState(state.GeneRequestId, ref _pendingGeneRequestId);
                var applyDatabank = ApplyState(state.DatabankRequestId, ref _pendingDatabankRequestId);
                _window.Populate(state, applyMode, applyGene, applyDatabank);
                break;
        }
    }

    public void SetMode(PlantAnalyzerMode mode)
    {
        _window?.SetMode(mode);
        _pendingModeRequestId = ++_nextRequestId;
        SendPredictedMessage(new PlantAnalyzerSetMode(mode, _pendingModeRequestId.Value));
    }

    public void SelectGene(PlantGeneId gene)
    {
        _pendingGeneRequestId = ++_nextRequestId;
        SendPredictedMessage(new PlantAnalyzerSelectGene(gene, _pendingGeneRequestId.Value));
    }

    public void SelectDatabankEntry(int index)
    {
        _pendingDatabankRequestId = ++_nextRequestId;
        SendPredictedMessage(new PlantAnalyzerSelectDatabankEntry(index, _pendingDatabankRequestId.Value));
    }

    public void DeleteDatabankEntry()
        => SendPredictedMessage(new PlantAnalyzerDeleteDatabankEntry());

    private static bool ApplyState(uint requestId, ref uint? pending)
    {
        if (requestId == 0)
            return pending == null;

        if (requestId != pending)
            return false;

        pending = null;
        return true;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
            return;

        if (_window != null)
            _window.OnClose -= Close;

        _window?.Dispose();
    }
}
