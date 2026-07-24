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
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Content.Shared.Popups;
using Robust.Shared.Map.Components;

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
        [Dependency] private readonly IMapManager _mapManager = default!;
        [Dependency] private readonly SharedPopupSystem _popup = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<SharedMortarComponent, GetVerbsEvent<ExamineVerb>>(OnMortarVerbUtility);
            SubscribeLocalEvent<SharedMortarComponent, ActivateInWorldEvent>(OnActivateInWorld);
            SubscribeLocalEvent<SharedMortarComponent, EntInsertedIntoContainerMessage>(OnItemInserted);
            SubscribeLocalEvent<SharedMortarComponent, InteractUsingEvent>(OnInteractUsing,
                after: new[] { typeof(ItemSlotsSystem) });
            SubscribeLocalEvent<SharedMortarComponent, MortarShellLoadDoAfterEvent>(OnMortarShellLoadDoAfter);
        }

        private void OnMortarVerbUtility(EntityUid uid, SharedMortarComponent component, GetVerbsEvent<ExamineVerb> args)
        {
            var verb = new ExamineVerb
            {
                Act = () => OnUsed(uid, args.User),
            };
            verb.Text = Loc.GetString("Open Mortar UI");
            verb.Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/_VXS14/Interface/mortarIcon.png"));
            args.Verbs.Add(verb);
        }

        private void OnActivateInWorld(EntityUid uid, SharedMortarComponent component, ActivateInWorldEvent args)
        {
            if (args.Handled)
                return;

            args.Handled = true;
            OnUsed(uid, args.User);
        }

        private void OnItemInserted(EntityUid uid, SharedMortarComponent component, EntInsertedIntoContainerMessage args)
        {
            if (HasComp<SharedMortarShellComponent>(args.Entity) && args.Container.ID == "mortar_chamber")
            {
                if (TryComp<SharedMortarShellComponent>(args.Entity, out var shellComponent) && shellComponent.InsertSound != null)
                {
                    _audioSystem.PlayPvs(shellComponent.InsertSound, uid);
                }
            }
        }

        private void OnInteractUsing(EntityUid uid, SharedMortarComponent component, InteractUsingEvent args)
        {
            if (!args.Handled)
                return;

            var sysMan = IoCManager.Resolve<IEntitySystemManager>();
            var itemSlots = sysMan.GetEntitySystem<ItemSlotsSystem>();
            var rocket = itemSlots.GetItemOrNull(uid, "mortar_chamber");

            if (rocket == null || rocket.Value != args.Used || !HasComp<SharedMortarShellComponent>(rocket.Value))
                return;

            component.CurrentLoader = args.User;

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
            {
                var sysMan = IoCManager.Resolve<IEntitySystemManager>();
                var itemSlots = sysMan.GetEntitySystem<ItemSlotsSystem>();
                if (itemSlots.TryGetSlot(uid, "mortar_chamber", out var slot))
                    itemSlots.TryEjectToHands(uid, slot, component.CurrentLoader);
                component.CurrentLoader = null;
                return;
            }

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

            if (mortarComp.CurrentLoader is { } loader && HasCeilingAbove(mortarUid))
            {
                _popup.PopupClient("Cannot fire, ceiling above.", mortarUid, loader);
                Logger.WarningS("mortar", "Cannot fire: ceiling detected above mortar");
                return;
            }

            offsetX = Math.Clamp(offsetX, mortarComp.MinOffsetX, mortarComp.MaxOffsetX);
            offsetY = Math.Clamp(offsetY, mortarComp.MinOffsetY, mortarComp.MaxOffsetY);

            var entMan = IoCManager.Resolve<IEntityManager>();
            var transformSystem = entMan.System<SharedTransformSystem>();
            var mortarPosition = transformSystem.GetMapCoordinates(mortarUid);

            var targetPosition = new MapCoordinates(
                new Vector2(
                    mortarPosition.X + offsetX,
                    mortarPosition.Y + offsetY),
                mortarPosition.MapId);

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

            var shellName = "Shell";
            if (entMan.TryGetComponent<MetaDataComponent>(rocket.Value, out var shellMetaData))
            {
                shellName = shellMetaData.EntityName ?? "Shell";
            }

            if (comp?.FireSound != null)
            {
                var mortarCoords = entMan.GetComponent<TransformComponent>(mortarUid).Coordinates;
                _audioSystem.PlayPvs(comp.FireSound, mortarCoords);
            }

            var distance = (targetPosition.Position - mortarPosition.Position).Length();
            var delay = (int)(distance * (comp?.DelayPerTile ?? 0.1f) * 1000);

            entMan.DeleteEntity(rocket);
            mortarComp.CurrentLoader = null;

            var preExplosionSound = comp?.PreExplosionSound;
            var useDirectExplosion = comp?.UseDirectExplosion ?? true;
            var explosionType = comp?.Type ?? "Default";
            var totalIntensity = comp?.TotalIntensity ?? 105f;
            var slope = comp?.Slope ?? 200f;
            var maxTileIntensity = comp?.MaxTileIntensity ?? 2f;
            var explosionEntity = comp?.ExplosionEntity;

            var timerManager = IoCManager.Resolve<ITimerManager>();
            timerManager.AddTimer(new Timer(delay, false, () =>
            {
                if (preExplosionSound != null)
                {
                    var mapSystem = sysMan.GetEntitySystem<SharedMapSystem>();
                    var mapEntity = mapSystem.GetMapOrInvalid(targetPosition.MapId);
                    var targetCoords = transformSystem.ToCoordinates(mapEntity, targetPosition);
                    _audioSystem.PlayPvs(preExplosionSound, targetCoords);
                }

                timerManager.AddTimer(new Timer(500, false, () =>
                {
                    Logger.InfoS("mortar", "=== TIMER FIRED ===");
                    Logger.InfoS("mortar", $"Target position: {targetPosition}");

                    var distanceFired = (targetPosition.Position - mortarPosition.Position).Length();
                    var accuracy = Math.Max(0f, mortarComp.BaseAccuracy - (distanceFired * mortarComp.AccuracyDegradation));
                    var spread = mortarComp.MaxSpread * (1f - accuracy);
                    if (spread > 0f)
                    {
                        var angle = _random.NextFloat() * MathF.PI * 2f;
                        var scatterDist = _random.NextFloat() * spread;
                        targetPosition = new MapCoordinates(
                            targetPosition.Position + new Vector2(
                                MathF.Cos(angle) * scatterDist,
                                MathF.Sin(angle) * scatterDist),
                            targetPosition.MapId);
                    }

                    targetPosition = GetHighestTileTarget(targetPosition, mortarUid);

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

                    if (useDirectExplosion)
                    {
                        sysMan.GetEntitySystem<ExplosionSystem>().QueueExplosion(targetPosition, explosionType, totalIntensity, slope, maxTileIntensity, null);
                    }
                    else if (!string.IsNullOrEmpty(explosionEntity))
                    {
                        Logger.InfoS("mortar", $"Using ExplosionEntity: {explosionEntity}");
                        entMan.SpawnEntity(explosionEntity, targetPosition);
                    }
                    else
                    {
                        Logger.WarningS("mortar", "Shell has neither UseDirectExplosion nor ExplosionEntity!");
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

        private bool HasCeilingAbove(EntityUid uid)
        {
            var xform = Transform(uid);
            if (xform.MapUid is not { } mapUid)
                return false;

            if (!TryComp<CEZMapComponent>(mapUid, out var zMap))
                return false;

            var sysMan = IoCManager.Resolve<IEntitySystemManager>();
            var zLevels = sysMan.GetEntitySystem<CESharedZLevelsSystem>();
            var worldPos = _transform.GetWorldPosition(uid);

            Entity<CEZMapComponent> currentMap = (mapUid, zMap);
            while (zLevels.TryMapUp((currentMap.Owner, (CEZMapComponent?)currentMap.Comp), out var mapAbove))
            {
                if (TileExistsAt(mapAbove, worldPos))
                    return true;
                currentMap = mapAbove;
            }

            return false;
        }

        private MapCoordinates GetHighestTileTarget(MapCoordinates targetPos, EntityUid mortarUid)
        {
            var xform = Transform(mortarUid);
            if (xform.MapUid is not { } mortarMapUid)
                return targetPos;

            if (!TryComp<CEZMapComponent>(mortarMapUid, out var zMap))
                return targetPos;

            if (!TryComp<CEZMapNetworkComponent>(zMap.NetworkUid, out var network))
                return targetPos;

            for (var i = network.SortedZLevels.Count - 1; i >= 0; i--)
            {
                var levelUid = network.SortedZLevels[i];
                if (!levelUid.IsValid())
                    continue;

                if (!TryComp<CEZMapComponent>(levelUid, out _))
                    continue;

                if (!TryComp<MapComponent>(levelUid, out var mapComp))
                    continue;

                if (TileExistsAt(levelUid, targetPos.Position))
                    return new MapCoordinates(targetPos.Position, mapComp.MapId);
            }

            return targetPos;
        }

        private bool TileExistsAt(EntityUid mapUid, Vector2 worldPos)
        {
            if (!_mapManager.TryFindGridAt(mapUid, worldPos, out var gridUid, out var grid))
                return false;

            var sysMan = IoCManager.Resolve<IEntitySystemManager>();
            var mapSystem = sysMan.GetEntitySystem<SharedMapSystem>();
            return mapSystem.TryGetTileRef(gridUid, grid, worldPos, out var tileRef) && !tileRef.Tile.IsEmpty;
        }

    }
}

