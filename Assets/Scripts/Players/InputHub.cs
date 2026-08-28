using System.Collections.Generic;
using UnityEngine;

namespace SanMonica.Players
{
    /// <summary>
    /// Single input surface for the whole game. Touch controls, Bluetooth
    /// gamepads and (in the editor) keyboard + mouse all feed the same struct,
    /// so gameplay code never needs to know which device is driving it.
    /// </summary>
    public class InputHub : MonoBehaviour
    {
        // ---- Continuous ----
        public Vector2 Move;            // left stick / left thumb area
        public Vector2 Look;            // per-frame look delta in degrees
        public float Throttle;          // vehicles
        public float Brake;
        public float Steer;
        public float Pitch;             // aircraft
        public float Roll;

        // ---- Held ----
        public bool Sprint;
        public bool Aim;
        public bool Fire;
        public bool Handbrake;
        public bool Crouch;
        public bool Horn;

        // ---- Edge triggered ----
        public bool JumpPressed;
        public bool InteractPressed;
        public bool EnterVehiclePressed;
        public bool ReloadPressed;
        public bool NextWeaponPressed;
        public bool PrevWeaponPressed;
        public bool MeleePressed;
        public bool PausePressed;
        public bool MapPressed;
        public bool RadioNextPressed;
        public bool CameraTogglePressed;
        public bool PhonePressed;

        [Header("Settings")]
        public float LookSensitivity = 1f;
        public float AimSensitivityMultiplier = 0.55f;
        public bool InvertY;
        public bool GamepadDetected;
        public bool TouchActive;

        // ---- Touch injection (written by the on-screen controls) ----
        private Vector2 _touchMove;
        private Vector2 _touchLook;
        private readonly HashSet<string> _touchHeld = new HashSet<string>();
        private readonly HashSet<string> _touchPressed = new HashSet<string>();
        private static readonly HashSet<string> MissingAxes = new HashSet<string>();

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

        private void Update()
        {
            bool touch = TouchActive;

            // ---------------- Move ----------------
            Vector2 move = _touchMove;
            Vector2 kb = new Vector2(SafeAxisRaw("Horizontal"), SafeAxisRaw("Vertical"));
            if (kb.sqrMagnitude > 0.02f) { move = Vector2.ClampMagnitude(kb, 1f); touch = false; }

            // ---------------- Look ----------------
            Vector2 look = _touchLook;
            float mouseX = SafeAxis("Mouse X"), mouseY = SafeAxis("Mouse Y");
            if (Mathf.Abs(mouseX) > 0.0001f || Mathf.Abs(mouseY) > 0.0001f)
                look += new Vector2(mouseX, mouseY) * 2.2f;

            Vector2 stick = new Vector2(SafeAxisRaw("RightStickX"), SafeAxisRaw("RightStickY"));
            if (stick.sqrMagnitude > 0.04f)
            {
                GamepadDetected = true;
                look += new Vector2(stick.x, -stick.y) * 140f * Time.unscaledDeltaTime;
            }

            float sens = LookSensitivity * (Aim ? AimSensitivityMultiplier : 1f);
            Look = new Vector2(look.x * sens, look.y * sens * (InvertY ? -1f : 1f));
            _touchLook = Vector2.zero;

            Move = move;

            // ---------------- Held ----------------
            Sprint = TouchHeld("sprint") || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.JoystickButton8);
            Aim = TouchHeld("aim") || Input.GetMouseButton(1) || SafeAxisRaw("TriggerLeft") > 0.4f;
            Fire = TouchHeld("fire") || Input.GetMouseButton(0) || SafeAxisRaw("TriggerRight") > 0.4f || Input.GetKey(KeyCode.JoystickButton5);
            Handbrake = TouchHeld("handbrake") || Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.JoystickButton0);
            Crouch = TouchHeld("crouch") || Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C);
            Horn = TouchHeld("horn") || Input.GetKey(KeyCode.H) || Input.GetKey(KeyCode.JoystickButton9);

            // ---------------- Vehicle ----------------
            float touchThrottle = TouchHeld("throttle") ? 1f : 0f;
            float touchBrake = TouchHeld("brake") ? 1f : 0f;
            Throttle = Mathf.Max(touchThrottle, Mathf.Max(0f, kb.y));
            Brake = Mathf.Max(touchBrake, Mathf.Max(0f, -kb.y));
            float trigT = SafeAxisRaw("TriggerRight");
            float trigB = SafeAxisRaw("TriggerLeft");
            if (trigT > 0.05f) Throttle = Mathf.Max(Throttle, trigT);
            if (trigB > 0.05f) Brake = Mathf.Max(Brake, trigB);
            Steer = Mathf.Clamp(move.x + kb.x, -1f, 1f);
            if (Mathf.Abs(move.x) > 0.02f) Steer = move.x;
            Pitch = -move.y;
            Roll = move.x;

            // ---------------- Edge ----------------
            JumpPressed = Consume("jump") || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.JoystickButton0);
            InteractPressed = Consume("interact") || Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.JoystickButton2);
            EnterVehiclePressed = Consume("entervehicle") || Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.JoystickButton3);
            ReloadPressed = Consume("reload") || Input.GetKeyDown(KeyCode.R) || Input.GetKeyDown(KeyCode.JoystickButton2);
            NextWeaponPressed = Consume("nextweapon") || Input.GetKeyDown(KeyCode.Q) || Input.mouseScrollDelta.y > 0.1f;
            PrevWeaponPressed = Consume("prevweapon") || Input.mouseScrollDelta.y < -0.1f;
            MeleePressed = Consume("melee") || Input.GetKeyDown(KeyCode.V);
            PausePressed = Consume("pause") || Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.JoystickButton7);
            MapPressed = Consume("map") || Input.GetKeyDown(KeyCode.M) || Input.GetKeyDown(KeyCode.JoystickButton6);
            RadioNextPressed = Consume("radio") || Input.GetKeyDown(KeyCode.N);
            CameraTogglePressed = Consume("camera") || Input.GetKeyDown(KeyCode.T);
            PhonePressed = Consume("phone") || Input.GetKeyDown(KeyCode.P);

            _touchPressed.Clear();
        }

        private bool Consume(string id) => _touchPressed.Contains(id);

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

        public void ClearFrame()
        {
            JumpPressed = InteractPressed = EnterVehiclePressed = ReloadPressed = false;
            NextWeaponPressed = PrevWeaponPressed = MeleePressed = PausePressed = false;
            MapPressed = RadioNextPressed = CameraTogglePressed = PhonePressed = false;
        }
    }
}
