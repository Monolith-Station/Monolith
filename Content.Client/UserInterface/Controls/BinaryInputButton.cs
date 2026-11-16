
namespace Content.Client.UserInterface.Controls;

public class BinaryButton : BaseButton
{
    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {

    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {

    }

        /// <summary>
        ///     Fired when the button is pressed down on by UIRightClick.
        /// </summary>
        public event Action<ButtonEventArgs>? OnButtonDownAlternate;

        /// <summary>
        ///     Fired when the button is pressed down on by UIRightClick.
        /// </summary>
        public event Action<ButtonEventArgs>? OnButtonUpAlternate;

        /// <summary>
        /// Fired when the button is "pressed" by UIRightClick (instead of UIClick). When this happens depends on <see cref="Mode"/>.
        /// </summary>
        public event Action<ButtonEventArgs>? OnPressedAlternate;


}
