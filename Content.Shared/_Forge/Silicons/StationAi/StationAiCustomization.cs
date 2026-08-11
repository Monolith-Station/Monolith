using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._Forge.Silicons.StationAi;

[Prototype]
public sealed partial class StationAiScreenPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = string.Empty;

    [DataField(required: true)]
    public LocId Name;

    [DataField(required: true)]
    public ResPath Sprite = default!;

    [DataField(required: true)]
    public string State = string.Empty;
}

/// <summary>
/// Replicates the selected screen and occupancy state to the physical AI core.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class StationAiScreenComponent : Component
{
    [DataField, AutoNetworkedField]
    public ProtoId<StationAiScreenPrototype> DefaultScreen = "StationAiScreenDefault";

    [DataField, AutoNetworkedField]
    public ProtoId<StationAiScreenPrototype> Screen = "StationAiScreenDefault";

    [DataField, AutoNetworkedField]
    public ResPath EmptySprite = new("Mobs/Silicon/station_ai.rsi");

    [DataField, AutoNetworkedField]
    public string EmptyState = "ai_empty";

    [DataField, AutoNetworkedField]
    public StationAiScreenLayer ScreenLayer = StationAiScreenLayer.Screen;

    [DataField, AutoNetworkedField]
    public bool Occupied;

    [DataField, AutoNetworkedField]
    public Color Color = Color.White;

    [DataField("force-name-prefix", readOnly: true)]
    public string ForceNamePrefix = string.Empty;

    [ViewVariables]
    public string? OriginalName;
}

[Serializable, NetSerializable]
public enum StationAiScreenLayer : byte
{
    Background,
    Screen,
    Frame,
}

[Serializable, NetSerializable]
public enum StationAiCustomizationUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class StationAiCustomizationState(
    string name,
    string forceNamePrefix,
    ProtoId<StationAiScreenPrototype> screen,
    Color color,
    int cooldownSeconds) : BoundUserInterfaceState
{
    public readonly string Name = name;
    public readonly string ForceNamePrefix = forceNamePrefix;
    public readonly ProtoId<StationAiScreenPrototype> Screen = screen;
    public readonly Color Color = color;
    public readonly int CooldownSeconds = cooldownSeconds;
}

[Serializable, NetSerializable]
public sealed class StationAiCustomizationApplyMessage(
    string name,
    ProtoId<StationAiScreenPrototype> screen,
    Color color) : BoundUserInterfaceMessage
{
    public readonly string Name = name;
    public readonly ProtoId<StationAiScreenPrototype> Screen = screen;
    public readonly Color Color = color;
}

public sealed partial class OpenStationAiCustomizationEvent : InstantActionEvent;
