/*
 * New Frontiers - This file is licensed under AGPLv3
 * Copyright (c) 2024 New Frontiers Contributors
 * See AGPLv3.txt for details.
 */
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
using Content.Shared.Database;
using Robust.Shared.Audio.Systems;
using Content.Shared._NF.Bank.BUI;

namespace Content.Server._NF.Bank;

public sealed partial class BankSystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly StackSystem _stackSystem = default!;
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly SharedContainerSystem _containerSystem = default!;
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;

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

        TryGetBalance(player, out var realBalance, true);
        // check for a bank account
        if (!TryComp<BankAccountComponent>(player, out var bank))
        {
            _log.Info($"{player} has no bank account");
            ConsolePopup(player, Loc.GetString("bank-atm-menu-no-bank"));
            PlayDenySound(uid, component);
            _uiSystem.SetUiState(uid, args.UiKey,
                new BankATMMenuInterfaceState(realBalance, false, deposit));
            return;
        }

        // check for sufficient funds
        if (bank.Balance < args.Amount)
        {
            ConsolePopup(args.Actor, Loc.GetString("bank-insufficient-funds"));
            PlayDenySound(uid, component);
            _uiSystem.SetUiState(uid, args.UiKey,
                new BankATMMenuInterfaceState(realBalance, true, deposit));
            return;
        }

        // try to actually withdraw from the bank. Validation happens on the banking system but we still indicate error.
        if (!TryBankWithdraw(player, args.Amount))
        {
            ConsolePopup(args.Actor, Loc.GetString("bank-atm-menu-transaction-denied"));
            PlayDenySound(uid, component);
            _uiSystem.SetUiState(uid, args.UiKey,
                new BankATMMenuInterfaceState(bank.Balance, true, deposit));
            return;
        }

        ConsolePopup(args.Actor, Loc.GetString("bank-atm-menu-withdraw-successful"));
        PlayConfirmSound(uid, component);
        _adminLogger.Add(LogType.ATMUsage, LogImpact.Low, $"{ToPrettyString(player):actor} withdrew {args.Amount} from {ToPrettyString(component.Owner)}");

        //spawn the cash stack of whatever cash type the ATM is configured to.
        var stackPrototype = _prototypeManager.Index<StackPrototype>(component.PrimaryCashType);
        _stackSystem.Spawn(args.Amount, stackPrototype, uid.ToCoordinates());

        _uiSystem.SetUiState(uid, args.UiKey,
            new BankATMMenuInterfaceState(bank.Balance, true, deposit));
    }

    private void OnDeposit(EntityUid uid, BankATMComponent component, BankDepositMessage args)
    {
        if (args.Actor is not { Valid: true } player)
            return;

        // gets the money inside a cashslot of an ATM.
        // Dynamically knows what kind of cash to look for according to BankATMComponent
        GetInsertedCashAmount(component, out var deposit);

        TryGetBalance(player, out var realBalance, true);
        // make sure the user actually has a bank
        if (!TryComp<BankAccountComponent>(player, out var bank))
        {
            _log.Info($"{player} has no bank account");
            ConsolePopup(args.Actor, Loc.GetString("bank-atm-menu-no-bank"));
            PlayDenySound(uid, component);
            _uiSystem.SetUiState(uid, args.UiKey,
                new BankATMMenuInterfaceState(realBalance, false, deposit));
            return;
        }

        // validating the cash slot was setup correctly in the yaml
        if (component.CashSlot.ContainerSlot is not BaseContainer cashSlot)
        {
            _log.Info($"ATM has no cash slot");
            ConsolePopup(args.Actor, Loc.GetString("bank-atm-menu-no-bank"));
            PlayDenySound(uid, component);
            _uiSystem.SetUiState(uid, args.UiKey,
                new BankATMMenuInterfaceState(realBalance, false, deposit));
            return;
        }

        // validate stack prototypes
        if (!TryComp<StackComponent>(component.CashSlot.ContainerSlot.ContainedEntity, out var stackComponent) ||
            stackComponent.StackTypeId == null)
        {
            _log.Info($"ATM cash slot contains bad stack prototype");
            ConsolePopup(args.Actor, Loc.GetString("bank-atm-menu-wrong-cash"));
            PlayDenySound(uid, component);
            _uiSystem.SetUiState(uid, args.UiKey,
                new BankATMMenuInterfaceState(realBalance, false, deposit));
            return;
        }

        // and then check them against the ATM's CashType
        if (!component.CashTypes.Contains(stackComponent.StackTypeId))
        {
            _log.Info($"{stackComponent.StackTypeId} is not in allowed types");
            ConsolePopup(args.Actor, Loc.GetString("bank-atm-menu-wrong-cash"));
            PlayDenySound(uid, component);
            _uiSystem.SetUiState(uid, args.UiKey,
                new BankATMMenuInterfaceState(realBalance, false, deposit));
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
        deposit = int.Max(0, deposit);

        // try to deposit the inserted cash into a player's bank acount. Validation happens on the banking system but we still indicate error.
        if (!TryBankDeposit(player, deposit, stackProto: stackComponent.StackTypeId))
        {
            ConsolePopup(args.Actor, Loc.GetString("bank-atm-menu-transaction-denied"));
            PlayDenySound(uid, component);
            _uiSystem.SetUiState(uid, args.UiKey,
                new BankATMMenuInterfaceState(realBalance, true, deposit));
            return;
        }

        ConsolePopup(args.Actor, Loc.GetString("bank-atm-menu-deposit-successful"));
        PlayConfirmSound(uid, component);
        _adminLogger.Add(LogType.ATMUsage, LogImpact.Low, $"{ToPrettyString(player):actor} deposited {deposit} into {ToPrettyString(component.Owner)}");

        // yeet and delete the stack in the cash slot after success
        _containerSystem.CleanContainer(cashSlot);
        _uiSystem.SetUiState(uid, args.UiKey,
            new BankATMMenuInterfaceState(realBalance + deposit, true, 0));
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

            // get our real balance even if ironman
            if (!TryGetBalance(player, out var balance, true))
                continue;

            BankATMMenuInterfaceState newState;
            if (component.CashSlot.ContainerSlot?.ContainedEntity is not { Valid : true } cash)
                newState = new BankATMMenuInterfaceState(balance, true, 0);
            else
                newState = new BankATMMenuInterfaceState(balance, true, deposit);

            _uiSystem.SetUiState(uid, uiComp.Key, newState);
        }
    }

    private void OnATMUIOpen(EntityUid uid, BankATMComponent component, BoundUIOpenedEvent args)
    {
        var player = args.Actor;

        if (player == null)
            return;

        GetInsertedCashAmount(component, out var deposit);

        // force get balance
        if (!TryGetBalance(player, out var balance, true))
        {
            _log.Info($"{player} has no bank account");
            _uiSystem.SetUiState(uid, args.UiKey,
                new BankATMMenuInterfaceState(0, false, deposit));
            return;
        }

        _uiSystem.SetUiState(uid, args.UiKey,
            new BankATMMenuInterfaceState(balance, true, deposit));
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
            !component.CashTypes.Contains(cashStack.StackTypeId))
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
}
