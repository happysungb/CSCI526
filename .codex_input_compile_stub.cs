namespace UnityEngine.InputSystem
{
    public sealed class Mouse
    {
        public static Mouse current { get; }
        public ButtonControl leftButton { get; }
        public Vector2Control position { get; }
    }

    public sealed class ButtonControl
    {
        public bool wasPressedThisFrame { get; }
    }

    public sealed class Vector2Control
    {
        public UnityEngine.Vector2 ReadValue()
        {
            return default;
        }
    }
}
