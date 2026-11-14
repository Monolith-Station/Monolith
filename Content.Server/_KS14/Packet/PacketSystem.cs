using System.Linq;
using Content.Server._FarHorizons.Power.Generation.FissionGenerator;
using Content.Server._KS14.Packet.Components;
using Content.Server._KS14.Packet.Modules.Base;
using Content.Server._KS14.Packet.States;
using Content.Server.Ame.EntitySystems;
using Content.Server.Atmos.Piping.Binary.EntitySystems;
using Content.Server.Chat.Systems;
using Content.Server.Chemistry.EntitySystems;
using Content.Server.DeviceLinking.Systems;
using Content.Server.Fax;
using Content.Server.GameTicking.Events;
using Content.Server.Lathe;
using Content.Server.Materials;
using Content.Server.Radio.EntitySystems;
using Content.Server.VendingMachines;
using Content.Shared._KS14.Packets.BUI;
using Content.Shared._KS14.Packets.Widgets;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.GameTicking;
using Content.Shared.Lathe;
using Content.Shared.Materials;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._KS14.Packet;

/// <summary>
/// Handles events and network.
/// </summary>
public sealed partial class PacketSystem : EntitySystem
{
    [Dependency] internal LatheSystem LatheSystem = default!;
    [Dependency] internal MaterialStorageSystem MaterialStorageSystem = default!;
    [Dependency] internal VendingMachineSystem VendingMachineSystem = default!;
    [Dependency] internal FaxSystem FaxSystem = default!;
    [Dependency] internal ChatSystem ChatSystem = default!;
    [Dependency] internal AmeControllerSystem AmeControllerSystem = default!;
    [Dependency] internal ChemMasterSystem ChemMasterSystem = default!;
    [Dependency] internal RadioSystem RadioSystem = default!;
    [Dependency] internal UserInterfaceSystem UserInterfaceSystem = default!;

    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _gameTiming = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private DeviceLinkSystem _deviceLinkSystem = default!;
    [Dependency] private SharedAudioSystem _audioSystem = default!;

    private int _mainThreadId;

    public override void Initialize()
    {
        _mainThreadId = Environment.CurrentManagedThreadId;
        PreInitJint();

        SubscribeLocalEvent<PacketExecutorComponent, BoundUIOpenedEvent>(SubscribeUpdateUiState);
        SubscribeLocalEvent<PacketExecutorComponent, SaveExecutorCommandMessage>(OnSave);
        SubscribeLocalEvent<PacketExecutorComponent, StartExecutionMessage>(OnExecute);
        SubscribeLocalEvent<PacketExecutorComponent, ReloadModulesMessage>(OnModuleReload);
        SubscribeLocalEvent<PacketExecutorComponent, TerminateExecutorMessage>(OnTerminate);
        SubscribeLocalEvent<PacketExecutorComponent, WidgetButtonClickedMessage>(OnWidgetButton);
        SubscribeLocalEvent<PacketExecutorComponent, ExecutorRequestCallsInfoMessage>(OnDeviceCallsRequest);

        SubscribeLocalEvent<PacketExecutorComponent, SignalReceivedEvent>(OnExecutorSignal);

        SubscribeLocalEvent<PacketNetworkComponent, MapInitEvent>(OnPacketInit);

        SubscribeLocalEvent<FrequenciesPaperComponent, MapInitEvent>(OnFrequencyPaperInit);

        SubscribeLocalEvent<RoundStartingEvent>(OnRoundStart);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnCleanup);
    }

    /// <summary>
    /// Processes system call queue, execution cooldowns and logging.
    /// </summary>
    /// <param name="frameTime"></param>
    public override void Update(float frameTime)
    {
        UpdateSystemCalls();
        foreach (var (uid, _) in _executorEntities)
        {
            if (uid.Comp.CurrentCooldown > TimeSpan.Zero)
                uid.Comp.CurrentCooldown -= TimeSpan.FromSeconds(frameTime);

            if (uid.Comp.Log == string.Empty)
                continue;

            UserInterfaceSystem.ServerSendUiMessage(uid.Owner, ExecutorUiKey.Key, new LogExecutorMessage(uid.Comp.Log));
            Logger.Info(uid.Comp.Log);
            uid.Comp.Log = string.Empty;
        }
    }

    private void SubscribeUpdateUiState<T>(Entity<PacketExecutorComponent> ent, ref T ev)
    {
        UpdateUiState(ent);
    }

    /// <summary>
    /// Receives code from client and loads it into executor's component.
    /// </summary>
    /// <param name="ent"></param>
    /// <param name="ev"></param>
    private void OnSave(Entity<PacketExecutorComponent> ent, ref SaveExecutorCommandMessage ev)
    {
        ent.Comp.Command = ev.Command;
    }

    /// <summary>
    /// Tries to execute code when "EXEC" message is sent.
    /// </summary>
    /// <param name="ent"></param>
    /// <param name="ev"></param>
    private void OnExecute(Entity<PacketExecutorComponent> ent, ref StartExecutionMessage ev)
    {
        ent.Comp.Command = ev.Command;
        TryExecute(ent.Comp.Command, ent, ev.Actor);
    }

    /// <summary>
    /// on "LOAD" message - Re-creates engine instance with new module set.
    /// </summary>
    /// <param name="ent"></param>
    /// <param name="ev"></param>
    private void OnModuleReload(Entity<PacketExecutorComponent> ent, ref ReloadModulesMessage ev)
    {
        if (!TryComp<ItemSlotsComponent>(ent, out var slotComponent))
            return;

        ReloadEngine(ent, slotComponent);
    }

    /// <summary>
    /// On "KILL" message - Kills all scripts that are currently running.
    /// </summary>
    /// <param name="ent"></param>
    /// <param name="ev"></param>
    private void OnTerminate(Entity<PacketExecutorComponent> ent, ref TerminateExecutorMessage ev)
    {
        Cancel(ent);
    }

    private void OnWidgetButton(Entity<PacketExecutorComponent> ent, ref WidgetButtonClickedMessage ev)
    {
        if (!TryFindWidgetElement<WidgetButton>(ent, ev.Button,  out var widget))
            return;

        LoadVariables(ent, ev.Arguments);
        InvokeAction(widget.MethodName, ent);
    }

    private void OnDeviceCallsRequest(Entity<PacketExecutorComponent> ent, ref ExecutorRequestCallsInfoMessage ev)
    {
        var message = new ExecutorRequestCallsInfoMessage
        {
            Calls = _classEntries,
        };

        UserInterfaceSystem.ServerSendUiMessage(ent.Owner, ExecutorUiKey.Key, message);
    }

    /// <summary>
    /// Processes signals that are registered through SinkListenMethod.
    /// </summary>
    /// <param name="ent"></param>
    /// <param name="ev"></param>
    private void OnExecutorSignal(Entity<PacketExecutorComponent> ent, ref SignalReceivedEvent ev)
    {
        if (!ent.Comp.ListeningPorts.ContainsKey(ev.Port))
            return;

        OnSignal(ev.Port, ent);
    }

    private void UpdateUiState(Entity<PacketExecutorComponent> ent)
    {
        var maxStatements = ent.Comp.MaximumExecutionStatements;
        var maxMemory = ent.Comp.MemoryAllocation;
        var command = ent.Comp.Command;

        UserInterfaceSystem.SetUiState(ent.Owner,
            ExecutorUiKey.Key,
            new ExecutorBoundUserInterfaceState(
                maxStatements,
                maxMemory,
                command,
                [.. ent.Comp.Widgets.Values]));
    }

    private void OnRoundStart(RoundStartingEvent ev)
    {
        RandomizeFrequencies();
    }

    private void OnPacketInit(Entity<PacketNetworkComponent> ent, ref MapInitEvent ev)
    {
        SetupAddress(ent);
        ReloadFrequencies(ent);
    }

    private void OnFrequencyPaperInit(Entity<FrequenciesPaperComponent> ent, ref MapInitEvent ev)
    {
        GenerateFrequenciesPaper(ent);
    }

    private void OnCleanup(RoundRestartCleanupEvent ev)
    {
        Dispose();
    }

    private void Dispose()
    {
        _packetEntities.Clear();
        _executorEntities.Clear();
        _methods.Clear();
        _modules.Clear();
        _engineCts.Clear();
        _frequencies.Clear();
        _networks.Clear();
    }
}
