using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server.Administration.Logs;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Construction; // Frontier
using Content.Server.Fluids.EntitySystems;
using Content.Server.Lathe.Components;
using Content.Server.Materials;
using Content.Server.Popups;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Server.Stack;
using Content.Shared.Atmos;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.UserInterface;
using Content.Shared.Database;
using Content.Shared.DeviceLinking.Events; // Mono
using Content.Server.DeviceLinking.Systems; // Mono
using Content.Shared.Emag.Components;
using Content.Shared.Emag.Systems;
using Content.Shared.Examine;
using Content.Shared.Lathe;
using Content.Shared.Lathe.Prototypes;
using Content.Shared.Materials;
using Content.Shared.Power;
using Content.Shared.ReagentSpeed;
using Content.Shared.Research.Components;
using Content.Shared.Research.Prototypes;
using JetBrains.Annotations;
using Robust.Server.Containers;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Content.Shared.Cargo.Components; // Frontier
using Content.Server._NF.Contraband.Systems; // Frontier
using Robust.Shared.Containers; // Frontier

namespace Content.Server.Lathe
{
    [UsedImplicitly]
    public sealed class LatheSystem : SharedLatheSystem
    {
        [Dependency] private readonly IGameTiming _timing = default!;
        [Dependency] private readonly IPrototypeManager _proto = default!;
        [Dependency] private readonly IAdminLogManager _adminLogger = default!;
        [Dependency] private readonly AtmosphereSystem _atmosphere = default!;
        [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
        [Dependency] private readonly SharedAudioSystem _audio = default!;
        [Dependency] private readonly ContainerSystem _container = default!;
        [Dependency] private readonly EmagSystem _emag = default!;
        [Dependency] private readonly UserInterfaceSystem _uiSys = default!;
        [Dependency] private readonly MaterialStorageSystem _materialStorage = default!;
        [Dependency] private readonly PopupSystem _popup = default!;
        [Dependency] private readonly PuddleSystem _puddle = default!;
        [Dependency] private readonly ReagentSpeedSystem _reagentSpeed = default!;
        [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;
        [Dependency] private readonly StackSystem _stack = default!;
        [Dependency] private readonly TransformSystem _transform = default!;
        [Dependency] private readonly ContrabandTurnInSystem _contraband = default!; // Frontier
        [Dependency] private readonly DeviceLinkSystem _deviceLink = default!; // Mono

        /// <summary>
        /// Per-tick cache
        /// </summary>
        private readonly List<GasMixture> _environments = new();
        private readonly HashSet<ProtoId<LatheRecipePrototype>> _availableRecipes = new();

        // Mono - re-check whether we can continue production if current recipe is frozen
        private TimeSpan _checkAccumulator = TimeSpan.FromSeconds(0);
        private TimeSpan _checkSpacing = TimeSpan.FromSeconds(1);

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<LatheComponent, GetMaterialWhitelistEvent>(OnGetWhitelist);
            SubscribeLocalEvent<LatheComponent, MapInitEvent>(OnMapInit);
            SubscribeLocalEvent<LatheComponent, PowerChangedEvent>(OnPowerChanged);
            SubscribeLocalEvent<LatheComponent, TechnologyDatabaseModifiedEvent>(OnDatabaseModified);
            SubscribeLocalEvent<LatheComponent, ResearchRegistrationChangedEvent>(OnResearchRegistrationChanged);

            SubscribeLocalEvent<LatheComponent, LatheQueueRecipeMessage>(OnLatheQueueRecipeMessage);
            SubscribeLocalEvent<LatheComponent, LatheSyncRequestMessage>(OnLatheSyncRequestMessage);
            // Mono
            SubscribeLocalEvent<LatheComponent, LatheSetLoopingMessage>(OnLatheSetLoopingMessage);
            SubscribeLocalEvent<LatheComponent, LatheSetSkipMessage>(OnLatheSetSkipMessage);
            SubscribeLocalEvent<LatheComponent, LatheRecipeCancelMessage>(OnLatheRecipeCancelMessage);

            SubscribeLocalEvent<LatheComponent, BeforeActivatableUIOpenEvent>((u, c, _) => UpdateUserInterfaceState(u, c));
            SubscribeLocalEvent<LatheComponent, MaterialAmountChangedEvent>(OnMaterialAmountChanged);
            SubscribeLocalEvent<TechnologyDatabaseComponent, LatheGetRecipesEvent>(OnGetRecipes);
            SubscribeLocalEvent<EmagLatheRecipesComponent, LatheGetRecipesEvent>(GetEmagLatheRecipes);

            //Frontier: upgradeable parts
            SubscribeLocalEvent<LatheComponent, RefreshPartsEvent>(OnPartsRefresh);
            SubscribeLocalEvent<LatheComponent, UpgradeExamineEvent>(OnUpgradeExamine);

            // Mono
            SubscribeLocalEvent<LatheComponent, SignalReceivedEvent>(OnSignalReceived);
            SubscribeLocalEvent<LatheHeatProducingComponent, ExaminedEvent>(OnHeatExamine);
        }
        public override void Update(float frameTime)
        {
            // Mono
            _checkAccumulator += TimeSpan.FromSeconds(frameTime);
            if (_checkAccumulator > _checkSpacing)
            {
                _checkAccumulator -= _checkSpacing;
                var rebootQuery = EntityQueryEnumerator<LatheComponent>();
                while (rebootQuery.MoveNext(out var uid, out var comp))
                {
                    // try see if we can reboot if we aren't producing
                    if (HasComp<LatheProducingComponent>(uid))
                        continue;

                    TryStartProducing(uid, comp);
                }
            }

            var query = EntityQueryEnumerator<LatheProducingComponent, LatheComponent>();
            while (query.MoveNext(out var uid, out var comp, out var lathe))
            {
                if (lathe.CurrentRecipe == null)
                    continue;

                if (_timing.CurTime - comp.StartTime >= comp.ProductionLength)
                    FinishProducing(uid, lathe);
            }

            // Mono - now checks all and not only producing lathes in order to check air temperature
            var heatQuery = EntityQueryEnumerator<LatheHeatProducingComponent, LatheComponent, TransformComponent>();
            while (heatQuery.MoveNext(out var uid, out var heatComp, out var latheComp, out var xform))
            {
                heatComp.UpdateAccumulator += TimeSpan.FromSeconds(frameTime);
                if (heatComp.UpdateAccumulator < heatComp.UpdateSpacing)
                    continue;
                heatComp.UpdateAccumulator -= heatComp.UpdateSpacing;

                var position = _transform.GetGridTilePositionOrDefault((uid, xform));
                _environments.Clear();

                if (_atmosphere.GetTileMixture(xform.GridUid, xform.MapUid, position, true) is { } tileMix)
                    _environments.Add(tileMix);

                if (xform.GridUid != null)
                {
                    var enumerator = _atmosphere.GetAdjacentTileMixtures(xform.GridUid.Value, position, false, true);
                    while (enumerator.MoveNext(out var mix))
                    {
                        _environments.Add(mix);
                    }
                }

                if (_environments.Count == 0)
                    continue;

                var avgTemp = 0f;
                var totalHeatCap = 0f;
                foreach (var env in _environments)
                {
                    avgTemp += env.Temperature;;
                    totalHeatCap += _atmosphere.GetHeatCapacity(env, true);
                }
                avgTemp /= _environments.Count;
                var wasHot = heatComp.IsHot;
                heatComp.IsHot = heatComp.TemperatureCap != null && avgTemp + heatComp.EnergyPerSecond / totalHeatCap > heatComp.TemperatureCap;
                if (heatComp.IsHot)
                    continue;
                else if (wasHot && !latheComp.Paused)
                    TryStartProducing(uid, latheComp);

                if (!HasComp<LatheProducingComponent>(uid))
                    continue;

                var heatPerTile = heatComp.EnergyPerSecond / _environments.Count;
                foreach (var env in _environments)
                {
                    _atmosphere.AddHeat(env, heatPerTile);
                }
            }
        }

        private void OnGetWhitelist(EntityUid uid, LatheComponent component, ref GetMaterialWhitelistEvent args)
        {
            if (args.Storage != uid)
                return;
            var materialWhitelist = new List<ProtoId<MaterialPrototype>>();
            var recipes = GetAvailableRecipes(uid, component, true);
            foreach (var id in recipes)
            {
                if (!_proto.TryIndex(id, out var proto))
                    continue;
                foreach (var (mat, _) in proto.Materials)
                {
                    if (!materialWhitelist.Contains(mat))
                    {
                        materialWhitelist.Add(mat);
                    }
                }
            }

            var combined = args.Whitelist.Union(materialWhitelist).ToList();
            args.Whitelist = combined;
        }

        [PublicAPI]
        public bool TryGetAvailableRecipes(EntityUid uid, [NotNullWhen(true)] out List<ProtoId<LatheRecipePrototype>>? recipes, [NotNullWhen(true)] LatheComponent? component = null, bool getUnavailable = false)
        {
            recipes = null;
            if (!Resolve(uid, ref component))
                return false;
            recipes = GetAvailableRecipes(uid, component, getUnavailable);
            return true;
        }

        public List<ProtoId<LatheRecipePrototype>> GetAvailableRecipes(EntityUid uid, LatheComponent component, bool getUnavailable = false)
        {
            _availableRecipes.Clear();
            AddRecipesFromPacks(_availableRecipes, component.StaticPacks);
            var ev = new LatheGetRecipesEvent(uid, getUnavailable)
            {
                Recipes = _availableRecipes
            };
            RaiseLocalEvent(uid, ev);
            return ev.Recipes.ToList();
        }

        public bool TryAddToQueue(EntityUid uid, LatheRecipePrototype recipe, int quantity, LatheComponent? component = null, // Frontier: add quantity
                                  bool canDebt = false) // Mono
        {
            if (!Resolve(uid, ref component))
                return false;

            // Frontier: argument check
            if (quantity <= 0)
                return false;
            // Frontier: argument check

            // Mono - debt
            if (!canDebt && !CanProduceEnd((uid, component), recipe, quantity)) // Frontier: 1<quantity
                return false;

            // Frontier: queue up a batch
            if (component.Queue.Count > 0 && component.Queue[^1].Recipe.ID == recipe.ID)
                component.Queue[^1].ItemsRequested += quantity;
            else
                component.Queue.Add(new LatheRecipeBatch(recipe, 0, quantity));
            // End Frontier
            // component.Queue.Add(recipe); // Frontier

            return true;
        }

        public bool TryStartProducing(EntityUid uid, LatheComponent? component = null)
        {
            if (!Resolve(uid, ref component))
                return false;
            // Mono - pause
            if (component.Paused
                || component.CurrentRecipe != null
                || component.Queue.Count <= 0
                || !this.IsPowered(uid, EntityManager)
                || TryComp<LatheHeatProducingComponent>(uid, out var heat) && heat.IsHot) // Mono - if you want to add more conditions turn this into an event please
                return false;

            // Frontier: handle batches
            var batch = component.Queue.First();
            var recipe = batch.Recipe;
            // <Mono> - resources now consumed as the production goes
            if (!CanProduce(uid, recipe, 1, component))
            {
                if (component.SkipBad)
                {
                    component.Queue.RemoveAt(0);
                    if (component.Loop)
                        component.Queue.Add(batch);
                    UpdateUserInterfaceState(uid, component);
                }
                return false;
            }

            foreach (var (mat, amount) in recipe.Materials)
            {
                var adjustedAmount = recipe.ApplyMaterialDiscount
                    ? (int) (-amount * component.FinalMaterialUseMultiplier) // Frontier: MaterialUseMultiplier<FinalMaterialUseMultiplier
                    : -amount;

                _materialStorage.TryChangeMaterialAmount(uid, mat, adjustedAmount);
            }
            // </Mono>

            batch.ItemsPrinted++;
            if (batch.ItemsPrinted >= batch.ItemsRequested || batch.ItemsPrinted < 0) // Rollover sanity check
                component.Queue.RemoveAt(0);
            // End Frontier

            var time = _reagentSpeed.ApplySpeed(uid, recipe.CompleteTime) * component.TimeMultiplier;

            var lathe = EnsureComp<LatheProducingComponent>(uid);
            lathe.StartTime = _timing.CurTime;
            lathe.ProductionLength = time * component.FinalTimeMultiplier; // Frontier: TimeMultiplier<FinalTimeMultiplier
            component.CurrentRecipe = recipe;

            var ev = new LatheStartPrintingEvent(recipe);
            RaiseLocalEvent(uid, ref ev);

            _audio.PlayPvs(component.ProducingSound, uid);
            UpdateRunningAppearance(uid, true);
            UpdateUserInterfaceState(uid, component);

            if (time == TimeSpan.Zero)
            {
                FinishProducing(uid, component, lathe);
            }
            return true;
        }

        public void FinishProducing(EntityUid uid, LatheComponent? comp = null, LatheProducingComponent? prodComp = null)
        {
            if (!Resolve(uid, ref comp, ref prodComp, false))
                return;

            if (comp.CurrentRecipe != null)
            {
                if (comp.CurrentRecipe.Result is { } resultProto)
                {
                    var result = Spawn(resultProto, Transform(uid).Coordinates);

                    // Frontier: adjust price before merge (stack prices changed once)
                    if (result.Valid)
                    {
                        ModifyPrintedEntityPrice(uid, comp, result);

                        _contraband.ClearContrabandValue(result);
                    }
                    // End Frontier

                    _stack.TryMergeToContacts(result);
                }

                if (comp.CurrentRecipe.ResultReagents is { } resultReagents &&
                    comp.ReagentOutputSlotId is { } slotId)
                {
                    var toAdd = new Solution(
                        resultReagents.Select(p => new ReagentQuantity(p.Key.Id, p.Value, null)));

                    // dispense it in the container if we have it and dump it if we don't
                    if (_container.TryGetContainer(uid, slotId, out var container) &&
                        container.ContainedEntities.Count == 1 &&
                        _solution.TryGetFitsInDispenser(container.ContainedEntities.First(), out var solution, out _))
                    {
                        _solution.AddSolution(solution.Value, toAdd);
                    }
                    else
                    {
                        _popup.PopupEntity(Loc.GetString("lathe-reagent-dispense-no-container", ("name", uid)), uid);
                        _puddle.TrySpillAt(uid, toAdd, out _);
                    }
                }

                // <Mono>
                if (comp.Loop)
                    TryAddToQueue(uid, comp.CurrentRecipe, 1, comp, true);

                _deviceLink.SendSignal(uid, comp.ProducedPort, true);
                // </Mono>
            }

            comp.CurrentRecipe = null;
            prodComp.StartTime = _timing.CurTime;

            if (!TryStartProducing(uid, comp))
            {
                RemCompDeferred(uid, prodComp);
                UpdateUserInterfaceState(uid, comp);
                UpdateRunningAppearance(uid, false);
            }
        }

        public void UpdateUserInterfaceState(EntityUid uid, LatheComponent? component = null)
        {
            if (!Resolve(uid, ref component))
                return;

            var producing = component.CurrentRecipe ?? component.Queue.FirstOrDefault()?.Recipe; // Frontier: add ?.Recipe

            var state = new LatheUpdateState(GetAvailableRecipes(uid, component), component.Queue, producing, component.Loop, component.SkipBad); // Mono
            _uiSys.SetUiState(uid, LatheUiKey.Key, state);
        }

        /// <summary>
        /// Adds every unlocked recipe from each pack to the recipes list.
        /// </summary>
        public void AddRecipesFromDynamicPacks(ref LatheGetRecipesEvent args, TechnologyDatabaseComponent database, IEnumerable<ProtoId<LatheRecipePackPrototype>> packs)
        {
            foreach (var id in packs)
            {
                var pack = _proto.Index(id);
                foreach (var recipe in pack.Recipes)
                {
                    if (args.getUnavailable || database.UnlockedRecipes.Contains(recipe))
                        args.Recipes.Add(recipe);
                }
            }
        }

        private void OnGetRecipes(EntityUid uid, TechnologyDatabaseComponent component, LatheGetRecipesEvent args)
        {
            if (uid != args.Lathe || !TryComp<LatheComponent>(uid, out var latheComponent))
                return;

            AddRecipesFromDynamicPacks(ref args, component, latheComponent.DynamicPacks);
        }

        private void GetEmagLatheRecipes(EntityUid uid, EmagLatheRecipesComponent component, LatheGetRecipesEvent args)
        {
            if (uid != args.Lathe)
                return;

            if (!args.getUnavailable && !_emag.CheckFlag(uid, EmagType.Interaction))
                return;

            AddRecipesFromPacks(args.Recipes, component.EmagStaticPacks);

            if (TryComp<TechnologyDatabaseComponent>(uid, out var database))
                AddRecipesFromDynamicPacks(ref args, database, component.EmagDynamicPacks);
        }

        private void OnMaterialAmountChanged(EntityUid uid, LatheComponent component, ref MaterialAmountChangedEvent args)
        {
            UpdateUserInterfaceState(uid, component);
        }

        /// <summary>
        /// Initialize the UI and appearance.
        /// Appearance requires initialization or the layers break
        /// </summary>
        private void OnMapInit(EntityUid uid, LatheComponent component, MapInitEvent args)
        {
            _appearance.SetData(uid, LatheVisuals.IsInserting, false);
            _appearance.SetData(uid, LatheVisuals.IsRunning, false);

            _materialStorage.UpdateMaterialWhitelist(uid);
            // New Frontiers - Lathe Upgrades - initialization of upgrade coefficients
            // This code is licensed under AGPLv3. See AGPLv3.txt
            component.FinalTimeMultiplier = component.TimeMultiplier;
            component.FinalMaterialUseMultiplier = component.MaterialUseMultiplier;
            // End of modified code
            // <Mono>
            _deviceLink.EnsureSinkPorts(uid, component.PausePort, component.ResumePort);
            _deviceLink.EnsureSourcePorts(uid, component.ProducedPort);
            // </Mono>
        }

        /// <summary>
        /// Sets the machine sprite to either play the running animation
        /// or stop.
        /// </summary>
        private void UpdateRunningAppearance(EntityUid uid, bool isRunning)
        {
            _appearance.SetData(uid, LatheVisuals.IsRunning, isRunning);
        }

        private void OnPowerChanged(EntityUid uid, LatheComponent component, ref PowerChangedEvent args)
        {
            if (!args.Powered)
            {
                RemComp<LatheProducingComponent>(uid);
                UpdateRunningAppearance(uid, false);
            }
            else if (component.CurrentRecipe != null)
            {
                EnsureComp<LatheProducingComponent>(uid);
                TryStartProducing(uid, component);
            }
        }

        private void OnDatabaseModified(EntityUid uid, LatheComponent component, ref TechnologyDatabaseModifiedEvent args)
        {
            UpdateUserInterfaceState(uid, component);
        }

        private void OnResearchRegistrationChanged(EntityUid uid, LatheComponent component, ref ResearchRegistrationChangedEvent args)
        {
            UpdateUserInterfaceState(uid, component);
        }

        protected override bool HasRecipe(EntityUid uid, LatheRecipePrototype recipe, LatheComponent component)
        {
            return GetAvailableRecipes(uid, component).Contains(recipe.ID);
        }

        #region UI Messages

        private void OnLatheQueueRecipeMessage(EntityUid uid, LatheComponent component, LatheQueueRecipeMessage args)
        {
            if (_proto.TryIndex(args.ID, out LatheRecipePrototype? recipe))
            {
                // Frontier: batching recipes
                if (TryAddToQueue(uid, recipe, args.Quantity, component))
                {
                    _adminLogger.Add(LogType.Action,
                        LogImpact.Low,
                        $"{ToPrettyString(args.Actor):player} queued {args.Quantity} {GetRecipeName(recipe)} at {ToPrettyString(uid):lathe}");
                }
                // End Frontier
            }
            TryStartProducing(uid, component);
            UpdateUserInterfaceState(uid, component);
        }

        private void OnLatheSyncRequestMessage(EntityUid uid, LatheComponent component, LatheSyncRequestMessage args)
        {
            UpdateUserInterfaceState(uid, component);
        }

        // <Mono>
        private void OnLatheSetLoopingMessage(Entity<LatheComponent> ent, ref LatheSetLoopingMessage args)
        {
            ent.Comp.Loop = args.ShouldLoop;
            UpdateUserInterfaceState(ent, ent.Comp);
        }

        private void OnLatheSetSkipMessage(Entity<LatheComponent> ent, ref LatheSetSkipMessage args)
        {
            ent.Comp.SkipBad = args.ShouldSkip;
            UpdateUserInterfaceState(ent, ent.Comp);
        }

        private void OnLatheRecipeCancelMessage(Entity<LatheComponent> ent, ref LatheRecipeCancelMessage args)
        {
            var id = args.Index;
            if (ent.Comp.Queue.RemoveAll(recipe => recipe.Index == id) != 0)
                UpdateUserInterfaceState(ent, ent.Comp);
        }
        // </Mono>
        #endregion


        // New Frontiers - Lathe Upgrades - upgrading lathe speed through machine parts
        // This code is licensed under AGPLv3. See AGPLv3.txt
        private void OnPartsRefresh(EntityUid uid, LatheComponent component, RefreshPartsEvent args)
        {
            var printTimeRating = args.PartRatings[component.MachinePartPrintSpeed];
            var materialUseRating = args.PartRatings[component.MachinePartMaterialUse];

            component.FinalTimeMultiplier = component.TimeMultiplier * MathF.Pow(component.PartRatingPrintTimeMultiplier, printTimeRating - 1);
            component.FinalMaterialUseMultiplier = component.MaterialUseMultiplier * MathF.Pow(component.PartRatingMaterialUseMultiplier, materialUseRating - 1);
            Dirty(uid, component);
        }

        private void OnUpgradeExamine(EntityUid uid, LatheComponent component, UpgradeExamineEvent args)
        {
            args.AddPercentageUpgrade("lathe-component-upgrade-speed", 1 / component.FinalTimeMultiplier);
            args.AddPercentageUpgrade("lathe-component-upgrade-material-use", component.FinalMaterialUseMultiplier);
        }

        // Mono
        private void OnHeatExamine(Entity<LatheHeatProducingComponent> ent, ref ExaminedEvent args)
        {
            if (ent.Comp.IsHot)
                args.PushMarkup(Loc.GetString("lathe-heat-producing-too-hot"));
        }

        // Frontier: modify item value
        private void ModifyPrintedEntityPrice(EntityUid uid, LatheComponent component, EntityUid target)
        {
            // Cannot reduce value, leave item as-is
            if (component.ProductValueModifier == null
            || !float.IsFinite(component.ProductValueModifier.Value)
            || component.ProductValueModifier < 0f)
                return;

            if (TryComp<StackPriceComponent>(target, out var stackPrice))
            {
                if (stackPrice.Price > 0)
                    stackPrice.Price *= component.ProductValueModifier.Value;
            }
            if (TryComp<StaticPriceComponent>(target, out var staticPrice))
            {
                if (staticPrice.Price > 0)
                    staticPrice.Price *= component.ProductValueModifier.Value;
            }

            // Recurse into contained entities
            if (TryComp<ContainerManagerComponent>(target, out var containers))
            {
                foreach (var container in containers.Containers.Values)
                {
                    foreach (var ent in container.ContainedEntities)
                    {
                        ModifyPrintedEntityPrice(uid, component, ent);
                    }
                }
            }
        }
        // End Frontier

        // Mono
        private void OnSignalReceived(Entity<LatheComponent> ent, ref SignalReceivedEvent args)
        {
            if (args.Port == ent.Comp.PausePort)
            {
                TryPause((ent, ent.Comp));
            }
            else if (args.Port == ent.Comp.ResumePort)
            {
                TryUnpause((ent, ent.Comp));
            }
        }

        public void TryPause(Entity<LatheComponent?> ent)
        {
            if (!Resolve(ent, ref ent.Comp))
                return;

            ent.Comp.Paused = true;
        }

        public void TryUnpause(Entity<LatheComponent?> ent)
        {
            if (!Resolve(ent, ref ent.Comp))
                return;

            bool wasPaused = ent.Comp.Paused;
            ent.Comp.Paused = false;
            if (wasPaused)
                TryStartProducing(ent, ent.Comp);
        }
    }
}
