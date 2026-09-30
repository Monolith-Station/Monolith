/*
 * New Frontiers - This file is licensed under AGPLv3
 * Copyright (c) 2024 New Frontiers Contributors
 * See AGPLv3.txt for details.
 */
using Content.Server._Mono.MonoCoins;
using Content.Server.Popups;
using Content.Server.Stack;
using Content.Shared._NF.Bank.BUI;
using Content.Shared._NF.Bank.Components;
using Content.Shared._NF.Bank.Events;
using Content.Shared.Coordinates;
using Content.Shared.Stacks;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Content.Server.Administration.Logs;
using Content.Shared._Mono.CCVar; // Mono
using Content.Shared.Database;
using Robust.Shared.Audio.Systems;
using Content.Shared._NF.Bank.BUI;
using Robust.Shared.Configuration; // Mono

namespace Content.Server._NF.Bank;

public sealed partial class BankSystem
{
    [Dependency] private IConfigurationManager _cfg = default!; // Mono
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private StackSystem _stackSystem = default!;
    [Dependency] private UserInterfaceSystem _uiSystem = default!;
    [Dependency] private SharedContainerSystem _containerSystem = default!;
    [Dependency] private IAdminLogManager _adminLogger = default!;

    private void InitializeATM()
    {
        SubscribeLocalEvent<BankATMComponent, BankWithdrawMessage>(OnWithdraw);
        SubscribeLocalEvent<BankATMComponent, BankDepositMessage>(OnDeposit);
        SubscribeLocalEvent<BankATMComponent, BoundUIOpenedEvent>(OnATMUIOpen);
        SubscribeLocalEvent<BankATMComponent, EntInsertedIntoContainerMessage>(OnCashSlotChanged);
        SubscribeLocalEvent<BankATMComponent, EntRemovedFromContainerMessage>(OnCashSlotChanged);
    }

    private void OnWithdraw(EntityUid uid, BankATMComponent component, BankWithdrawMessage args)
    {
        if (args.Actor is not { Valid : true } player)
            return;

        // to keep the window stateful
        GetInsertedCashAmount(component, out var deposit);

        var state = new BankATMMenuInterfaceState(0, 0, false, deposit);

        // check for a bank account
        if (!TryComp<BankAccountComponent>(player, out var bank))
        {
            _log.Info($"{player} has no bank account");
            ConsolePopup(player, Loc.GetString("bank-atm-menu-no-bank"));
            PlayDenySound(uid, component);
            _uiSystem.SetUiState(uid, args.UiKey, state);
            return;
        }

        state.Balance = bank.Balance;
        state.Savings = GetEntSavings(player);

        // check for sufficient funds
        if (bank.Balance < args.Amount)
        {
            ConsolePopup(args.Actor, Loc.GetString("bank-insufficient-funds"));
            PlayDenySound(uid, component);
            _uiSystem.SetUiState(uid, args.UiKey, state);
            return;
        }

        state.Enabled = true;

        // try to actually withdraw from the bank. Validation happens on the banking system but we still indicate error.
        if (!TryBankWithdraw(player, args.Amount))
        {
            ConsolePopup(args.Actor, Loc.GetString("bank-atm-menu-transaction-denied"));
            PlayDenySound(uid, component);
            _uiSystem.SetUiState(uid, args.UiKey, state);
            return;
        }

        state.Balance = bank.Balance;

        ConsolePopup(args.Actor, Loc.GetString("bank-atm-menu-withdraw-successful"));
        PlayConfirmSound(uid, component);
        _adminLogger.Add(LogType.ATMUsage, LogImpact.Low, $"{ToPrettyString(player):actor} withdrew {args.Amount} from {ToPrettyString(component.Owner)}");

        //spawn the cash stack of whatever cash type the ATM is configured to.
        var stackPrototype = _prototypeManager.Index<StackPrototype>(component.CashType);
        _stackSystem.Spawn(args.Amount, stackPrototype, uid.ToCoordinates());

        _uiSystem.SetUiState(uid, args.UiKey, state);
    }

    private void OnDeposit(EntityUid uid, BankATMComponent component, BankDepositMessage args)
    {
        if (args.Actor is not { Valid: true } player)
            return;

        // gets the money inside a cashslot of an ATM.
        // Dynamically knows what kind of cash to look for according to BankATMComponent
        GetInsertedCashAmount(component, out var deposit);

        var state = new BankATMMenuInterfaceState(0, 0, false, deposit);

        // make sure the user actually has a bank
        if (!TryComp<BankAccountComponent>(player, out var bank))
        {
            _log.Info($"{player} has no bank account");
            ConsolePopup(args.Actor, Loc.GetString("bank-atm-menu-no-bank"));
            PlayDenySound(uid, component);
            _uiSystem.SetUiState(uid, args.UiKey, state);
            return;
        }

        state.Balance = bank.Balance;
        if (_playerManager.TryGetSessionByEntity(player, out var session))
            state.Savings = _coins.GetMonoCoinsBalance(session.UserId) ?? 0;

        // validating the cash slot was setup correctly in the yaml
        if (component.CashSlot.ContainerSlot is not BaseContainer cashSlot)
        {
            _log.Info($"ATM has no cash slot");
            ConsolePopup(args.Actor, Loc.GetString("bank-atm-menu-no-bank"));
            PlayDenySound(uid, component);
            _uiSystem.SetUiState(uid, args.UiKey, state);
            return;
        }

        // validate stack prototypes
        if (!TryComp<StackComponent>(component.CashSlot.ContainerSlot.ContainedEntity, out var stackComponent) ||
            stackComponent.StackTypeId == null)
        {
            _log.Info($"ATM cash slot contains bad stack prototype");
            ConsolePopup(args.Actor, Loc.GetString("bank-atm-menu-wrong-cash"));
            PlayDenySound(uid, component);
            _uiSystem.SetUiState(uid, args.UiKey, state);
            return;
        }

        // and then check them against the ATM's CashType
        if (_prototypeManager.Index<StackPrototype>(component.CashType) != _prototypeManager.Index<StackPrototype>(stackComponent.StackTypeId))
        {
            _log.Info($"{stackComponent.StackTypeId} is not {component.CashType}");
            ConsolePopup(args.Actor, Loc.GetString("bank-atm-menu-wrong-cash"));
            PlayDenySound(uid, component);
            _uiSystem.SetUiState(uid, args.UiKey, state);
            return;
        }

        var originalDeposit = deposit;
        foreach (var (account, taxCoeff) in component.TaxAccounts)
        {
            if (!float.IsFinite(taxCoeff) || taxCoeff <= 0.0f)
                continue;
            var tax = (int)Math.Floor(originalDeposit * taxCoeff);
            TrySectorDeposit(account, tax, LedgerEntryType.AtmTax); // Mono BlackMarketAtmTax->AtmTax
            deposit -= tax; // Charge the user whether or not the deposit went through.
        }

        state.Enabled = true;

        // try to deposit the inserted cash into a player's bank acount. Validation happens on the banking system but we still indicate error.
        if (!TryBankDeposit(player, deposit))
        {
            ConsolePopup(args.Actor, Loc.GetString("bank-atm-menu-transaction-denied"));
            PlayDenySound(uid, component);
            _uiSystem.SetUiState(uid, args.UiKey, state);
            return;
        }

        ConsolePopup(args.Actor, Loc.GetString("bank-atm-menu-deposit-successful"));
        PlayConfirmSound(uid, component);
        _adminLogger.Add(LogType.ATMUsage, LogImpact.Low, $"{ToPrettyString(player):actor} deposited {deposit} into {ToPrettyString(component.Owner)}");

        state.Deposit = 0;
        state.Balance = bank.Balance;
        if (session != null)
            state.Savings = _coins.GetMonoCoinsBalance(session.UserId) ?? 0;

        // yeet and delete the stack in the cash slot after success
        _containerSystem.CleanContainer(cashSlot);
        _uiSystem.SetUiState(uid, args.UiKey, state);
        return;
    }

    private void OnCashSlotChanged(EntityUid uid, BankATMComponent component, ContainerModifiedMessage args)
    {
        if (!TryComp<ActivatableUIComponent>(uid, out var uiComp) || uiComp.Key is null)
            return;

        var uiUsers = _uiSystem.GetActors(uid, uiComp.Key);
        GetInsertedCashAmount(component, out var deposit);

        foreach (var user in uiUsers)
        {
            if (user is not { Valid: true } player)
                continue;

            if (!TryComp<BankAccountComponent>(player, out var bank))
                continue;

            var state = new BankATMMenuInterfaceState(bank.Balance, 0, true, deposit);
            state.Savings = GetEntSavings(player);

            if (component.CashSlot.ContainerSlot?.ContainedEntity is not { Valid : true } cash)
                state.Deposit = 0;
            else
                state.Deposit = deposit;

            _uiSystem.SetUiState(uid, uiComp.Key, state);
        }
    }

    private void OnATMUIOpen(EntityUid uid, BankATMComponent component, BoundUIOpenedEvent args)
    {
        var player = args.Actor;

        if (player == null)
            return;

        GetInsertedCashAmount(component, out var deposit);

        var state = new BankATMMenuInterfaceState(0, 0, false, deposit);

        if (!TryComp<BankAccountComponent>(player, out var bank))
        {
            _log.Info($"{player} has no bank account");
            _uiSystem.SetUiState(uid, args.UiKey, state);
            return;
        }

        state.Balance = bank.Balance;
        state.Savings = GetEntSavings(player);

        state.Enabled = true;
        _uiSystem.SetUiState(uid, args.UiKey, state);
    }

    private void GetInsertedCashAmount(BankATMComponent component, out int amount)
    {
        amount = 0;
        var cashEntity = component.CashSlot.ContainerSlot?.ContainedEntity;
        // Nothing inserted: amount should be 0.
        if (cashEntity is null)
            return;

        // Invalid item inserted (doubloons, FMC, telecrystals...): amount should be negative (to denote an error)
        if (!TryComp<StackComponent>(cashEntity, out var cashStack) ||
            cashStack.StackTypeId != component.CashType)
        {
            amount = -1;
            return;
        }

        // Valid amount: output the stack's value.
        amount = cashStack.Count;
        return;
    }

    private void PlayDenySound(EntityUid uid, BankATMComponent component)
    {
        _audio.PlayPvs(_audio.GetSound(component.ErrorSound), uid);
    }

    private void PlayConfirmSound(EntityUid uid, BankATMComponent component)
    {
        _audio.PlayPvs(_audio.GetSound(component.ConfirmSound), uid);
    }

    private void ConsolePopup(EntityUid actor, string text)
    {
        if (actor is { Valid: true } player)
            _popup.PopupEntity(text, player);
    }

    private long GetEntSavings(EntityUid uid)
    {
        if (_playerManager.TryGetSessionByEntity(uid, out var session))
            return _coins.GetMonoCoinsBalance(session.UserId) ?? 0;
        return 0;
    }
}
