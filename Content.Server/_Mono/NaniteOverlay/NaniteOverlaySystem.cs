using Content.Server.Destructible;
using Content.Server.Destructible.Thresholds.Triggers;
using Content.Shared._Mono.NaniteOverlay;
using Content.Shared._Mono.ShipRepair.Components;
using Content.Shared.Eye;
using Content.Shared.FixedPoint;
using Content.Shared.Hands;
using Content.Shared.Inventory.Events;
using Content.Shared.Tools.Components;
using Content.Shared.Tools.Systems;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Content.Server._Mono.NaniteOverlay;

public sealed partial class NaniteOverlaySystem : EntitySystem
{
    [Dependency] private SharedEyeSystem _eye = default!;
    [Dependency] private SharedToolSystem _tool = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ToolComponent, GotEquippedHandEvent>(OnToolHandEquipped);
        SubscribeLocalEvent<ToolComponent, GotEquippedEvent>(OnToolEquipped);
        SubscribeLocalEvent<ToolComponent, GotUnequippedHandEvent>(OnToolHandUnequipped);
        SubscribeLocalEvent<ToolComponent, GotUnequippedEvent>(OnToolUnequipped);
        SubscribeLocalEvent<NaniteOverlayEyeComponent, GetVisMaskEvent>(OnGetVis);

        SubscribeNetworkEvent<NaniteOverlayMessage>(OnNaniteOverlayMessage);
    }

    private void OnNaniteOverlayMessage(NaniteOverlayMessage message, EntitySessionEventArgs eventArgs)
    {
        //TODO: security?

        var ents = GetEntityArray(message.Targets);
        var response = new FixedPoint2[ents.Length];

        int i = -1;
        foreach (var ent in ents)
        {
            i++;
            if (!ent.Valid || !TryComp<DestructibleComponent>(ent, out var destructible))
            {
                response[i] = 0;
                continue;
            }

            var trigger = (DamageTrigger?)destructible.Thresholds.LastOrDefault(threshold => threshold.Trigger is DamageTrigger)?.Trigger;
            if (trigger == null)
            {
                response[i] = 0;
                continue;
            }

            response[i] = trigger.Damage;
        }

        RaiseNetworkEvent(new NaniteOverlayMessage(message.Targets, response), eventArgs.SenderSession);
    }

    private void OnEquip(EntityUid user)
    {
        var comp = EnsureComp<NaniteOverlayEyeComponent>(user);
        comp.Count++;

        if (comp.Count > 1)
            return;

        _eye.RefreshVisibilityMask(user);
    }

    private void OnUnequip(EntityUid user)
    {
        if (!TryComp(user, out NaniteOverlayEyeComponent? comp))
            return;

        comp.Count--;

        if (comp.Count > 0)
            return;

        RemComp<NaniteOverlayEyeComponent>(user);
        _eye.RefreshVisibilityMask(user);
    }

    private void OnToolHandEquipped(Entity<ToolComponent> ent, ref GotEquippedHandEvent args)
    {
        if(_tool.HasQuality(ent, "Applicating"))
            OnEquip(args.User);
    }

    private void OnToolEquipped(Entity<ToolComponent> ent, ref GotEquippedEvent args)
    {
        if (_tool.HasQuality(ent, "Applicating"))
            OnEquip(args.Equipee);
    }

    private void OnToolHandUnequipped(Entity<ToolComponent> ent, ref GotUnequippedHandEvent args)
    {
        if (_tool.HasQuality(ent, "Applicating"))
            OnUnequip(args.User);
    }

    private void OnToolUnequipped(Entity<ToolComponent> ent, ref GotUnequippedEvent args)
    {
        if (_tool.HasQuality(ent, "Applicating"))
            OnUnequip(args.Equipee);
    }

    private void OnGetVis(Entity<NaniteOverlayEyeComponent> ent, ref GetVisMaskEvent args)
    {
        args.VisibilityMask |= (int)VisibilityFlags.Subfloor;
    }
}
