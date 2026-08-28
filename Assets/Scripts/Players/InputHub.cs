using System.Collections.Generic;
using UnityEngine;

namespace SanMonica.Players
{
    /// <summary>
    /// Single input surface for the whole game. Touch controls, Bluetooth
    /// gamepads and (in the editor) keyboard + mouse all feed the same values.
    ///
    /// Devices are polled lazily, once per frame, by whichever system reads the
    /// input first. That removes any dependency on script execution order, so a
    /// button press is seen exactly once by every consumer in the same frame.
    /// </summary>
    public class InputHub : MonoBehaviour
    {
        [Header("Settings")]
        public float LookSensitivity = 1f;
        public float AimSensitivityMultiplier = 0.55f;
        public bool InvertY;
        public bool GamepadDetected;
        public bool TouchActive;

        // ---- polled state ----
        private Vector2 _move, _look;
        private float _throttle, _brake, _steer, _pitch, _roll;
        private bool _sprint, _aim, _fire, _handbrake, _crouch, _horn;
        private bool _jump, _interact, _enterVehicle, _reload, _nextWeapon, _prevWeapon;
        private bool _melee, _pause, _map, _radioNext, _cameraToggle, _phone;
        private int _polledFrame = -1;

        // ---- touch injection ----
        private Vector2 _touchMove;
        private Vector2 _touchLook;
        private readonly HashSet<string> _touchHeld = new HashSet<string>();
        private readonly HashSet<string> _touchPressed = new HashSet<string>();
        private static readonly HashSet<string> MissingAxes = new HashSet<string>();

        // ------------------------------------------------------------------
        public Vector2 Move { get { Poll(); return _move; } }
        public Vector2 Look { get { Poll(); return _look; } }
        public float Throttle { get { Poll(); return _throttle; } }
        public float Brake { get { Poll(); return _brake; } }
        public float Steer { get { Poll(); return _steer; } }
        public float Pitch { get { Poll(); return _pitch; } }
        public float Roll { get { Poll(); return _roll; } }

        public bool Sprint { get { Poll(); return _sprint; } }
        public bool Aim { get { Poll(); return _aim; } }
        public bool Fire { get { Poll(); return _fire; } }
        public bool Handbrake { get { Poll(); return _handbrake; } }
        public bool Crouch { get { Poll(); return _crouch; } }
        public bool Horn { get { Poll(); return _horn; } }

        public bool JumpPressed { get { Poll(); return _jump; } }
        public bool InteractPressed { get { Poll(); return _interact; } }
        public bool EnterVehiclePressed { get { Poll(); return _enterVehicle; } }
        public bool ReloadPressed { get { Poll(); return _reload; } }
        public bool NextWeaponPressed { get { Poll(); return _nextWeapon; } }
        public bool PrevWeaponPressed { get { Poll(); return _prevWeapon; } }
        public bool MeleePressed { get { Poll(); return _melee; } }
        public bool PausePressed { get { Poll(); return _pause; } }
        public bool MapPressed { get { Poll(); return _map; } }
        public bool RadioNextPressed { get { Poll(); return _radioNext; } }
        public bool CameraTogglePressed { get { Poll(); return _cameraToggle; } }
        public bool PhonePressed { get { Poll(); return _phone; } }

        // ------------------------------------------------------------------
        public void SetTouchMove(Vector2 v) { _touchMove = Vector2.ClampMagnitude(v, 1f); TouchActive = true; }
        public void AddTouchLook(Vector2 delta) { _touchLook += delta; TouchActive = true; }

        public void SetTouchButton(string id, bool held)
        {
            TouchActive = true;
            if (held) { if (_touchHeld.Add(id)) _touchPressed.Add(id); }
            else _touchHeld.Remove(id);
        }

        public void PressTouchButton(string id) { TouchActive = true; _touchPressed.Add(id); }
        public bool TouchHeld(string id) => _touchHeld.Contains(id);

        private void Awake()
        {
            var names = Input.GetJoystickNames();
            for (int i = 0; i < names.Length; i++)
                if (!string.IsNullOrEmpty(names[i])) { GamepadDetected = true; break; }
        }

        /// <summary>Polls once per frame, no matter who asks first.</summary>
        private void Poll()
        {
            int frame = Time.frameCount;
            if (_polledFrame == frame) return;
            _polledFrame = frame;

            // ---------------- Move ----------------
            Vector2 move = _touchMove;
            Vector2 keyboard = new Vector2(SafeAxisRaw("Horizontal"), SafeAxisRaw("Vertical"));
            if (keyboard.sqrMagnitude > 0.02f) move = Vector2.ClampMagnitude(keyboard, 1f);
            _move = move;

            // ---------------- Look ----------------
            Vector2 look = _touchLook;
            _touchLook = Vector2.zero;

            float mouseX = SafeAxis("Mouse X"), mouseY = SafeAxis("Mouse Y");
            if (Mathf.Abs(mouseX) > 0.0001f || Mathf.Abs(mouseY) > 0.0001f)
                look += new Vector2(mouseX, mouseY) * 2.2f;

            Vector2 stick = new Vector2(SafeAxisRaw("RightStickX"), SafeAxisRaw("RightStickY"));
            if (stick.sqrMagnitude > 0.04f)
            {
                GamepadDetected = true;
                look += new Vector2(stick.x, -stick.y) * 140f * Time.unscaledDeltaTime;
            }

            float sensitivity = LookSensitivity * (_aim ? AimSensitivityMultiplier : 1f);
            _look = new Vector2(look.x * sensitivity, look.y * sensitivity * (InvertY ? -1f : 1f));

            // ---------------- Held ----------------
            _sprint = TouchHeld("sprint") || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.JoystickButton8);
            _aim = TouchHeld("aim") || Input.GetMouseButton(1) || SafeAxisRaw("TriggerLeft") > 0.4f;
            _fire = TouchHeld("fire") || Input.GetMouseButton(0) || SafeAxisRaw("TriggerRight") > 0.4f || Input.GetKey(KeyCode.JoystickButton5);
            _handbrake = TouchHeld("handbrake") || Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.JoystickButton0);
            _crouch = TouchHeld("crouch") || Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C);
            _horn = TouchHeld("horn") || Input.GetKey(KeyCode.H) || Input.GetKey(KeyCode.JoystickButton9);

            // ---------------- Vehicle ----------------
            float touchThrottle = TouchHeld("throttle") ? 1f : 0f;
            float touchBrake = TouchHeld("brake") ? 1f : 0f;
            _throttle = Mathf.Max(touchThrottle, Mathf.Max(0f, keyboard.y));
            _brake = Mathf.Max(touchBrake, Mathf.Max(0f, -keyboard.y));

            float triggerThrottle = SafeAxisRaw("TriggerRight");
            float triggerBrake = SafeAxisRaw("TriggerLeft");
            if (triggerThrottle > 0.05f) _throttle = Mathf.Max(_throttle, triggerThrottle);
            if (triggerBrake > 0.05f) _brake = Mathf.Max(_brake, triggerBrake);

            _steer = Mathf.Clamp(Mathf.Abs(move.x) > 0.02f ? move.x : keyboard.x, -1f, 1f);
            _pitch = -move.y;
            _roll = move.x;

            // ---------------- Edge triggered ----------------
            _jump = _touchPressed.Contains("jump") || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.JoystickButton0);
            _interact = _touchPressed.Contains("interact") || Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.JoystickButton2);
            _enterVehicle = _touchPressed.Contains("entervehicle") || Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.JoystickButton3);
            _reload = _touchPressed.Contains("reload") || Input.GetKeyDown(KeyCode.R);
            _nextWeapon = _touchPressed.Contains("nextweapon") || Input.GetKeyDown(KeyCode.Q) || Input.mouseScrollDelta.y > 0.1f;
            _prevWeapon = _touchPressed.Contains("prevweapon") || Input.mouseScrollDelta.y < -0.1f;
            _melee = _touchPressed.Contains("melee") || Input.GetKeyDown(KeyCode.V);
            _pause = _touchPressed.Contains("pause") || Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.JoystickButton7);
            _map = _touchPressed.Contains("map") || Input.GetKeyDown(KeyCode.M) || Input.GetKeyDown(KeyCode.JoystickButton6);
            _radioNext = _touchPressed.Contains("radio") || Input.GetKeyDown(KeyCode.N);
            _cameraToggle = _touchPressed.Contains("camera") || Input.GetKeyDown(KeyCode.T);
            _phone = _touchPressed.Contains("phone") || Input.GetKeyDown(KeyCode.P);

            _touchPressed.Clear();
        }

        private void LateUpdate()
        {
            // Guarantees a poll happens even in a frame where nothing read the input,
            // so a touch press is never carried over into the next frame.
            Poll();
        }

        // Axis lookups are wrapped because a project may not define every axis.
        private static float SafeAxis(string name)
        {
            if (MissingAxes.Contains(name)) return 0f;
            try { return Input.GetAxis(name); }
            catch (System.Exception) { MissingAxes.Add(name); return 0f; }
        }

        private static float SafeAxisRaw(string name)
        {
            if (MissingAxes.Contains(name)) return 0f;
            try { return Input.GetAxisRaw(name); }
            catch (System.Exception) { MissingAxes.Add(name); return 0f; }
        }
    }
}
