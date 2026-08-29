namespace UnityEngine
{
    public enum KeyCode
    {
        None, Space, Escape, Return, Tab, Backspace, Delete,
        A, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T, U, V, W, X, Y, Z,
        Alpha0, Alpha1, Alpha2, Alpha3, Alpha4, Alpha5, Alpha6, Alpha7, Alpha8, Alpha9,
        LeftShift, RightShift, LeftControl, RightControl, LeftAlt, RightAlt,
        UpArrow, DownArrow, LeftArrow, RightArrow,
        JoystickButton0, JoystickButton1, JoystickButton2, JoystickButton3,
        JoystickButton4, JoystickButton5, JoystickButton6, JoystickButton7,
        JoystickButton8, JoystickButton9, JoystickButton10, JoystickButton11,
        F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12
    }

    public enum TouchPhase { Began, Moved, Stationary, Ended, Canceled }

    public struct Touch
    {
        public int fingerId;
        public Vector2 position;
        public Vector2 deltaPosition;
        public TouchPhase phase;
    }

    public static class Input
    {
        public static bool touchSupported { get; set; }
        public static bool multiTouchEnabled { get; set; }
        public static int touchCount => 0;
        public static bool simulateMouseWithTouches { get; set; }
        public static Touch[] touches => new Touch[0];
        public static Touch GetTouch(int index) => default;
        public static Vector3 mousePosition => default;
        public static Vector2 mouseScrollDelta => default;
        public static float GetAxis(string name) => 0f;
        public static float GetAxisRaw(string name) => 0f;
        public static bool GetButton(string name) => false;
        public static bool GetButtonDown(string name) => false;
        public static bool GetButtonUp(string name) => false;
        public static bool GetKey(KeyCode key) => false;
        public static bool GetKeyDown(KeyCode key) => false;
        public static bool GetKeyUp(KeyCode key) => false;
        public static bool GetMouseButton(int button) => false;
        public static bool GetMouseButtonDown(int button) => false;
        public static bool GetMouseButtonUp(int button) => false;
        public static string[] GetJoystickNames() => new string[0];
    }
}
