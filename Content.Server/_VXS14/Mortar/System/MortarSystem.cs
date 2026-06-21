using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Random;
using Content.Shared._VXS14.Mortar;
using Robust.Shared.IoC;
using Content.Server.Explosion.EntitySystems;
using Content.Shared.Verbs;
using Content.Server.Administration.Commands;
using Content.Server.EUI;
using Robust.Server.Player;
using System.Numerics;
using Robust.Shared.Utility;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Containers;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;
using Content.Server.ArtilleryDetection.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;

namespace Content.Server._VXS14.Mortar
{
    public sealed class MortarSystem : EntitySystem
    {
        [Dependency] private readonly SharedTransformSystem _transform = default!;
        [Dependency] protected readonly EntityManager EntityManager = default!;
        [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly ExplosionSystem _explosionSystem = default!;
        [Dependency] private readonly IPlayerManager _playerManager = default!;
        [Dependency] private readonly SharedAudioSystem _audioSystem = default!;
        [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<SharedMortarComponent, GetVerbsEvent<ExamineVerb>>(OnMortarVerbUtility);
            SubscribeLocalEvent<SharedMortarComponent, EntInsertedIntoContainerMessage>(OnItemInserted);
            SubscribeLocalEvent<SharedMortarComponent, InteractUsingEvent>(OnInteractUsing,
                after: new[] { typeof(ItemSlotsSystem) });
            SubscribeLocalEvent<SharedMortarComponent, MortarShellLoadDoAfterEvent>(OnMortarShellLoadDoAfter);
        }

        private void OnMortarVerbUtility(EntityUid uid, SharedMortarComponent component, GetVerbsEvent<ExamineVerb> args)
        {
            // Always show the mortar UI verb regardless of whether a shell is loaded
            var verb = new ExamineVerb
            {
                Act = () => OnUsed(uid, args.User),
            };
            verb.Text = Loc.GetString("Open Mortar UI");
            verb.Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/_VXS14/Interface/mortarIcon.png"));
            args.Verbs.Add(verb);
        }

        private void OnItemInserted(EntityUid uid, SharedMortarComponent component, EntInsertedIntoContainerMessage args)
        {
            // Play insert sound when a mortar shell enters the chamber
            if (HasComp<SharedMortarShellComponent>(args.Entity) && args.Container.ID == "mortar_chamber")
            {
                if (TryComp<SharedMortarShellComponent>(args.Entity, out var shellComponent) && shellComponent.InsertSound != null)
                {
                    _audioSystem.PlayPvs(new SoundPathSpecifier(shellComponent.InsertSound), uid);
                }
                // Firing is handled by DoAfter started in OnInteractUsing
            }
        }

        private void OnInteractUsing(EntityUid uid, SharedMortarComponent component, InteractUsingEvent args)
        {
            // Only proceed if ItemSlotsSystem successfully handled the insertion
            if (!args.Handled)
                return;

            // After ItemSlotsSystem has processed the interaction, check if the used item
            // is now loaded in the mortar chamber and start the loading DoAfter.
            var sysMan = IoCManager.Resolve<IEntitySystemManager>();
            var itemSlots = sysMan.GetEntitySystem<ItemSlotsSystem>();
            var rocket = itemSlots.GetItemOrNull(uid, "mortar_chamber");

            if (rocket == null || rocket.Value != args.Used || !HasComp<SharedMortarShellComponent>(rocket.Value))
                return;

            var doAfterArgs = new DoAfterArgs(EntityManager, args.User, component.LoadDelay,
                new MortarShellLoadDoAfterEvent(), uid)
            {
                BreakOnDamage = true,
                BreakOnMove = true,
                NeedHand = false,
            };
            _doAfter.TryStartDoAfter(doAfterArgs);
        }

        private void OnMortarShellLoadDoAfter(EntityUid uid, SharedMortarComponent component, MortarShellLoadDoAfterEvent args)
        {
            if (args.Cancelled)
                return;

            FireMortar(uid, component, component.TargetOffsetX, component.TargetOffsetY);
        }

        public void FireMortar(EntityUid mortarUid, SharedMortarComponent mortarComp, float offsetX, float offsetY)
        {
            var sysMan = IoCManager.Resolve<IEntitySystemManager>();
            var itemSlots = sysMan.GetEntitySystem<ItemSlotsSystem>();
            var rocket = itemSlots.GetItemOrNull(mortarUid, "mortar_chamber");

            if (rocket == null)
            {
                Logger.WarningS("mortar", "No shell in mortar chamber!");
                return;
            }

            var entMan = IoCManager.Resolve<IEntityManager>();
            var transformSystem = entMan.System<SharedTransformSystem>();
            var mortarPosition = transformSystem.GetMapCoordinates(mortarUid);

            // Calculate the target position based on offsets
            var targetPosition = new MapCoordinates(
                new Vector2(
                    mortarPosition.X + offsetX,
                    mortarPosition.Y + offsetY),
                mortarPosition.MapId);

            // Prevent shooting at too close a range
            var distanceFromMortar = (targetPosition.Position - mortarPosition.Position).Length();
            var minDistance = mortarComp.MinSafeDistance;
            if (distanceFromMortar < minDistance)
            {
                var direction = targetPosition.Position - mortarPosition.Position;
                if (direction.Length() > 0)
                {
                    direction = Vector2.Normalize(direction);
                    var adjustedPosition = mortarPosition.Position + direction * minDistance;
                    targetPosition = new MapCoordinates(adjustedPosition, mortarPosition.MapId);
                }
                else
                {
                    targetPosition = new MapCoordinates(
                        new Vector2(mortarPosition.X + minDistance, mortarPosition.Y),
                        mortarPosition.MapId);
                }
            }

            entMan.TryGetComponent<SharedMortarShellComponent>(rocket, out var comp);
            Logger.InfoS("mortar", $"Shell component retrieved: {comp != null}");

            // Play fire sound at mortar position
            if (comp?.FireSound != null)
            {
                var mortarCoords = entMan.GetComponent<TransformComponent>(mortarUid).Coordinates;
                _audioSystem.PlayPvs(new SoundPathSpecifier(comp.FireSound), mortarCoords);
            }

            // Calculate distance for delay
            var distance = (targetPosition.Position - mortarPosition.Position).Length();
            var delay = (int)(distance * (comp?.DelayPerTile ?? 0.1f) * 1000);

            var timerManager = IoCManager.Resolve<ITimerManager>();
            timerManager.AddTimer(new Timer(delay, false, () =>
            {
                // Play pre-explosion sound at target position
                if (comp?.PreExplosionSound != null)
                {
                    var mapSystem = sysMan.GetEntitySystem<SharedMapSystem>();
                    var mapEntity = mapSystem.GetMapOrInvalid(targetPosition.MapId);
                    var targetCoords = transformSystem.ToCoordinates(mapEntity, targetPosition);
                    _audioSystem.PlayPvs(new SoundPathSpecifier(comp.PreExplosionSound), targetCoords);
                }

                timerManager.AddTimer(new Timer(500, false, () =>
                {
                    Logger.InfoS("mortar", "=== TIMER FIRED ===");
                    Logger.InfoS("mortar", $"Rocket entity: {rocket}");
                    Logger.InfoS("mortar", $"Target position: {targetPosition}");

                    var shellName = "Shell";
                    if (rocket != null && entMan.TryGetComponent<MetaDataComponent>(rocket.Value, out var shellMetaData))
                    {
                        shellName = shellMetaData.EntityName ?? "Shell";
                    }

                    entMan.DeleteEntity(rocket);

                    if (comp != null)
                    {
                        var distanceFired = (targetPosition.Position - mortarPosition.Position).Length();
                        var accuracyModifier = Math.Max(0.1f, mortarComp.BaseAccuracy - (distanceFired * mortarComp.AccuracyDegradation));

                        var artillerySystem = sysMan.GetEntitySystem<ArtilleryDetectionSystem>();
                        if (artillerySystem == null)
                        {
                            Logger.ErrorS("mortar", "ArtilleryDetectionSystem is null!");
                            return;
                        }

                        var mortarName = "Mortar";
                        if (entMan.TryGetComponent<MetaDataComponent>(mortarUid, out var metaData))
                        {
                            mortarName = metaData.EntityName ?? "Mortar";
                        }

                        var weaponType = $"{mortarName} ({shellName})";
                        artillerySystem.OnArtilleryFired(mortarPosition, weaponType, IoCManager.Resolve<IGameTiming>().CurTime, mortarName, shellName);

                        if (comp.UseDirectExplosion)
                        {
                            var adjustedTotalIntensity = comp.TotalIntensity * accuracyModifier;
                            var adjustedSlope = comp.Slope * accuracyModifier;
                            var adjustedMaxTileIntensity = comp.MaxTileIntensity * accuracyModifier;
                            sysMan.GetEntitySystem<ExplosionSystem>().QueueExplosion(targetPosition, comp.Type, adjustedTotalIntensity, adjustedSlope, adjustedMaxTileIntensity, null);
                        }
                        else if (!string.IsNullOrEmpty(comp.ExplosionEntity))
                        {
                            Logger.InfoS("mortar", $"Using ExplosionEntity: {comp.ExplosionEntity}");
                            entMan.SpawnEntity(comp.ExplosionEntity, targetPosition);
                        }
                        else
                        {
                            Logger.WarningS("mortar", "Shell has neither UseDirectExplosion nor ExplosionEntity!");
                        }
                    }
                }));
            }));
        }

        private void OnUsed(EntityUid uid, EntityUid user, bool canReach = true)
        {
            if (_playerManager.TryGetSessionByEntity(user, out var session))
            {
                var eui = IoCManager.Resolve<EuiManager>();
                var ui = new MortarEui(uid);
                eui.OpenEui(ui, session);
            }
        }

    }
}

