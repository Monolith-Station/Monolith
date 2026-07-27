using Content.Shared.Clothing.Components;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Components;
using Content.Shared.Popups;
using Content.Shared.Tools.Components;
using Content.Shared.Tools.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Network;

namespace Content.Shared._Forge.Clothing;

public abstract partial class SharedModsuitGauntletToolsSystem : EntitySystem
{
    /// <summary>
    /// Allows removing gauntlet tools from hands during internal store operations.
    /// </summary>
    private readonly HashSet<EntityUid> _allowedHandRemovals = new();

    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedToolSystem _toolSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ModsuitGauntletToolComponent, ContainerGettingRemovedAttemptEvent>(OnToolRemoveFromHandAttempt);
        SubscribeLocalEvent<ModsuitGauntletToolComponent, GotUnequippedHandEvent>(OnToolUnequippedHand);
    }

    protected void ToggleToolBySlot(
        Entity<ModsuitGauntletToolsComponent> gauntlets,
        EntityUid wearer,
        ModsuitGauntletToolSlot slot)
    {
        if (!IsSlotEnabled(gauntlets.Comp, slot))
            return;

        switch (slot)
        {
            case ModsuitGauntletToolSlot.Urk:
                ToggleTool(gauntlets, wearer, gauntlets.Comp.UrkEntity, ref gauntlets.Comp.UrkInHand);
                break;
            case ModsuitGauntletToolSlot.Omnitool:
                ToggleTool(gauntlets, wearer, gauntlets.Comp.OmnitoolEntity, ref gauntlets.Comp.OmnitoolInHand);
                break;
            case ModsuitGauntletToolSlot.Welder:
                ToggleTool(gauntlets, wearer, gauntlets.Comp.WelderEntity, ref gauntlets.Comp.WelderInHand);
                break;
            case ModsuitGauntletToolSlot.NaniteApplicator:
                ToggleTool(gauntlets, wearer, gauntlets.Comp.NaniteApplicatorEntity, ref gauntlets.Comp.NaniteApplicatorInHand);
                break;
            case ModsuitGauntletToolSlot.Auxiliary:
                ToggleTool(gauntlets, wearer, gauntlets.Comp.AuxiliaryEntity, ref gauntlets.Comp.AuxiliaryInHand);
                break;
            case ModsuitGauntletToolSlot.Piping:
                ToggleTool(gauntlets, wearer, gauntlets.Comp.PipingEntity, ref gauntlets.Comp.PipingInHand);
                break;
        }
    }

    public static bool IsSlotEnabled(ModsuitGauntletToolsComponent comp, ModsuitGauntletToolSlot slot)
    {
        return slot switch
        {
            ModsuitGauntletToolSlot.Urk => comp.EnabledSlots.HasFlag(ModsuitGauntletEnabledSlots.Urk),
            ModsuitGauntletToolSlot.Omnitool => comp.EnabledSlots.HasFlag(ModsuitGauntletEnabledSlots.Omnitool),
            ModsuitGauntletToolSlot.Welder => comp.EnabledSlots.HasFlag(ModsuitGauntletEnabledSlots.Welder),
            ModsuitGauntletToolSlot.NaniteApplicator => comp.EnabledSlots.HasFlag(ModsuitGauntletEnabledSlots.NaniteApplicator),
            ModsuitGauntletToolSlot.Auxiliary => comp.EnabledSlots.HasFlag(ModsuitGauntletEnabledSlots.Auxiliary),
            ModsuitGauntletToolSlot.Piping => comp.EnabledSlots.HasFlag(ModsuitGauntletEnabledSlots.Piping),
            _ => false,
        };
    }

    protected bool TryGetActiveToolSlot(ModsuitGauntletToolsComponent comp, out ModsuitGauntletToolSlot slot)
    {
        if (comp.UrkInHand && IsSlotEnabled(comp, ModsuitGauntletToolSlot.Urk))
        {
            slot = ModsuitGauntletToolSlot.Urk;
            return true;
        }

        if (comp.OmnitoolInHand && IsSlotEnabled(comp, ModsuitGauntletToolSlot.Omnitool))
        {
            slot = ModsuitGauntletToolSlot.Omnitool;
            return true;
        }

        if (comp.WelderInHand && IsSlotEnabled(comp, ModsuitGauntletToolSlot.Welder))
        {
            slot = ModsuitGauntletToolSlot.Welder;
            return true;
        }

        if (comp.NaniteApplicatorInHand && IsSlotEnabled(comp, ModsuitGauntletToolSlot.NaniteApplicator))
        {
            slot = ModsuitGauntletToolSlot.NaniteApplicator;
            return true;
        }

        if (comp.AuxiliaryInHand && IsSlotEnabled(comp, ModsuitGauntletToolSlot.Auxiliary))
        {
            slot = ModsuitGauntletToolSlot.Auxiliary;
            return true;
        }

        if (comp.PipingInHand && IsSlotEnabled(comp, ModsuitGauntletToolSlot.Piping))
        {
            slot = ModsuitGauntletToolSlot.Piping;
            return true;
        }

        slot = default;
        return false;
    }

    protected bool IsToolDeployed(ModsuitGauntletToolsComponent comp, ModsuitGauntletToolSlot slot)
    {
        return slot switch
        {
            ModsuitGauntletToolSlot.Urk => comp.UrkInHand,
            ModsuitGauntletToolSlot.Omnitool => comp.OmnitoolInHand,
            ModsuitGauntletToolSlot.Welder => comp.WelderInHand,
            ModsuitGauntletToolSlot.NaniteApplicator => comp.NaniteApplicatorInHand,
            ModsuitGauntletToolSlot.Auxiliary => comp.AuxiliaryInHand,
            ModsuitGauntletToolSlot.Piping => comp.PipingInHand,
            _ => false,
        };
    }

    protected void ToggleTool(
        Entity<ModsuitGauntletToolsComponent> gauntlets,
        EntityUid wearer,
        EntityUid? tool,
        ref bool inHand)
    {
        if (tool == null || !Exists(tool))
            return;

        if (!TryComp<TransformComponent>(tool.Value, out _))
            return;

        if (inHand || _hands.IsHolding(wearer, tool.Value))
        {
            if (TryStoreTool(wearer, tool.Value))
            {
                inHand = false;
                UpdateInHandFlags(gauntlets, gauntlets.Comp);
                Dirty(gauntlets);
            }

            return;
        }

        StowOtherHeldTools(gauntlets, wearer, tool.Value);

        if (!_hands.TryGetEmptyHand(wearer, out _))
        {
            _popup.PopupEntity(Loc.GetString("modsuit-gauntlet-no-free-hand"), wearer, wearer);
            return;
        }

        if (TryDeployTool(wearer, tool.Value))
        {
            inHand = true;
            UpdateInHandFlags(gauntlets, gauntlets.Comp);
            Dirty(gauntlets);
        }
    }

    private void StowOtherHeldTools(Entity<ModsuitGauntletToolsComponent> gauntlets, EntityUid wearer, EntityUid activeTool)
    {
        var comp = gauntlets.Comp;
        StowIfHeld(gauntlets, wearer, activeTool, comp.UrkEntity, ref comp.UrkInHand);
        StowIfHeld(gauntlets, wearer, activeTool, comp.OmnitoolEntity, ref comp.OmnitoolInHand);
        StowIfHeld(gauntlets, wearer, activeTool, comp.WelderEntity, ref comp.WelderInHand);
        StowIfHeld(gauntlets, wearer, activeTool, comp.NaniteApplicatorEntity, ref comp.NaniteApplicatorInHand);
        StowIfHeld(gauntlets, wearer, activeTool, comp.AuxiliaryEntity, ref comp.AuxiliaryInHand);
        StowIfHeld(gauntlets, wearer, activeTool, comp.PipingEntity, ref comp.PipingInHand);
    }

    private void StowIfHeld(
        Entity<ModsuitGauntletToolsComponent> gauntlets,
        EntityUid wearer,
        EntityUid activeTool,
        EntityUid? tool,
        ref bool inHand)
    {
        if (tool == null || tool.Value == activeTool)
            return;

        if (!Exists(tool) || !TryComp<TransformComponent>(tool.Value, out _))
        {
            inHand = false;
            return;
        }

        if (!inHand && !_hands.IsHolding(wearer, tool.Value))
            return;

        if (TryStoreTool(wearer, tool.Value))
            inHand = false;
    }

    private void OnToolRemoveFromHandAttempt(
        Entity<ModsuitGauntletToolComponent> ent,
        ref ContainerGettingRemovedAttemptEvent args)
    {
        if (_allowedHandRemovals.Contains(ent.Owner))
            return;

        if (!HasComp<HandsComponent>(args.Container.Owner))
            return;

        args.Cancel();

        if (!_net.IsServer)
            return;

        if (!TryComp(ent.Comp.Gauntlets, out ModsuitGauntletToolsComponent? gauntlets))
            return;

        if (!TryRemoveFromHandForStore(args.Container.Owner, ent.Owner))
            return;

        StoreToolHidden(ent.Owner);
        UpdateInHandFlags(ent.Comp.Gauntlets, gauntlets);
        Dirty(ent.Comp.Gauntlets, gauntlets);
        _popup.PopupEntity(Loc.GetString("modsuit-gauntlet-cannot-drop"), args.Container.Owner, args.Container.Owner);
    }

    private void OnToolUnequippedHand(Entity<ModsuitGauntletToolComponent> ent, ref GotUnequippedHandEvent args)
    {
        if (_allowedHandRemovals.Contains(ent.Owner))
            return;

        if (!_net.IsServer)
            return;

        if (!TryComp(ent.Comp.Gauntlets, out ModsuitGauntletToolsComponent? gauntlets))
            return;

        if (!Exists(ent.Owner) || TerminatingOrDeleted(ent.Owner))
            return;

        if (IsToolStored(ent.Owner))
            return;

        StoreToolHidden(ent.Owner);
        UpdateInHandFlags(ent.Comp.Gauntlets, gauntlets);
        Dirty(ent.Comp.Gauntlets, gauntlets);
    }

    protected void EnsureToolLink(EntityUid tool, EntityUid gauntlets)
    {
        var link = EnsureComp<ModsuitGauntletToolComponent>(tool);
        link.Gauntlets = gauntlets;
    }

    protected void EnsureToolStored(EntityUid tool)
    {
        if (!Exists(tool) || TerminatingOrDeleted(tool))
            return;

        var unremoveable = EnsureComp<UnremoveableComponent>(tool);
        unremoveable.DeleteOnDrop = false;
    }

    protected void StoreToolHidden(EntityUid tool)
    {
        if (!Exists(tool) || TerminatingOrDeleted(tool))
            return;

        if (!TryComp<TransformComponent>(tool, out var xform))
            return;

        if (HasComp<UnremoveableComponent>(tool))
            RemComp<UnremoveableComponent>(tool);

        if (xform.ParentUid.IsValid())
            _transform.DetachEntity(tool, xform);

        SetToolStoredHidden(tool, true);
        EnsureToolStored(tool);
    }

    protected bool IsToolStored(EntityUid tool)
    {
        if (!Exists(tool) || !TryComp<TransformComponent>(tool, out var xform))
            return true;

        return !xform.ParentUid.IsValid();
    }

    protected void UpdateInHandFlags(EntityUid gauntlets, ModsuitGauntletToolsComponent comp)
    {
        var wearer = GetWearer(gauntlets);
        if (wearer == null)
        {
            comp.UrkInHand = false;
            comp.OmnitoolInHand = false;
            comp.WelderInHand = false;
            comp.NaniteApplicatorInHand = false;
            comp.AuxiliaryInHand = false;
            comp.PipingInHand = false;
            return;
        }

        comp.UrkInHand = IsHeld(wearer.Value, comp.UrkEntity);
        comp.OmnitoolInHand = IsHeld(wearer.Value, comp.OmnitoolEntity);
        comp.WelderInHand = IsHeld(wearer.Value, comp.WelderEntity);
        comp.NaniteApplicatorInHand = IsHeld(wearer.Value, comp.NaniteApplicatorEntity);
        comp.AuxiliaryInHand = IsHeld(wearer.Value, comp.AuxiliaryEntity);
        comp.PipingInHand = IsHeld(wearer.Value, comp.PipingEntity);
    }

    private bool IsHeld(EntityUid wearer, EntityUid? tool)
    {
        return tool != null && Exists(tool) && _hands.IsHolding(wearer, tool);
    }

    protected EntityUid? GetWearer(EntityUid gauntlets)
    {
        if (!TryComp<ClothingComponent>(gauntlets, out var clothing) || clothing.InSlot == null)
            return null;

        return Transform(gauntlets).ParentUid;
    }

    private bool TryRemoveFromHandForStore(EntityUid wearer, EntityUid tool)
    {
        if (!_hands.IsHolding(wearer, tool, out var hand))
            return true;

        if (HasComp<UnremoveableComponent>(tool))
            RemComp<UnremoveableComponent>(tool);

        _allowedHandRemovals.Add(tool);
        try
        {
            _hands.DoDrop(wearer, hand, doDropInteraction: false);
        }
        finally
        {
            _allowedHandRemovals.Remove(tool);
        }

        return !_hands.IsHolding(wearer, tool);
    }

    protected bool TryStoreTool(EntityUid wearer, EntityUid tool)
    {
        if (!Exists(tool) || TerminatingOrDeleted(tool))
            return false;

        if (!TryRemoveFromHandForStore(wearer, tool))
            return false;

        StoreToolHidden(tool);
        return true;
    }

    protected bool TryDeployTool(EntityUid wearer, EntityUid tool)
    {
        if (!Exists(tool) || TerminatingOrDeleted(tool))
            return false;

        if (IsToolStored(tool))
            _transform.SetCoordinates(tool, Transform(wearer).Coordinates);

        if (HasComp<UnremoveableComponent>(tool))
            RemComp<UnremoveableComponent>(tool);

        if (!_hands.TryPickupAnyHand(wearer, tool, checkActionBlocker: false))
        {
            StoreToolHidden(tool);
            return false;
        }

        SetToolStoredHidden(tool, false);

        if (TryComp<MultipleToolComponent>(tool, out var multiple))
            _toolSystem.SetMultipleTool(tool, multiple);

        return true;
    }

    /// <summary>
    /// Stows every deployed integrated tool on the wearer before crafting or similar inventory scans.
    /// </summary>
    public void StowAllDeployedTools(EntityUid wearer)
    {
        var query = EntityQueryEnumerator<ModsuitGauntletToolsComponent>();
        while (query.MoveNext(out var gauntletsUid, out var comp))
        {
            if (GetWearer(gauntletsUid) != wearer)
                continue;

            StowDeployedTool(wearer, (gauntletsUid, comp), comp.UrkEntity, ref comp.UrkInHand);
            StowDeployedTool(wearer, (gauntletsUid, comp), comp.OmnitoolEntity, ref comp.OmnitoolInHand);
            StowDeployedTool(wearer, (gauntletsUid, comp), comp.WelderEntity, ref comp.WelderInHand);
            StowDeployedTool(wearer, (gauntletsUid, comp), comp.NaniteApplicatorEntity, ref comp.NaniteApplicatorInHand);
            StowDeployedTool(wearer, (gauntletsUid, comp), comp.AuxiliaryEntity, ref comp.AuxiliaryInHand);
            StowDeployedTool(wearer, (gauntletsUid, comp), comp.PipingEntity, ref comp.PipingInHand);
            Dirty(gauntletsUid, comp);
        }
    }

    private void StowDeployedTool(
        EntityUid wearer,
        Entity<ModsuitGauntletToolsComponent> gauntlets,
        EntityUid? tool,
        ref bool inHand)
    {
        if (tool == null || !inHand && !_hands.IsHolding(wearer, tool.Value))
            return;

        if (TryStoreTool(wearer, tool.Value))
            inHand = false;
    }

    private void SetToolStoredHidden(EntityUid tool, bool hidden)
    {
        var link = EnsureComp<ModsuitGauntletToolComponent>(tool);
        if (link.StoredHidden == hidden)
            return;

        link.StoredHidden = hidden;
        Dirty(tool, link);
    }
}
