using UnityEngine;
using SanMonica.Core;
using SanMonica.Data;

namespace SanMonica.Vehicles
{
    /// <summary>Base class for everything that turns driver input into motion.</summary>
    public abstract class VehicleMotor : MonoBehaviour
    {
        protected Vehicle V;
        protected Rigidbody Body;
        protected VehicleDefinition Def;

        public float EngineRpmNormalised { get; protected set; }
        public virtual bool IsGrounded => true;
        public virtual float SlipAmount => 0f;

        protected float AirPitch, AirRoll, AirCollective;

        public virtual void Bind(Vehicle vehicle)
        {
            V = vehicle;
            Body = vehicle.Body;
            Def = vehicle.Definition;
        }

        public virtual void SetAirInput(float pitch, float roll, float collective)
        {
            AirPitch = pitch; AirRoll = roll; AirCollective = collective;
        }

        protected float MaxForce => Def != null ? Def.enginePower * 1000f / Mathf.Max(4f, Def.TopSpeedMs * 0.35f) : 4000f;
    }

    // ------------------------------------------------------------------
    /// <summary>Wheel-collider driven car, SUV, truck and bus physics.</summary>
    public class CarMotor : VehicleMotor
    {
        private WheelCollider[] _wheels;
        private Transform[] _visuals;
        private bool[] _steered;
        private bool[] _powered;
        private float _grounded;
        private float _slip;
        private float _engineRpm;

        public override bool IsGrounded => _grounded > 0.4f;
        public override float SlipAmount => _slip;

        public override void Bind(Vehicle vehicle)
        {
            base.Bind(vehicle);
            BuildWheels();
        }

        private void BuildWheels()
        {
            var positions = V.WheelPositions;
            if (positions == null || positions.Length == 0) return;
            _wheels = new WheelCollider[positions.Length];
            _steered = new bool[positions.Length];
            _powered = new bool[positions.Length];
            _visuals = V.WheelVisuals;

            float halfBase = Def.wheelbase * 0.5f;
            for (int i = 0; i < positions.Length; i++)
            {
                var go = new GameObject("Wheel" + i);
                go.transform.SetParent(transform, false);
                go.transform.localPosition = positions[i];
                go.layer = GameLayers.VehicleWheel;

                var wc = go.AddComponent<WheelCollider>();
                wc.radius = Def.wheelRadius;
                wc.mass = Mathf.Max(12f, Def.mass * 0.02f);
                wc.wheelDampingRate = 0.6f;
                wc.suspensionDistance = Def.suspensionDistance;
                wc.forceAppPointDistance = 0.1f;

                var spring = wc.suspensionSpring;
                spring.spring = Def.suspensionSpring * (Def.mass / 1500f);
                spring.damper = Def.suspensionDamper * (Def.mass / 1500f);
                spring.targetPosition = 0.45f;
                wc.suspensionSpring = spring;

                var fwd = wc.forwardFriction;
                fwd.extremumSlip = 0.36f; fwd.extremumValue = 1.0f;
                fwd.asymptoteSlip = 0.85f; fwd.asymptoteValue = 0.62f;
                fwd.stiffness = 2.2f * Def.grip;
                wc.forwardFriction = fwd;

                var side = wc.sidewaysFriction;
                side.extremumSlip = 0.26f; side.extremumValue = 1.05f;
                side.asymptoteSlip = 0.60f; side.asymptoteValue = 0.72f;
                side.stiffness = 2.6f * Def.grip;
                wc.sidewaysFriction = side;

                _wheels[i] = wc;
                _steered[i] = positions[i].z > halfBase * 0.4f;
                bool front = positions[i].z > 0f;
                _powered[i] = Def.driveType == DriveType.AllWheel
                    || (Def.driveType == DriveType.FrontWheel && front)
                    || (Def.driveType == DriveType.RearWheel && !front);
            }
        }

        private void FixedUpdate()
        {
            if (_wheels == null || V == null || Body == null) return;

            float speed = V.SpeedMs;
            float absSpeed = Mathf.Abs(speed);
            float topSpeed = Def.TopSpeedMs;

            float throttle = V.IsDestroyed || !V.EngineRunning || V.Fuel <= 0f ? 0f : V.Throttle;
            float brake = V.BrakeInput;

            // Reverse when braking from a standstill.
            bool reversing = speed < 0.5f && brake > 0.1f && throttle < 0.05f;
            float driveTorque = 0f;
            if (reversing) driveTorque = -MaxForce * 0.45f * brake * Def.wheelRadius;
            else if (throttle > 0.01f)
            {
                float curve = 1f - Mathf.Clamp01(absSpeed / Mathf.Max(1f, topSpeed));
                curve = Mathf.Max(0.08f, curve * curve * 0.7f + curve * 0.4f);
                driveTorque = MaxForce * throttle * curve * Def.acceleration * Def.wheelRadius;
            }

            float brakeTorque = 0f;
            if (!reversing && brake > 0.01f && speed > 0.5f) brakeTorque = Def.brakeTorque * brake;
            else if (throttle < 0.02f && brake < 0.02f) brakeTorque = Def.brakeTorque * 0.06f;

            float steerLimit = Mathf.Lerp(Def.steerAngle, Def.steerAngle * 0.28f, Mathf.Clamp01(absSpeed / Mathf.Max(6f, topSpeed * 0.75f)));
            float steer = V.SteerInput * steerLimit;

            int poweredCount = 0;
            for (int i = 0; i < _powered.Length; i++) if (_powered[i]) poweredCount++;
            poweredCount = Mathf.Max(1, poweredCount);

            int groundedWheels = 0;
            float slipSum = 0f;

            for (int i = 0; i < _wheels.Length; i++)
            {
                var wc = _wheels[i];
                if (_steered[i]) wc.steerAngle = Mathf.Lerp(wc.steerAngle, steer, 0.35f);
                wc.motorTorque = _powered[i] ? driveTorque / poweredCount : 0f;

                bool rear = !_steered[i];
                float wheelBrake = brakeTorque;
                if (V.HandbrakeInput && rear) wheelBrake = Def.handbrakeTorque;
                wc.brakeTorque = wheelBrake;

                if (wc.GetGroundHit(out var hit))
                {
                    groundedWheels++;
                    slipSum += Mathf.Abs(hit.sidewaysSlip) + Mathf.Abs(hit.forwardSlip) * 0.5f;
                }

                if (_visuals != null && i < _visuals.Length && _visuals[i] != null)
                {
                    wc.GetWorldPose(out var pos, out var rot);
                    _visuals[i].SetPositionAndRotation(pos, rot);
                }
            }

            _grounded = _wheels.Length > 0 ? groundedWheels / (float)_wheels.Length : 0f;
            _slip = _wheels.Length > 0 ? slipSum / _wheels.Length : 0f;

            // Handbrake slides: soften rear grip while it is held.
            if (V.HandbrakeInput)
            {
                for (int i = 0; i < _wheels.Length; i++)
                {
                    if (_steered[i]) continue;
                    var f = _wheels[i].sidewaysFriction;
                    f.stiffness = 0.85f * Def.grip;
                    _wheels[i].sidewaysFriction = f;
                }
            }
            else
            {
                for (int i = 0; i < _wheels.Length; i++)
                {
                    if (_steered[i]) continue;
                    var f = _wheels[i].sidewaysFriction;
                    f.stiffness = Mathf.Lerp(f.stiffness, 2.6f * Def.grip, 0.2f);
                    _wheels[i].sidewaysFriction = f;
                }
            }

            // Downforce and anti-roll keep the car planted at speed.
            if (groundedWheels > 0)
            {
                Body.AddForce(-transform.up * (Def.downforce * absSpeed), ForceMode.Force);
                AntiRoll(0, 1);
                if (_wheels.Length > 3) AntiRoll(2, 3);
            }
            else
            {
                // Airborne: gentle self-levelling so landings are survivable.
                Vector3 flatForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
                if (flatForward.sqrMagnitude > 0.01f)
                {
                    Quaternion level = Quaternion.LookRotation(flatForward, Vector3.up);
                    Body.MoveRotation(Quaternion.Slerp(Body.rotation, level, Time.fixedDeltaTime * 0.9f));
                }
                Body.AddTorque(transform.right * (AirPitch * Def.mass * 0.6f), ForceMode.Force);
            }

            // Speed limiter.
            if (absSpeed > topSpeed)
                Body.velocity = Vector3.ClampMagnitude(Body.velocity, topSpeed);

            _engineRpm = Mathf.Lerp(_engineRpm, Mathf.Clamp01(absSpeed / Mathf.Max(1f, topSpeed)) * 0.75f + throttle * 0.35f, Time.fixedDeltaTime * 4f);
            EngineRpmNormalised = Mathf.Clamp01(_engineRpm);

            if (throttle > 0.05f && V.Fuel > 0f) V.Fuel = Mathf.Max(0f, V.Fuel - throttle * Time.fixedDeltaTime * 0.0035f * (Def.mass / 1400f));
        }

        private void AntiRoll(int left, int right)
        {
            if (_wheels.Length <= Mathf.Max(left, right)) return;
            float travelL = 1f, travelR = 1f;
            bool groundedL = _wheels[left].GetGroundHit(out var hitL);
            if (groundedL) travelL = (-_wheels[left].transform.InverseTransformPoint(hitL.point).y - _wheels[left].radius) / _wheels[left].suspensionDistance;
            bool groundedR = _wheels[right].GetGroundHit(out var hitR);
            if (groundedR) travelR = (-_wheels[right].transform.InverseTransformPoint(hitR.point).y - _wheels[right].radius) / _wheels[right].suspensionDistance;

            float force = (travelL - travelR) * Def.suspensionSpring * 0.35f;
            if (groundedL) Body.AddForceAtPosition(_wheels[left].transform.up * -force, _wheels[left].transform.position);
            if (groundedR) Body.AddForceAtPosition(_wheels[right].transform.up * force, _wheels[right].transform.position);
        }
    }

    // ------------------------------------------------------------------
    /// <summary>Two wheels, lean-based steering and an upright stabiliser.</summary>
    public class BikeMotor : VehicleMotor
    {
        private WheelCollider _front, _rear;
        private Transform[] _visuals;
        private float _lean;
        private float _grounded;

        public override bool IsGrounded => _grounded > 0.4f;

        public override void Bind(Vehicle vehicle)
        {
            base.Bind(vehicle);
            var positions = V.WheelPositions;
            if (positions == null || positions.Length < 2) return;
            _visuals = V.WheelVisuals;
            _front = MakeWheel(positions[0], "FrontWheel");
            _rear = MakeWheel(positions[1], "RearWheel");
        }

        private WheelCollider MakeWheel(Vector3 pos, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = pos;
            go.layer = GameLayers.VehicleWheel;
            var wc = go.AddComponent<WheelCollider>();
            wc.radius = Def.wheelRadius;
            wc.mass = 14f;
            wc.suspensionDistance = Def.suspensionDistance * 0.8f;
            var spring = wc.suspensionSpring;
            spring.spring = Def.suspensionSpring * 0.35f;
            spring.damper = Def.suspensionDamper * 0.35f;
            spring.targetPosition = 0.5f;
            wc.suspensionSpring = spring;
            var side = wc.sidewaysFriction;
            side.stiffness = 2.4f * Def.grip;
            wc.sidewaysFriction = side;
            var fwd = wc.forwardFriction;
            fwd.stiffness = 2.4f * Def.grip;
            wc.forwardFriction = fwd;
            return wc;
        }

        private void FixedUpdate()
        {
            if (_front == null || _rear == null || Body == null) return;

            float speed = V.SpeedMs;
            float absSpeed = Mathf.Abs(speed);
            float throttle = V.IsDestroyed || !V.EngineRunning ? 0f : V.Throttle;
            float topSpeed = Def.TopSpeedMs;

            bool reversing = speed < 0.4f && V.BrakeInput > 0.1f && throttle < 0.05f;
            float curve = Mathf.Max(0.1f, 1f - Mathf.Clamp01(absSpeed / Mathf.Max(1f, topSpeed)));
            _rear.motorTorque = reversing ? -MaxForce * 0.3f * Def.wheelRadius : MaxForce * throttle * curve * Def.acceleration * Def.wheelRadius;
            float brake = reversing ? 0f : V.BrakeInput * Def.brakeTorque;
            _front.brakeTorque = brake * 0.65f + (V.HandbrakeInput ? Def.handbrakeTorque * 0.4f : 0f);
            _rear.brakeTorque = brake * 0.35f + (V.HandbrakeInput ? Def.handbrakeTorque : 0f);

            int grounded = 0;
            if (_front.isGrounded) grounded++;
            if (_rear.isGrounded) grounded++;
            _grounded = grounded * 0.5f;

            float steerLimit = Mathf.Lerp(Def.steerAngle, Def.steerAngle * 0.25f, Mathf.Clamp01(absSpeed / Mathf.Max(6f, topSpeed * 0.6f)));
            _front.steerAngle = Mathf.Lerp(_front.steerAngle, V.SteerInput * steerLimit, 0.4f);

            // Lean into the corner and stay upright.
            float targetLean = -V.SteerInput * Mathf.Clamp01(absSpeed / 14f) * 38f;
            _lean = Mathf.Lerp(_lean, targetLean, Time.fixedDeltaTime * 5f);

            if (grounded > 0)
            {
                Vector3 up = transform.up;
                Vector3 desiredUp = Quaternion.AngleAxis(_lean, transform.forward) * Vector3.up;
                Vector3 torque = Vector3.Cross(up, desiredUp) * Def.mass * 28f;
                Body.AddTorque(torque, ForceMode.Force);
                Body.AddForce(-transform.up * (Def.downforce * absSpeed * 0.5f));
            }

            if (_visuals != null)
            {
                if (_visuals.Length > 0 && _visuals[0] != null) { _front.GetWorldPose(out var p0, out var r0); _visuals[0].SetPositionAndRotation(p0, r0); }
                if (_visuals.Length > 1 && _visuals[1] != null) { _rear.GetWorldPose(out var p1, out var r1); _visuals[1].SetPositionAndRotation(p1, r1); }
            }

            if (absSpeed > topSpeed) Body.velocity = Vector3.ClampMagnitude(Body.velocity, topSpeed);
            EngineRpmNormalised = Mathf.Clamp01(absSpeed / Mathf.Max(1f, topSpeed) * 0.8f + throttle * 0.3f);
        }
    }

    // ------------------------------------------------------------------
    /// <summary>Buoyancy driven boat physics with a simple planing model.</summary>
    public class BoatMotor : VehicleMotor
    {
        private Vector3[] _floatPoints;
        private float _submersion;

        public override bool IsGrounded => _submersion > 0.05f;

        public override void Bind(Vehicle vehicle)
        {
            base.Bind(vehicle);
            float L = Def.length, W = Def.width;
            _floatPoints = new[]
            {
                new Vector3(-W * 0.35f, -Def.height * 0.25f, L * 0.38f),
                new Vector3(W * 0.35f, -Def.height * 0.25f, L * 0.38f),
                new Vector3(-W * 0.35f, -Def.height * 0.25f, -L * 0.38f),
                new Vector3(W * 0.35f, -Def.height * 0.25f, -L * 0.38f),
                new Vector3(0f, -Def.height * 0.28f, 0f)
            };
            if (Body != null) Body.centerOfMass = new Vector3(0f, -Def.height * 0.18f, 0f);
        }

        private void FixedUpdate()
        {
            if (Body == null || Services.Water == null) return;
            var water = Services.Water;
            float totalSub = 0f;
            float perPoint = Def.mass * 9.81f * Def.buoyancy / _floatPoints.Length;

            for (int i = 0; i < _floatPoints.Length; i++)
            {
                Vector3 world = transform.TransformPoint(_floatPoints[i]);
                float surface = water.SurfaceAt(world);
                if (surface == float.MinValue) continue;
                float depth = surface - world.y;
                if (depth <= 0f) continue;
                float k = Mathf.Clamp01(depth / Mathf.Max(0.2f, Def.height * 0.5f));
                totalSub += k;
                Body.AddForceAtPosition(Vector3.up * (perPoint * k), world, ForceMode.Force);
                // Water resistance at the contact point.
                Vector3 pointVel = Body.GetPointVelocity(world);
                Body.AddForceAtPosition(-pointVel * (Def.waterDrag * k * Def.mass * 0.02f), world, ForceMode.Force);
            }
            _submersion = totalSub / _floatPoints.Length;

            if (_submersion < 0.02f) return;

            float throttle = V.IsDestroyed || !V.EngineRunning ? 0f : V.Throttle - V.BrakeInput * 0.6f;
            Vector3 thrustPoint = transform.TransformPoint(new Vector3(0f, -Def.height * 0.2f, -Def.length * 0.45f));
            Body.AddForceAtPosition(transform.forward * (MaxForce * throttle * _submersion), thrustPoint, ForceMode.Force);

            float speed = Mathf.Abs(V.SpeedMs);
            float rudder = V.SteerInput * Mathf.Clamp01(speed / 6f + Mathf.Abs(throttle) * 0.4f);
            Body.AddTorque(Vector3.up * (rudder * Def.mass * 1.6f * _submersion), ForceMode.Force);

            // Self righting.
            Vector3 torque = Vector3.Cross(transform.up, Vector3.up) * Def.mass * 3.2f * _submersion;
            Body.AddTorque(torque, ForceMode.Force);

            if (speed > Def.TopSpeedMs) Body.velocity = Vector3.ClampMagnitude(Body.velocity, Def.TopSpeedMs);
            EngineRpmNormalised = Mathf.Clamp01(Mathf.Abs(throttle) * 0.7f + speed / Mathf.Max(1f, Def.TopSpeedMs) * 0.4f);
        }
    }

    // ------------------------------------------------------------------
    public class HelicopterMotor : VehicleMotor
    {
        private float _rotorSpeed;
        private Transform _rotor;
        private float _bladeAngle;

        public override void Bind(Vehicle vehicle)
        {
            base.Bind(vehicle);
            BuildRotor();
            if (Body != null) Body.centerOfMass = new Vector3(0f, -0.4f, 0f);
        }

        private void BuildRotor()
        {
            var go = new GameObject("Rotor");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = V.RotorLocalPosition;
            var mb = new SanMonica.Utils.MeshBuilder(1);
            float r = Mathf.Max(3f, V.RotorRadius);
            for (int i = 0; i < 4; i++)
            {
                var rot = Quaternion.Euler(0f, i * 90f, 0f);
                int s = mb.VertexCount;
                mb.AddBox(rot * new Vector3(0f, 0f, r * 0.5f), new Vector3(0.28f, 0.06f, r), rot, 0f, 0);
                mb.SetUVRange(s, mb.VertexCount, SanMonica.Utils.PaletteAtlas.UV(new Color(0.14f, 0.14f, 0.16f)));
            }
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mf.sharedMesh = mb.ToMesh("Rotor");
            mr.sharedMaterial = SanMonica.Utils.PaletteAtlas.Matte;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _rotor = go.transform;
        }

        private void FixedUpdate()
        {
            if (Body == null) return;
            bool powered = !V.IsDestroyed && V.EngineRunning && V.HasDriver;
            _rotorSpeed = Mathf.MoveTowards(_rotorSpeed, powered ? 1f : 0f, Time.fixedDeltaTime * 0.45f);

            if (_rotor != null)
            {
                _bladeAngle += _rotorSpeed * Def.rotorSpeed * 60f * Time.fixedDeltaTime;
                _rotor.localRotation = Quaternion.Euler(0f, _bladeAngle, 0f);
            }

            if (_rotorSpeed < 0.05f) return;

            float collective = Mathf.Clamp(AirCollective, -1f, 1f);
            float lift = Def.liftPower * Def.mass * _rotorSpeed * (0.62f + collective * 0.55f);
            Body.AddForce(transform.up * lift, ForceMode.Force);

            float authority = _rotorSpeed * Def.mass;
            Body.AddTorque(transform.right * (AirPitch * authority * 0.60f), ForceMode.Force);
            Body.AddTorque(-transform.forward * (AirRoll * authority * 0.55f), ForceMode.Force);
            Body.AddTorque(transform.up * (V.SteerInput * authority * 0.42f), ForceMode.Force);

            // Auto level when the pilot lets go.
            if (Mathf.Abs(AirPitch) < 0.1f && Mathf.Abs(AirRoll) < 0.1f)
            {
                Vector3 level = Vector3.Cross(transform.up, Vector3.up);
                Body.AddTorque(level * (Def.mass * 0.9f * _rotorSpeed), ForceMode.Force);
            }

            Body.AddForce(-Body.velocity * (Def.mass * 0.06f * _rotorSpeed), ForceMode.Force);
            if (Body.velocity.magnitude > Def.TopSpeedMs)
                Body.velocity = Vector3.ClampMagnitude(Body.velocity, Def.TopSpeedMs);

            EngineRpmNormalised = _rotorSpeed;
        }
    }

    // ------------------------------------------------------------------
    public class PlaneMotor : VehicleMotor
    {
        private WheelCollider[] _gear;
        private Transform[] _visuals;
        private float _thrust;

        public override void Bind(Vehicle vehicle)
        {
            base.Bind(vehicle);
            var positions = V.WheelPositions;
            _visuals = V.WheelVisuals;
            if (positions != null && positions.Length > 0)
            {
                _gear = new WheelCollider[positions.Length];
                for (int i = 0; i < positions.Length; i++)
                {
                    var go = new GameObject("Gear" + i);
                    go.transform.SetParent(transform, false);
                    go.transform.localPosition = positions[i];
                    go.layer = GameLayers.VehicleWheel;
                    var wc = go.AddComponent<WheelCollider>();
                    wc.radius = Def.wheelRadius;
                    wc.mass = 30f;
                    wc.suspensionDistance = 0.25f;
                    var spring = wc.suspensionSpring;
                    spring.spring = Def.mass * 30f;
                    spring.damper = Def.mass * 4f;
                    spring.targetPosition = 0.5f;
                    wc.suspensionSpring = spring;
                    _gear[i] = wc;
                }
            }
            if (Body != null) Body.centerOfMass = new Vector3(0f, -0.2f, Def.length * 0.02f);
        }

        private void FixedUpdate()
        {
            if (Body == null) return;
            bool powered = !V.IsDestroyed && V.EngineRunning && V.HasDriver;
            float target = powered ? Mathf.Clamp01(V.Throttle) : 0f;
            _thrust = Mathf.MoveTowards(_thrust, target, Time.fixedDeltaTime * 0.35f);

            float forwardSpeed = Vector3.Dot(Body.velocity, transform.forward);
            Body.AddForce(transform.forward * (_thrust * Def.enginePower * 26f), ForceMode.Force);

            // Lift grows with the square of airspeed, as it should.
            float lift = Def.liftPower * Def.mass * Mathf.Clamp01(forwardSpeed / 55f) * Mathf.Clamp01(forwardSpeed / 55f);
            Body.AddForce(transform.up * lift, ForceMode.Force);

            // Induced drag and airframe drag.
            Body.AddForce(-Body.velocity.normalized * (Body.velocity.sqrMagnitude * Def.mass * 0.00035f), ForceMode.Force);

            float authority = Mathf.Clamp01(Mathf.Abs(forwardSpeed) / 40f) * Def.mass;
            Body.AddTorque(transform.right * (AirPitch * authority * 0.55f), ForceMode.Force);
            Body.AddTorque(-transform.forward * (AirRoll * authority * 0.60f), ForceMode.Force);
            Body.AddTorque(transform.up * (V.SteerInput * authority * 0.22f), ForceMode.Force);

            // Weathervane: the nose follows the velocity vector.
            if (Body.velocity.sqrMagnitude > 25f)
            {
                Vector3 align = Vector3.Cross(transform.forward, Body.velocity.normalized);
                Body.AddTorque(align * (Def.mass * 0.5f), ForceMode.Force);
            }

            if (_gear != null)
            {
                bool onGround = false;
                for (int i = 0; i < _gear.Length; i++)
                {
                    _gear[i].motorTorque = _thrust * Def.mass * 0.8f;
                    _gear[i].brakeTorque = V.BrakeInput * Def.mass * 6f + (V.HandbrakeInput ? Def.mass * 12f : 0f);
                    if (i == 0) _gear[i].steerAngle = Mathf.Lerp(_gear[i].steerAngle, V.SteerInput * 18f, 0.2f);
                    if (_gear[i].isGrounded) onGround = true;
                    if (_visuals != null && i < _visuals.Length && _visuals[i] != null)
                    {
                        _gear[i].GetWorldPose(out var p, out var r);
                        _visuals[i].SetPositionAndRotation(p, r);
                    }
                }
                if (onGround && forwardSpeed < 4f)
                    Body.AddTorque(Vector3.Cross(transform.up, Vector3.up) * (Def.mass * 1.2f), ForceMode.Force);
            }

            EngineRpmNormalised = _thrust;
        }
    }
}
