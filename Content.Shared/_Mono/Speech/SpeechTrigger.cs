namespace Content.Shared._Mono.Speech;

/// <summary>
/// Describes an event that can cause an entity to speak.
/// </summary>
public enum SpeechTrigger
{
    Eating,
    PickedUp,
    Thrown,
    Drinking,
    Critical,
	Dead,
	Revived,
	Examined,
}