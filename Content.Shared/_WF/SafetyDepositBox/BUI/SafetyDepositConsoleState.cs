using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._WF.SafetyDepositBox.BUI;

/// <summary>
/// State of the safety deposit console UI
/// </summary>
[Serializable, NetSerializable]
public sealed class SafetyDepositConsoleState : BoundUserInterfaceState
{
    /// <summary>
    /// List of boxes owned by the current user.
    /// </summary>
    public List<SafetyDepositBoxInfo> OwnedBoxes = new();

    /// <summary>
    /// Amount of cash currently inserted in the console.
    /// </summary>
    public int InsertedCash;

    /// <summary>
    /// Is there a box currently in the box slot?
    /// </summary>
    public bool HasBoxInSlot;

    /// <summary>
    /// Info about the box in the slot, if any.
    /// </summary>
    public SafetyDepositBoxInfo? BoxInSlot;

    /// <summary>
    /// Purchase cost for a small box.
    /// </summary>
    public int SmallBoxCost;

    /// <summary>
    /// Purchase cost for a medium box.
    /// </summary>
    public int MediumBoxCost;

    /// <summary>
    /// Purchase cost for a large box.
    /// </summary>
    public int LargeBoxCost;

    /// <summary>
    /// The current round ID, used to determine if boxes are lost.
    /// </summary>
    public int CurrentRoundId;

    public SafetyDepositConsoleState(
        List<SafetyDepositBoxInfo> ownedBoxes,
        int insertedCash,
        bool hasBoxInSlot,
        SafetyDepositBoxInfo? boxInSlot,
        int smallBoxCost,
        int mediumBoxCost,
        int largeBoxCost,
        int currentRoundId)
    {
        OwnedBoxes = ownedBoxes;
        InsertedCash = insertedCash;
        HasBoxInSlot = hasBoxInSlot;
        BoxInSlot = boxInSlot;
        SmallBoxCost = smallBoxCost;
        MediumBoxCost = mediumBoxCost;
        LargeBoxCost = largeBoxCost;
        CurrentRoundId = currentRoundId;
    }
}

/// <summary>
/// Information about a safety deposit box.
/// </summary>
[Serializable, NetSerializable]
public record struct SafetyDepositBoxInfo(
    Guid BoxId,
    string OwnerName,
    bool IsDeposited,
    string? Nickname,
    string ProtoId,
    DateTime? LastWithdrawn,
    int? LastWithdrawnRoundId
);
