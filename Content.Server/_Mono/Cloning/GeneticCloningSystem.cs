using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Content.Server._NF.Traits.Assorted;
using Content.Server.Chat.Systems;
using Content.Server.Cloning;
using Content.Server.Cloning.Components;
using Content.Server.EUI;
using Content.Server.Humanoid;
using Content.Server.Jobs;
using Content.Server.Materials;
using Content.Shared._EinsteinEngines.Silicon.Components;
using Content.Shared._NF.Bank.Components;
using Content.Shared._NF.Cloning;
using Content.Shared.CCVar;
using Content.Shared.Chat;
using Content.Shared.Cloning;
using Content.Shared.Damage;
using Content.Shared.Humanoid;
using Content.Shared.Mind;
using Content.Shared.Roles.Jobs;
using Content.Shared.Sprite;
using Robust.Server.Containers;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects.Components.Localization;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Timing;
using Content.Server._Mono.Traits;

namespace Content.Server._Mono.Cloning;

/// <summary>
/// Creating the gene records, and bodies
/// </summary>
public sealed partial class GeneticCloningSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _configManager = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IRobustRandom _robustRandom = default!;
    [Dependency] private ISerializationManager _serialization = default!;
    [Dependency] private ChatSystem _chatSystem = default!;
    [Dependency] private CloningSystem _cloningSystem = default!;
    [Dependency] private ContainerSystem _containerSystem = default!;
    [Dependency] private EuiManager _euiManager = default!;
    [Dependency] private HumanoidAppearanceSystem _humanoidSystem = default!;
    [Dependency] private MaterialStorageSystem _material = default!;
    [Dependency] private MetaDataSystem _metaSystem = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedJobSystem _jobs = default!;
    [Dependency] private SharedMindSystem _mindSystem = default!;
    [Dependency] private TransformSystem _transformSystem = default!;
    [Dependency] private GrammarSystem _grammar = default!;

    /// <summary>
    /// body check for ui
    /// </summary>
    public bool CanRecord(EntityUid body, out EntityUid mindId, [NotNullWhen(false)] out string? failReason)
    {
        if (!_mindSystem.TryGetMind(body, out mindId, out var mind) || mind.UserId == null)
        {
            failReason = Loc.GetString("genetic-scan-fail-no-mind");
            return false;
        }

        if (!TryComp<HumanoidAppearanceComponent>(body, out var humanoid) ||
            HasComp<SiliconComponent>(body) || // Yet another IPC nerf, poor robots
            !_prototype.HasIndex(humanoid.Species) ||
            !HasComp<PhysicsComponent>(body))
        {
            failReason = Loc.GetString("genetic-scan-fail-incompatible");
            return false;
        }

        if (TryComp<UncloneableComponent>(body, out var uncloneable))
        {
            failReason = Loc.GetString(uncloneable.ReasonMessage);
            return false;
        }

        failReason = null;
        return true;
    }

    /// <summary>
    /// Snapshots a body
    /// </summary>
    public GeneticRecord? CreateRecord(EntityUid body, out string? failReason)
    {
        if (!CanRecord(body, out var mindId, out failReason))
            return null;

        var humanoid = Comp<HumanoidAppearanceComponent>(body);
        var physics = Comp<PhysicsComponent>(body);

        // fail chance would go here. Removed.

        var record = new GeneticRecord
        {
            Mind = mindId,
            Name = MetaData(body).EntityName,
            Species = humanoid.Species,
            Appearance = _serialization.CreateCopy(humanoid, notNullableOverride: true),
            Mass = physics.FixturesMass,
            HasBankAccount = HasComp<BankAccountComponent>(body),
            ScannedAt = _timing.CurTime, //not sure if this is needed anymore
        };

        foreach (var comp in AllComps(body))
        {
            if (comp is ITransferredByCloning)
                record.TransferredComponents.Add(_serialization.CreateCopy(comp, notNullableOverride: true));
        }

        failReason = null;
        return record;
    }

    public bool IsBeingCloned(MindComponent mind)
    {
        var query = EntityQueryEnumerator<BeingClonedComponent>();
        while (query.MoveNext(out _, out var cloned))
        {
            if (cloned.Mind == mind)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Validation stuff
    /// </summary>
    public bool TryCloning(EntityUid uid, GeneticRecord record, CloningPodComponent? clonePod = null)
    {
        if (!Resolve(uid, ref clonePod))
            return false;

        if (HasComp<ActiveCloningPodComponent>(uid))
            return false;

        if (!TryComp<MindComponent>(record.Mind, out var mind))
            return false;

        var mindEnt = new Entity<MindComponent>(record.Mind, mind);

        if (IsBeingCloned(mind))
            return false;

        if (mind.UserId == null || !_playerManager.TryGetSessionById(mind.UserId.Value, out var client))
            return false;

        if (!_prototype.TryIndex(record.Species, out var speciesPrototype))
            return false;

        var cloningCost = (int)Math.Round(record.Mass * clonePod.BiomassRequirementMultiplier);

        if (_configManager.GetCVar(CCVars.BiomassEasyMode))
            cloningCost = (int)Math.Round(cloningCost * CloningSystem.EasyModeCloningCost);

        var biomassAmount = _material.GetMaterialAmount(uid, clonePod.RequiredMaterial);

        if (biomassAmount < cloningCost)
        {
            if (clonePod.ConnectedConsole != null)
                _chatSystem.TrySendInGameICMessage(clonePod.ConnectedConsole.Value, Loc.GetString("cloning-console-chat-error", ("units", cloningCost)), InGameICChatType.Speak, false);
            return false;
        }

        _material.TryChangeMaterialAmount(uid, clonePod.RequiredMaterial, -cloningCost);
        clonePod.UsedBiomass = cloningCost;

        // I dont know if we still need this tbh
        if (record.FailChance > 0)
        {
            if (clonePod.ConnectedConsole != null)
                _chatSystem.TrySendInGameICMessage(clonePod.ConnectedConsole.Value, Loc.GetString("cloning-console-cellular-warning", ("percent", Math.Round(100 - record.FailChance * 100))), InGameICChatType.Speak, false);

            if (_robustRandom.Prob(record.FailChance))
            {
                _cloningSystem.UpdateStatus(uid, CloningPodStatus.Gore, clonePod);
                clonePod.FailedClone = true;
                AddComp<ActiveCloningPodComponent>(uid);
                return true;
            }
        }

        var mob = Spawn(speciesPrototype.Prototype, _transformSystem.GetMapCoordinates(uid));
        ApplyAppearance(mob, record.Appearance);

        if (record.HasBankAccount)
            EnsureComp<BankAccountComponent>(mob);

        _metaSystem.SetEntityName(mob, record.Name);

        foreach (var comp in record.TransferredComponents)
        {
            var copy = _serialization.CreateCopy(comp, notNullableOverride: true);
            AddComp(mob, copy, overwrite: true);
        }

        // Apparently traits are exploded at spawn and come from the profile not the character, add them again for real
        if (TryComp<AppliedTraitsComponent>(mob, out var traits))
        {
            foreach (var traitId in traits.Traits)
            {
                if (_prototype.TryIndex(traitId, out var trait))
                    EntityManager.AddComponents(mob, trait.Components, removeExisting: false);
            }
        }

        var cloneMindReturn = AddComp<BeingClonedComponent>(mob);
        cloneMindReturn.Mind = mind;
        cloneMindReturn.Parent = uid;
        _containerSystem.Insert(mob, clonePod.BodyContainer);
        _cloningSystem.UpdateStatus(uid, CloningPodStatus.NoMind, clonePod);
        _euiManager.OpenEui(new AcceptCloningEui(mindEnt, mind, _cloningSystem), client);

        AddComp<ActiveCloningPodComponent>(uid);

        // Why isnt this a component on the mind
        if (_jobs.MindTryGetJob(mindEnt, out var prototype))
        {
            foreach (var special in prototype.Special)
            {
                if (special is AddComponentSpecial)
                    special.AfterEquip(mob);
            }
        }

        return true;
    }

    private void ApplyAppearance(EntityUid target, HumanoidAppearanceComponent source)
    {
        if (!TryComp<HumanoidAppearanceComponent>(target, out var targetHumanoid))
            return;

        targetHumanoid.Species = source.Species;
        targetHumanoid.SkinColor = source.SkinColor;
        targetHumanoid.EyeColor = source.EyeColor;
        targetHumanoid.Age = source.Age;
        targetHumanoid.Height = source.Height;
        targetHumanoid.Width = source.Width;
        _humanoidSystem.SetSex(target, source.Sex, false, targetHumanoid);
        targetHumanoid.CustomBaseLayers = new(source.CustomBaseLayers);
        targetHumanoid.MarkingSet = new(source.MarkingSet);

        targetHumanoid.Gender = source.Gender;
        if (TryComp<GrammarComponent>(target, out var grammar))
            _grammar.SetGender((target, grammar), source.Gender);

        if (source.Height != 1.0f || source.Width != 1.0f)
        {
            EnsureComp<ScaleVisualsComponent>(target);
            var appearance = EnsureComp<AppearanceComponent>(target);
            _appearance.SetData(target, ScaleVisuals.Scale, new Vector2(source.Width, source.Height), appearance);
        }

        Dirty(target, targetHumanoid);
    }
}
