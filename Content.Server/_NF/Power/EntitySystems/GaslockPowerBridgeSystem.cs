using Content.Server._NF.Atmos.Components;
using Content.Shared._NF.Power.Components;
using Content.Server.NodeContainer;
using Content.Server.NodeContainer.EntitySystems;
using Content.Server.NodeContainer.Nodes;
using Content.Server.Power.Nodes;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Events;
using Content.Shared.Power;
using System.Diagnostics.CodeAnalysis;

namespace Content.Server._NF.Power.EntitySystems;

public sealed class GaslockPowerBridgeSystem : EntitySystem
{
    [Dependency] private readonly NodeContainerSystem _nodeContainer = default!;
    [Dependency] private readonly NodeGroupSystem _nodeGroup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GaslockPowerBridgeComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<GaslockPowerBridgeComponent, AnchorStateChangedEvent>(OnAnchorChanged);
        SubscribeLocalEvent<GaslockPowerBridgeComponent, ReAnchorEvent>(OnReAnchor);
        SubscribeLocalEvent<GaslockPowerBridgeComponent, DockEvent>(OnDock);
        SubscribeLocalEvent<GaslockPowerBridgeComponent, UndockEvent>(OnUndock);
    }

    private void OnInit(Entity<GaslockPowerBridgeComponent> ent, ref ComponentInit args)
    {
        RefreshBridgeState(ent);
    }

    private void OnDock(Entity<GaslockPowerBridgeComponent> ent, ref DockEvent args)
    {
        RefreshBridgePair(args.DockA.Owner, args.DockB.Owner);
    }

    private void OnUndock(Entity<GaslockPowerBridgeComponent> ent, ref UndockEvent args)
    {
        RefreshBridgePair(args.DockA.Owner, args.DockB.Owner);
    }

    private void OnAnchorChanged(Entity<GaslockPowerBridgeComponent> ent, ref AnchorStateChangedEvent args)
    {
        RefreshBridgePair(ent.Owner);
    }

    private void OnReAnchor(Entity<GaslockPowerBridgeComponent> ent, ref ReAnchorEvent args)
    {
        RefreshBridgePair(ent.Owner);
    }

    public bool TryGetFocusData(EntityUid uid, [NotNullWhen(true)] out PowerMonitoringFocusGaslockData? data)
    {
        data = null;

        if (!TryComp<GaslockPowerBridgeComponent>(uid, out var bridge))
            return false;

        var pressure = 0f;
        if (TryComp<DockablePipeComponent>(uid, out var dockablePipe) &&
            _nodeContainer.TryGetNode<PipeNode>(uid, dockablePipe.InternalNodeName, out var internalPipe))
        {
            pressure = internalPipe.Air.Pressure;
        }

        NetEntity? dockedNet = null;
        if (TryComp<DockingComponent>(uid, out var docking) && docking.DockedWith != null)
            dockedNet = GetNetEntity(docking.DockedWith.Value);

        data = new PowerMonitoringFocusGaslockData(
            GetNetEntity(uid),
            dockedNet,
            pressure,
            GetChannelState((uid, bridge), GaslockPowerChannel.HV),
            GetChannelState((uid, bridge), GaslockPowerChannel.MV),
            GetChannelState((uid, bridge), GaslockPowerChannel.LV));

        return true;
    }

    public PowerMonitoringGaslockChannelState GetChannelState(Entity<GaslockPowerBridgeComponent> ent, GaslockPowerChannel channel)
    {
        var (_, dockNodeName) = GetChannelData(ent.Comp, channel);
        var active = false;

        if (TryComp<NodeContainerComponent>(ent, out var nodeContainer) &&
            nodeContainer.Nodes.TryGetValue(dockNodeName, out var node) &&
            node is CableDeviceNode cableNode)
        {
            active = cableNode.ReachableNodes.Count > 0;
        }

        return new PowerMonitoringGaslockChannelState(active);
    }

    private void RefreshBridgePair(EntityUid uid)
    {
        RefreshBridgeState(uid);

        if (TryComp<DockingComponent>(uid, out var docking) &&
            docking.DockedWith != null &&
            HasComp<GaslockPowerBridgeComponent>(docking.DockedWith.Value))
        {
            RefreshBridgeState(docking.DockedWith.Value);
        }
    }

    private void RefreshBridgePair(EntityUid first, EntityUid second)
    {
        RefreshBridgeState(first);
        if (second != first)
            RefreshBridgeState(second);
    }

    private void RefreshBridgeState(EntityUid uid)
    {
        if (!TryComp<GaslockPowerBridgeComponent>(uid, out var bridge) ||
            !TryComp<NodeContainerComponent>(uid, out var nodeContainer))
        {
            return;
        }

        RefreshChannel(uid, nodeContainer, bridge, GaslockPowerChannel.HV);
        RefreshChannel(uid, nodeContainer, bridge, GaslockPowerChannel.MV);
        RefreshChannel(uid, nodeContainer, bridge, GaslockPowerChannel.LV);
    }

    private void RefreshChannel(
        EntityUid uid,
        NodeContainerComponent nodeContainer,
        GaslockPowerBridgeComponent bridge,
        GaslockPowerChannel channel)
    {
        var (_, dockNodeName) = GetChannelData(bridge, channel);

        if (!nodeContainer.Nodes.TryGetValue(dockNodeName, out var node) ||
            node is not CableDeviceNode cableNode)
        {
            return;
        }

        // Keep dock nodes connectable at all times; docking state controls whether
        // they discover a partner, mirroring dockable pipe behavior.
        if (!cableNode.Enabled)
            cableNode.Enabled = true;

        if (ShouldRefloodDockBridge(uid))
            _nodeGroup.QueueReflood(cableNode);
        else
            _nodeGroup.QueueNodeRemove(cableNode);
    }

    private bool ShouldRefloodDockBridge(EntityUid uid)
    {
        if (!Transform(uid).Anchored)
            return false;

        if (!TryComp<DockingComponent>(uid, out var docking) || docking.DockedWith == null)
            return false;

        return HasComp<GaslockPowerBridgeComponent>(docking.DockedWith.Value);
    }

    private static (string internalNode, string dockNode) GetChannelData(
        GaslockPowerBridgeComponent comp,
        GaslockPowerChannel channel)
    {
        return channel switch
        {
            GaslockPowerChannel.HV => (comp.HvInternalNode, comp.HvDockNode),
            GaslockPowerChannel.MV => (comp.MvInternalNode, comp.MvDockNode),
            GaslockPowerChannel.LV => (comp.LvInternalNode, comp.LvDockNode),
            _ => (comp.HvInternalNode, comp.HvDockNode),
        };
    }
}
