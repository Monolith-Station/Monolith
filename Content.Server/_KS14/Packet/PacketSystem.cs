using Content.Server._KS14.Packet.Components;
using Content.Server._KS14.Packet.Modules.Base;
using Content.Server.DeviceLinking.Systems;
using Content.Server.GameTicking.Events;
using Content.Shared._KS14.Packets.BUI;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.GameTicking;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._KS14.Packet;

/// <summary>
/// Handles events and network.
/// </summary>
public sealed partial class PacketSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private UserInterfaceSystem _userInterfaceSystem = default!;
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
        SubscribeLocalEvent<PacketExecutorComponent, InputExecutorMessage>(OnInput);

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

            _userInterfaceSystem.ServerSendUiMessage(uid.Owner, ExecutorUiKey.Key, new LogExecutorMessage(uid.Comp.Log));
            uid.Comp.Log = string.Empty;
        }
    }

    private void SubscribeUpdateUiState<T>(Entity<PacketExecutorComponent> ent, ref T ev)
    {
        UpdateUiState(ent);
    }

    /// <summary>
    /// On "SAVE" message - receives code from client and loads it into executor's component.
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

    /// <summary>
    /// On "SEND" message - Send data from client in input method channel.
    /// </summary>
    /// <param name="ent"></param>
    /// <param name="ev"></param>
    private void OnInput(Entity<PacketExecutorComponent> ent, ref InputExecutorMessage ev)
    {
        SendData(ev.Input, ent, typeof(InputMethod), "BasePacketModule", ent.Comp);
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

        _userInterfaceSystem.SetUiState(ent.Owner, ExecutorUiKey.Key, new ExecutorBoundUserInterfaceState(maxStatements, maxMemory, command));
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
