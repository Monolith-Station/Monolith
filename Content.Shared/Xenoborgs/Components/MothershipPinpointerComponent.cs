namespace Content.Shared.Xenoborgs.Components;

/// <summary>
/// Defines what is a MothershipPinpointerPiece for the intentions of the xenoborgsystem. if a mothership core is destroyed, this will identify pinpointer pieces and remove them ensuring players must collect 4 more pieces to rebuild another pinpointer.
/// </summary>
[RegisterComponent]
public sealed partial class MothershipPinpointerPieceComponent : Component;