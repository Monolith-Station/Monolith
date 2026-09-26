using Content.Server._Mono.PDV.Components;
using Content.Server.Radio.EntitySystems;
using Content.Shared._Mono.PDV.Components;
using Content.Shared.Contraband;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server._Mono.PDV.EntitySystems;

public sealed partial class HereticSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private RadioSystem _radio = default!;
    [Dependency] private SharedUserInterfaceSystem _uiSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HereticComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<HereticConsoleComponent, BoundUIOpenedEvent>(OnHereticConsoleOpened);
    }

    private void OnMapInit(Entity<HereticComponent> ent, ref MapInitEvent args)
    {
        var reward = 0;
        if (TryComp<ContrabandComponent>(ent, out var contraband) && contraband.TurnInValues.Keys.Contains("Doubloon"))
            reward = contraband.TurnInValues["Doubloon"];
        var query = EntityQueryEnumerator<HereticDetectorComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            _proto.TryIndex(comp.Language, out var languageProto);
            _radio.SendRadioMessage(uid, Loc.GetString(comp.Message, ("heretic", Name(ent)), ("reward", reward)), comp.RadioChannel, uid, language: languageProto);
            UpdateHereticUi(ent);
        }
    }

    private void OnHereticConsoleOpened(Entity<HereticConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateHereticUi(ent);
    }

    private void UpdateHereticUi(EntityUid console)
    {
        var query = EntityQueryEnumerator<HereticComponent>();
        var heretics = new List<Heretic>();
        while (query.MoveNext(out var uid, out _))
        {
            var reward = 0;
            if (TryComp<ContrabandComponent>(uid, out var contra) && contra.TurnInValues.Keys.Contains("Doubloon"))
                reward = contra.TurnInValues["Doubloon"];

            var name = MetaData(uid).EntityName;
            heretics.Add(new Heretic(reward, name));
        }

        _uiSystem.SetUiState(console, HereticConsoleUiKey.Heretics, new HereticConsoleState(heretics));
    }
}