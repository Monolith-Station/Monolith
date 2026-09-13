using System.IO;
using Content.Server._Mono.MonoCoins;
using Content.Server.Administration.Logs;
using Content.Server.Database;
using Content.Server.GameTicking;
using Content.Server.Hands.Systems;
using Content.Server.Popups;
using Content.Server.Preferences.Managers;
using Content.Server._NF.Bank;
using Content.Shared._NF.Bank.Components;
using Content.Shared._WF.SafetyDepositBox.BUI;
using Content.Shared._WF.SafetyDepositBox.Components;
using Content.Shared._WF.SafetyDepositBox.Events;
using Content.Shared.Database;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Labels.Components;
using Content.Shared.Labels.EntitySystems;
using Content.Shared.Preferences;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.EntitySerialization.Systems;

namespace Content.Server._WF.SafetyDepositBox;

public sealed partial class SafetyDepositBoxSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private BankSystem _bankSystem = default!;
    [Dependency] private UserInterfaceSystem _uiSystem = default!;
    [Dependency] private IAdminLogManager _adminLogger = default!;
    [Dependency] private HandsSystem _hands = default!;
    [Dependency] private TransformSystem _transform = default!;
    [Dependency] private IServerDbManager _dbManager = default!;
    [Dependency] private SharedStorageSystem _storage = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private SharedLabelSystem _label = default!; // Wicce: LabelSystem -> SharedLabelSystem
    [Dependency] private IServerPreferencesManager _prefsManager = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private IComponentFactory _componentFactory = default!;
    [Dependency] private MapLoaderSystem _loader = default!;
    [Dependency] private MonoCoinsManager _coinBase = default!; // I had to.
    [Dependency] private ISharedPlayerManager _playerManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SafetyDepositConsoleComponent, ComponentInit>(OnConsoleInit);
        SubscribeLocalEvent<SafetyDepositConsoleComponent, BoundUIOpenedEvent>(OnUIOpen);
        SubscribeLocalEvent<SafetyDepositConsoleComponent, SafetyDepositPurchaseMessage>(OnPurchase);
        SubscribeLocalEvent<SafetyDepositConsoleComponent, SafetyDepositDepositMessage>(OnDeposit);
        SubscribeLocalEvent<SafetyDepositConsoleComponent, SafetyDepositWithdrawMessage>(OnWithdraw);
        SubscribeLocalEvent<SafetyDepositConsoleComponent, SafetyDepositReclaimMessage>(OnReclaim);
        SubscribeLocalEvent<SafetyDepositConsoleComponent, EntInsertedIntoContainerMessage>(OnSlotChanged);
        SubscribeLocalEvent<SafetyDepositConsoleComponent, EntRemovedFromContainerMessage>(OnSlotChanged);
    }

    private void OnConsoleInit(EntityUid uid, SafetyDepositConsoleComponent component, ComponentInit args)
    {
        _itemSlots.AddItemSlot(uid, SafetyDepositConsoleComponent.BoxSlotId, component.BoxSlot);
    }

    private void OnUIOpen(EntityUid uid, SafetyDepositConsoleComponent component, BoundUIOpenedEvent args)
    {
        if (args.Actor is not { Valid: true } player)
            return;

        UpdateUI(uid, component, player);
    }

    private async void UpdateUI(EntityUid consoleUid, SafetyDepositConsoleComponent component, EntityUid player)
    {
        if (!TryComp<ActorComponent>(player, out var actor))
            return;

        var userId = actor.PlayerSession.UserId;
        if (!_prefsManager.TryGetCachedPreferences(userId, out var prefs))
            return;

        var characterIndex = prefs.SelectedCharacterIndex;

        // Get all boxes owned by this character from database
        var ownedBoxes = await _dbManager.GetPlayerSafetyDepositBoxes(userId.UserId, characterIndex);

        var boxInfoList = new List<SafetyDepositBoxInfo>();
        foreach (var box in ownedBoxes)
        {
            // A box is considered deposited if:
            // - It has never been withdrawn (!LastWithdrawn.HasValue), OR
            // - It was withdrawn in the current round and still has items
            // A box is considered lost if it was withdrawn in a previous round and has no items
            bool isDeposited;
            if (!box.LastWithdrawn.HasValue)
                isDeposited = true;
            else if (box.LastWithdrawnRoundId.HasValue && box.LastWithdrawnRoundId.Value != _gameTicker.RoundId)
            {
                // Withdrawn in a previous round - lost regardless of items
                isDeposited = false;
            }
            else
            {
                // Withdrawn in current round - deposited only if it has items
                isDeposited = box.Items.Count > 0;
            }

            boxInfoList.Add(new (
                box.BoxId,
                box.OwnerName,
                isDeposited,
                box.Nickname,
                box.ProtoId,
                box.LastWithdrawn,
                box.LastWithdrawnRoundId
            ));
        }

        var boxInSlot = component.BoxSlot.Item;
        SafetyDepositBoxInfo? boxInSlotInfo = null;

        if (boxInSlot != null && TryComp<SafetyDepositBoxComponent>(boxInSlot, out var boxComp) && boxComp.BoxId.HasValue)
        {
            // Get label if it exists
            string? nickname = null;
            if (TryComp<LabelComponent>(boxInSlot.Value, out var labelComp))
            {
                nickname = labelComp.CurrentLabel;
            }

            if (!TryPrototype(boxInSlot.Value, out var boxProto))
                return;

            boxInSlotInfo = new SafetyDepositBoxInfo(
                boxComp.BoxId.Value,
                boxComp.OwnerName ?? "Unknown",
                false,
                nickname,
                boxProto.ToString(),
                null,
                null
            );
        }

        var state = new SafetyDepositConsoleState(
            boxInfoList,
            0, // No cash display needed anymore
            boxInSlot != null,
            boxInSlotInfo,
            GetBoxCost(component.SmallBoxProto),
            GetBoxCost(component.MediumBoxProto),
            GetBoxCost(component.LargeBoxProto),
            _gameTicker.RoundId
        );

        _uiSystem.SetUiState(consoleUid, SafetyDepositConsoleUiKey.Key, state);
    }

    private void OnPurchase(EntityUid uid, SafetyDepositConsoleComponent component, SafetyDepositPurchaseMessage args)
    {
        int cost;
        EntityPrototype prototypeId;
        if (args.Actor is not { Valid: true } player)
            return;

        if (!TryComp<ActorComponent>(player, out var actor))
            return;

        if (_prototypeManager.TryIndex(args.BoxProto, out var proto) && proto.TryGetComponent<SafetyDepositBoxComponent>(out var boxComponent, _componentFactory))
        {
            cost = boxComponent.Cost;
            prototypeId = proto;
        }
        else
        {
            ConsolePopup(player, "Error: Invalid box size.");
            PlayDenySound(uid, component);
            return;
        }
        var userId = actor.PlayerSession.UserId;

        // Create the box in the database
        if (!_prefsManager.TryGetCachedPreferences(userId, out var prefs) || !_playerManager.TryGetSessionByEntity(player, out var session) || prefs.SelectedCharacter is not HumanoidCharacterProfile profile)
        {
            ConsolePopup(player, "Error: Could not load character data.");
            PlayDenySound(uid, component);
            return;
        }

        // Check bank account
        if (!TryComp<BankAccountComponent>(player, out var bank))
        {
            ConsolePopup(player, "Error: No bank account found.");
            PlayDenySound(uid, component);
            return;
        }

        long initialBankBalance = bank.Balance;
        initialBankBalance += _coinBase.GetMonoCoinsBalance(userId) ?? 0L;
        var bankBalance = initialBankBalance;
        bankBalance -= cost;

        if (initialBankBalance < cost)
        {
            ConsolePopup(player, $"Insufficient funds. You need ${cost:N0}, but only have ${bank.Balance:N0} in bank and ${initialBankBalance-bank.Balance:N0} in savings.");
            PlayDenySound(uid, component);
            return;
        }

        // Withdraw from bank
        if (!_bankSystem.TryBankWithdraw(session!, prefs!, profile, (int)(initialBankBalance - bankBalance), out var newBalance, true))
        {
            ConsolePopup(player, "Transaction failed.");
            PlayDenySound(uid, component);
            return;
        }

        var characterIndex = prefs.SelectedCharacterIndex;
        var characterName = MetaData(player).EntityName;

        PurchaseBoxAsync(uid, component, player, userId.UserId, characterIndex, characterName, prototypeId, cost);
    }

    // got tired of doing this
    public int GetBoxCost(EntProtoId boxProto)
    {
        if (_prototypeManager.TryIndex(boxProto, out var proto) &&
            proto.TryGetComponent<SafetyDepositBoxComponent>(out var boxComponent, _componentFactory))
            return boxComponent.Cost;
        else
            return 0;
    }

    private async void PurchaseBoxAsync(
        EntityUid consoleUid,
        SafetyDepositConsoleComponent component,
        EntityUid player,
        Guid userId,
        int characterIndex,
        string characterName,
        EntityPrototype prototypeId,
        int cost)
    {
        // Create box in database
        var box = await _dbManager.PurchaseSafetyDepositBox(userId, characterIndex, characterName, prototypeId.ID);

        // Spawn the physical box
        var boxEntity = Spawn(prototypeId.ID, Transform(player).Coordinates);
        var boxComp = EnsureComp<SafetyDepositBoxComponent>(boxEntity);

        boxComp.BoxId = box.BoxId;
        boxComp.OwnerId = userId;
        boxComp.CharacterIndex = characterIndex;
        boxComp.OwnerName = characterName;
        Dirty(boxEntity, boxComp);

        // Try to put it in player's hands
        if (!_hands.TryPickupAnyHand(player, boxEntity))
        {
            _transform.SetLocalRotation(boxEntity, Angle.Zero);
        }

        // Mark the box as withdrawn so it shows "In World" in the UI
        await _dbManager.ClearSafetyDepositBoxItems(box.BoxId, _gameTicker.RoundId);

        ConsolePopup(player, $"Safety deposit box purchased! Box ID: {box.BoxId.ToString()[..8]}...");
        PlayConfirmSound(consoleUid, component);

        _adminLogger.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(player):actor} purchased safety deposit box {box.BoxId} for {cost} credits");

        UpdateUI(consoleUid, component, player);
    }

    private void OnDeposit(EntityUid uid, SafetyDepositConsoleComponent component, SafetyDepositDepositMessage args)
    {
        if (args.Actor is not { Valid: true } player)
            return;

        if (!TryComp<ActorComponent>(player, out var actor))
            return;

        // Check if there's a box in the slot
        var boxEntity = component.BoxSlot.Item;
        if (boxEntity == null)
        {
            ConsolePopup(player, "Please insert a safety deposit box.");
            PlayDenySound(uid, component);
            return;
        }

        if (!TryComp<SafetyDepositBoxComponent>(boxEntity.Value, out var boxComp) || !boxComp.BoxId.HasValue)
        {
            ConsolePopup(player, "Invalid safety deposit box.");
            PlayDenySound(uid, component);
            return;
        }

        // Verify ownership
        var userId = actor.PlayerSession.UserId;
        if (!_prefsManager.TryGetCachedPreferences(userId, out var prefs))
        {
            ConsolePopup(player, "Error: Could not load character data.");
            PlayDenySound(uid, component);
            return;
        }

        var characterIndex = prefs.SelectedCharacterIndex;
        if (boxComp.OwnerId != userId.UserId || boxComp.CharacterIndex != characterIndex)
        {
            ConsolePopup(player, "This box does not belong to you.");
            PlayDenySound(uid, component);
            return;
        }

        // Serialize the contents
        if (!TryComp<StorageComponent>(boxEntity.Value, out var storageComp))
        {
            ConsolePopup(player, "Error: Box has no storage.");
            PlayDenySound(uid, component);
            return;
        }

        DepositBoxAsync(uid, component, player, boxEntity.Value, boxComp, storageComp);
    }

    private async void DepositBoxAsync(
        EntityUid consoleUid,
        SafetyDepositConsoleComponent component,
        EntityUid player,
        EntityUid boxEntity,
        SafetyDepositBoxComponent boxComp,
        StorageComponent storageComp)
    {
        var entityDataList = new List<string>();

        Log.Info($"DepositBoxAsync: Box has {storageComp.Container.ContainedEntities.Count} items");

        // Serialize each item in the box - store prototype + component data
        foreach (var item in storageComp.Container.ContainedEntities)
        {
            try
            {
                Log.Info($"Serializing item: {ToPrettyString(item)}");
                using var writer = new StringWriter();
                _loader.TrySaveEntity(item, writer);
                entityDataList.Add(writer.ToString());
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to serialize item {ToPrettyString(item)} in safety deposit box: {ex}");
            }
        }

        Log.Info($"Saving {entityDataList.Count} items to database for box {boxComp.BoxId}");

        // Get nickname from label if it exists
        string? nickname = null;
        if (TryComp<LabelComponent>(boxEntity, out var boxLabel) && !string.IsNullOrEmpty(boxLabel.CurrentLabel))
        {
            nickname = boxLabel.CurrentLabel;
            Log.Info($"Saving box nickname: {nickname}");
        }

        // Save to database
        await _dbManager.DepositSafetyDepositBoxItems(boxComp.BoxId!.Value, entityDataList);

        // Update nickname if one was set
        if (nickname != null)
        {
            await _dbManager.UpdateSafetyDepositBoxNickname(boxComp.BoxId!.Value, nickname);
        }

        // Remove from slot before deleting to properly update UI
        _itemSlots.TryEject(consoleUid, component.BoxSlot, null, out _);

        // Delete the physical box
        QueueDel(boxEntity);

        ConsolePopup(player, "Safety deposit box contents saved. The box has been stored.");
        PlayConfirmSound(consoleUid, component);

        _adminLogger.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(player):actor} deposited safety deposit box {boxComp.BoxId} with {storageComp.Container.ContainedEntities.Count} items");

        UpdateUI(consoleUid, component, player);
    }

    private void OnWithdraw(EntityUid uid, SafetyDepositConsoleComponent component, SafetyDepositWithdrawMessage args)
    {
        if (args.Actor is not { Valid: true } player)
            return;

        if (!TryComp<ActorComponent>(player, out var actor))
            return;

        var userId = actor.PlayerSession.UserId;
        if (!_prefsManager.TryGetCachedPreferences(userId, out var prefs))
            return;

        var characterIndex = prefs.SelectedCharacterIndex;

        WithdrawBoxAsync(uid, component, player, userId.UserId, characterIndex, args.BoxId);
    }

    private void OnReclaim(EntityUid uid, SafetyDepositConsoleComponent component, SafetyDepositReclaimMessage args)
    {
        if (args.Actor is not { Valid: true } player)
            return;

        if (!TryComp<ActorComponent>(player, out var actor))
            return;

        var userId = actor.PlayerSession.UserId;
        if (!_prefsManager.TryGetCachedPreferences(userId, out var prefs))
            return;

        var characterIndex = prefs.SelectedCharacterIndex;

        ReclaimBoxAsync(uid, component, player, userId.UserId, characterIndex, args.BoxId);
    }

    private async void ReclaimBoxAsync(
        EntityUid consoleUid,
        SafetyDepositConsoleComponent component,
        EntityUid player,
        Guid userId,
        int characterIndex,
        Guid boxId)
    {
        // Get box from database
        var box = await _dbManager.GetSafetyDepositBox(boxId);

        if (box == null)
        {
            ConsolePopup(player, "Box not found.");
            PlayDenySound(consoleUid, component);
            return;
        }

        // Verify ownership
        if (box.OwnerUserId != userId || box.CharacterIndex != characterIndex)
        {
            ConsolePopup(player, "This box does not belong to you.");
            PlayDenySound(consoleUid, component);
            return;
        }

        // Verify box is actually lost (withdrawn in previous round with no items)
        bool isLost = box.LastWithdrawn.HasValue &&
                      box.LastWithdrawnRoundId.HasValue &&
                      box.LastWithdrawnRoundId.Value != _gameTicker.RoundId &&
                      box.Items.Count == 0;

        if (!isLost)
        {
            ConsolePopup(player, "This box is not lost and cannot be reclaimed.");
            PlayDenySound(consoleUid, component);
            return;
        }

        // Delete the database record
        await _dbManager.DeleteSafetyDepositBox(boxId);

        // Create a new database record for the replacement box
        var newBox = await _dbManager.PurchaseSafetyDepositBox(
            userId,
            characterIndex,
            MetaData(player).EntityName,
            box.ProtoId
        );

        // Spawn a new empty physical box
        string prototypeId = box.ProtoId;

        var boxEntity = Spawn(prototypeId, Transform(player).Coordinates);
        var boxComp = EnsureComp<SafetyDepositBoxComponent>(boxEntity);
        boxComp.BoxId = newBox.BoxId;
        boxComp.OwnerId = userId;
        boxComp.CharacterIndex = characterIndex;
        boxComp.OwnerName = MetaData(player).EntityName;
        Dirty(boxEntity, boxComp);

        // Mark the box as withdrawn in the current round (since we're giving them a physical box)
        await _dbManager.ClearSafetyDepositBoxItems(newBox.BoxId, _gameTicker.RoundId);

        // Restore nickname if one was saved
        if (!string.IsNullOrEmpty(box.Nickname))
        {
            _label.Label(boxEntity, box.Nickname);
        }

        // Try to put it in player's hands
        if (!_hands.TryPickupAnyHand(player, boxEntity))
        {
            _transform.SetLocalRotation(boxEntity, Angle.Zero);
        }

        ConsolePopup(player, "Lost box reclaimed! A new empty box has been issued.");
        PlayConfirmSound(consoleUid, component);

        _adminLogger.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(player):actor} reclaimed lost safety deposit box {boxId}");

        UpdateUI(consoleUid, component, player);
    }

    private async void WithdrawBoxAsync(
        EntityUid consoleUid,
        SafetyDepositConsoleComponent component,
        EntityUid player,
        Guid userId,
        int characterIndex,
        Guid boxId)
    {
        // Get box from database
        var box = await _dbManager.GetSafetyDepositBox(boxId);

        if (box == null)
        {
            ConsolePopup(player, "Box not found.");
            PlayDenySound(consoleUid, component);
            return;
        }

        if (box.LastWithdrawn != null) // Check to make sure it isn't already deposited.
        {
            ConsolePopup(player, "Box already withdrawn in world.");
            PlayDenySound(consoleUid, component);
            return;
        }

        Log.Info($"WithdrawBoxAsync: Retrieved box {boxId} with {box.Items.Count} items from database");

        // Verify ownership
        if (box.OwnerUserId != userId || box.CharacterIndex != characterIndex)
        {
            ConsolePopup(player, "This box does not belong to you.");
            PlayDenySound(consoleUid, component);
            return;
        }

        // Spawn the physical box (use stored box size to determine prototype);

        var boxEntity = Spawn(box.ProtoId, Transform(player).Coordinates);
        var boxComp = EnsureComp<SafetyDepositBoxComponent>(boxEntity);
        boxComp.BoxId = box.BoxId;
        boxComp.OwnerId = userId;
        boxComp.CharacterIndex = characterIndex;
        // Use current character name instead of stored name in case they changed it
        boxComp.OwnerName = MetaData(player).EntityName;
        Dirty(boxEntity, boxComp);

        // Restore nickname if one was saved
        if (!string.IsNullOrEmpty(box.Nickname))
        {
            _label.Label(boxEntity, box.Nickname);
            Log.Info($"Restored box nickname: {box.Nickname}");
        }

        // Deserialize and spawn items into the box
        if (TryComp<StorageComponent>(boxEntity, out var storageComp))
        {
            foreach (var itemData in box.Items)
            {
                try
                {
                    using var reader = new StringReader(itemData.EntityData);
                    if (!_loader.TryLoadEntity(reader, "safety deposit box", out var entity))
                        return;

                    var itemEntity = entity.Value.Owner;
                    // Mark item as having been stored in a deposit box
                    EnsureComp<SafetyDepositStoredComponent>(itemEntity);

                    // Insert into storage
                    if (!_storage.Insert(boxEntity, itemEntity, out _, storageComp: storageComp, playSound: false))
                        QueueDel(itemEntity);

                }
                catch (Exception ex)
                {
                    Log.Error($"Failed to deserialize item from safety deposit box {boxId}: {ex}");
                }
            }
        }
        else
        {
            Log.Error($"Box entity {boxEntity} has no StorageComponent!");
        }

        // Clear items from database
        await _dbManager.ClearSafetyDepositBoxItems(boxId, _gameTicker.RoundId);

        // Try to put it in player's hands or place it near them
        if (!_hands.TryPickupAnyHand(player, boxEntity))
        {
            _transform.SetLocalRotation(boxEntity, Angle.Zero);
        }

        ConsolePopup(player, "Safety deposit box retrieved.");
        PlayConfirmSound(consoleUid, component);

        _adminLogger.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(player):actor} withdrew safety deposit box {boxId} with {box.Items.Count} items");

        UpdateUI(consoleUid, component, player);
    }

    private void OnSlotChanged(EntityUid uid, SafetyDepositConsoleComponent component, ContainerModifiedMessage args)
    {
        // Update UI for anyone who has this console's UI open
        foreach (var actor in _uiSystem.GetActors(uid, SafetyDepositConsoleUiKey.Key))
        {
            UpdateUI(uid, component, actor);
        }
    }

    private void PlayDenySound(EntityUid uid, SafetyDepositConsoleComponent component)
    {
        _audio.PlayPvs(component.ErrorSound, uid);
    }

    private void PlayConfirmSound(EntityUid uid, SafetyDepositConsoleComponent component)
    {
        _audio.PlayPvs(component.ConfirmSound, uid);
    }

    private void ConsolePopup(EntityUid actor, string text)
    {
        _popup.PopupEntity(text, actor, actor);
    }
}
