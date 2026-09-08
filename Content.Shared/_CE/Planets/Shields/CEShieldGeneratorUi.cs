using Robust.Shared.Serialization;

namespace Content.Shared._CE.Planets.Shields;

[Serializable, NetSerializable]
public enum CEShieldGeneratorUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class CEShieldGeneratorBuiState : BoundUserInterfaceState
{
    public CEShieldGeneratorStage Stage;
    public float Charge;
    public float MaxCharge;
    public float ReceivedPower;
    public float WantedPower;
    public float EtaSeconds = -1f;
    public bool OnPlanet = true;
    public bool ObjectTooLarge;
}
