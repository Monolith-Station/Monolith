namespace Content.Server.Explosion.Components
{
    /// <summary>
    /// calls the trigger when the object is initialized
    /// </summary>
    [RegisterComponent]
    public sealed partial class TriggerOnSpawnComponent : Component
    {
        /// <summary>
        /// Starts a timer on spawn rather than instantly triggering
        /// </summary>
        [DataField("timerOnly")] // Mono: Added "timerOnly" boolean
        public bool timerOnly = false;
    }
}
