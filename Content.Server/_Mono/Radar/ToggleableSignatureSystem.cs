using Content.Server.Popups;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Verbs;

namespace Content.Server._Mono.Radar;

public sealed class ToggleableSignatureSystem : EntitySystem
{
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ToggleableSignatureComponent, GetVerbsEvent<Verb>>(OnVerb);
        SubscribeLocalEvent<ToggleableSignatureComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<ToggleableSignatureComponent, MapInitEvent>(OnInit);
    }

    // if this is made an alternative verb it conflicts with (un)locking on borgs
    private void OnVerb(Entity<ToggleableSignatureComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var verb = new Verb
        {
            Text = Loc.GetString("suit-sensor-signature-toggle", ("status", GetStatusSignatureName(ent.Comp))),
            Act = () => TryToggleSignature(ent)
        };
        args.Verbs.Add(verb);
    }

    private void OnExamine(Entity<ToggleableSignatureComponent> ent, ref ExaminedEvent args)
    {
        if (!ent.Comp.Examinable || !args.IsInDetailsRange)
            return;

        string radarMsg;
        switch (ent.Comp.Enabled)
        {
            case true:
                radarMsg = "suit-sensor-signature-examine-on";
                break;
            case false:
                radarMsg = "suit-sensor-signature-examine-off";
                break;
        }

        args.PushMarkup(Loc.GetString(radarMsg));
    }

    private void OnInit(Entity<ToggleableSignatureComponent> ent, ref MapInitEvent args)
    {
        // so that it works if we specify enabled: true in yml
        if (ent.Comp.Enabled)
            TryEnableSignature(ent);
        else
            TryDisableSignature(ent);
    }

    private string GetStatusSignatureName(ToggleableSignatureComponent component)
    {
        string signatureName;
        switch (component.Enabled)
        {
            case true:
                signatureName = "suit-sensor-signature-verb-disable";
                break;
            case false:
                signatureName = "suit-sensor-signature-verb-enable";
                break;
        }

        return Loc.GetString(signatureName);
    }

    public void TryToggleSignature(Entity<ToggleableSignatureComponent> ent)
    {
        if (ent.Comp.Enabled || HasComp<RadarBlipComponent>(ent))
            TryDisableSignature(ent);
        else
            TryEnableSignature(ent);
    }

    public bool TryDisableSignature(Entity<ToggleableSignatureComponent> ent)
    {
        ent.Comp.Enabled = false;
        if (!HasComp<RadarBlipComponent>(ent))
            return false;

        RemComp<RadarBlipComponent>(ent);
        _popup.PopupEntity(Loc.GetString("suit-sensor-signature-toggled-off"), ent);
        return true;
    }

    public bool TryEnableSignature(Entity<ToggleableSignatureComponent> ent)
    {
        ent.Comp.Enabled = true;
        // if we already have a blip but it's not our blip, someone did something wrong and we should override it
        if (TryComp<RadarBlipComponent>(ent, out var blip) && blip == ent.Comp.Blip)
            return false;

        AddComp(ent, ent.Comp.Blip, true);
        _popup.PopupEntity(Loc.GetString("suit-sensor-signature-toggled-on"), ent);
        return true;
    }
}
