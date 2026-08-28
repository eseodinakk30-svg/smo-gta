using System.Collections.Generic;
using UnityEngine;
using SanMonica.Core;
using SanMonica.Data;
using SanMonica.Utils;

namespace SanMonica.Vehicles
{
    /// <summary>
    /// Runtime instance of any drivable thing in San Monica. Holds the seats,
    /// condition, lights, audio and the motor that actually moves it.
    /// </summary>
    public class Vehicle : MonoBehaviour, IPoolable, SanMonica.Characters.IDamageable
    {
        public VehicleDefinition Definition { get; private set; }
        public Rigidbody Body { get; private set; }
        public VehicleMotor Motor { get; private set; }
        public Color Paint { get; private set; }
        public string InstanceId { get; private set; }

        [Header("State")]
        public float Health = 1000f;
        public float Fuel = 55f;
        public bool IsPlayerOwned;
        public bool HasOwner = true;          // an NPC or the city "owns" it, so taking it is theft
        public bool IsMissionVehicle;
        public bool EngineRunning = true;
        public bool SirenOn;
        public bool LightsOn;

        public bool IsDestroyed { get; private set; }
        public bool IsAlive => !IsDestroyed;
        public Transform Transform => transform;
        public string DisplayName => Definition != null ? Definition.displayName : "Vehicle";
        public float SpeedMs => Body != null ? Vector3.Dot(Body.velocity, transform.forward) : 0f;
        public float SpeedKph => SpeedMs * 3.6f;
        public float AbsSpeedKph => Mathf.Abs(SpeedKph);
        public bool AllowsDriveBy => Definition == null || !Definition.IsAircraft;
        public bool HasDriver => _occupants.Count > 0 && _occupants.ContainsKey(0);
        public bool DriverIsPlayer { get; private set; }
        public GameObject Driver => _occupants.TryGetValue(0, out var d) ? d : null;
        public int SeatCount => _seatAnchors.Count;
        public float Throttle { get; private set; }
        public float BrakeInput { get; private set; }
        public float SteerInput { get; private set; }
        public bool HandbrakeInput { get; private set; }
        public float EngineRpmNormalised => Motor != null ? Motor.EngineRpmNormalised : 0f;

        private readonly List<Transform> _seatAnchors = new List<Transform>(4);
        private readonly Dictionary<int, GameObject> _occupants = new Dictionary<int, GameObject>(4);
        private readonly List<Light> _lights = new List<Light>(4);
        private MeshRenderer _renderer;
        private MeshFilter _filter;
        private Transform _visualRoot;
        private VehicleVisual _visual;
        private VehicleAudio _audio;
        private float _hornTimer;
        private float _sirenPhase;
        private float _damageSmoke;
        private float _lastCollisionTime;
        private Material[] _materialsInstance;
        private Transform[] _wheelVisuals;
        private Transform _rotor;
        private float _rotorSpin;

        public event System.Action<Vehicle> Destroyed;

        // ------------------------------------------------------------------
        public void Construct(VehicleDefinition definition, Color paint, int seed)
        {
            Definition = definition;
            Paint = paint;
            InstanceId = definition.id + "_" + seed;
            gameObject.name = "Veh_" + definition.id;
            gameObject.layer = GameLayers.Vehicle;

            _visual = VehicleMeshBuilder.Build(definition, paint, seed);

            if (_visualRoot == null)
            {
                _visualRoot = new GameObject("Visual").transform;
                _visualRoot.SetParent(transform, false);
                _filter = _visualRoot.gameObject.AddComponent<MeshFilter>();
                _renderer = _visualRoot.gameObject.AddComponent<MeshRenderer>();
                _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            _filter.sharedMesh = _visual.Mesh;
            _renderer.sharedMaterials = _visual.Materials;

            Body = gameObject.GetComponent<Rigidbody>();
            if (Body == null) Body = gameObject.AddComponent<Rigidbody>();
            Body.mass = definition.mass;
            Body.drag = definition.IsWatercraft ? definition.waterDrag * 0.25f : 0.06f;
            Body.angularDrag = definition.IsAircraft ? 1.4f : 1.1f;
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            Body.centerOfMass = new Vector3(0f, definition.centerOfMassHeight, 0f);

            BuildColliders(definition);
            BuildSeats(definition);
            BuildLights(definition);
            BuildWheels(definition);

            Motor = CreateMotor(definition);
            Motor.Bind(this);

            _audio = gameObject.GetComponent<VehicleAudio>();
            if (_audio == null) _audio = gameObject.AddComponent<VehicleAudio>();
            _audio.Bind(this);

            Health = definition.maxHealth;
            Fuel = definition.fuelCapacity;
            IsDestroyed = false;
        }

        private VehicleMotor CreateMotor(VehicleDefinition def)
        {
            var existing = GetComponent<VehicleMotor>();
            if (existing != null) DestroyImmediate(existing);
            // Wheel colliders belong to the previous motor - clear them out so a
            // rebuilt vehicle does not end up driving on two sets of wheels.
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (child.name.StartsWith("Wheel") || child.name.StartsWith("Gear") || child.name == "Rotor")
                    DestroyImmediate(child.gameObject);
            }
            if (def.vehicleClass == VehicleClass.Helicopter) return gameObject.AddComponent<HelicopterMotor>();
            if (def.vehicleClass == VehicleClass.Plane) return gameObject.AddComponent<PlaneMotor>();
            if (def.IsWatercraft) return gameObject.AddComponent<BoatMotor>();
            if (def.IsBike) return gameObject.AddComponent<BikeMotor>();
            return gameObject.AddComponent<CarMotor>();
        }

        private void BuildColliders(VehicleDefinition def)
        {
            foreach (var old in GetComponents<BoxCollider>()) DestroyImmediate(old);
            float ride = def.IsWatercraft || def.vehicleClass == VehicleClass.Helicopter ? 0f : def.rideHeight;
            var box = gameObject.AddComponent<BoxCollider>();
            box.size = new Vector3(def.width * 0.96f, (def.height - ride) * 0.9f, def.length * 0.96f);
            box.center = new Vector3(0f, ride + (def.height - ride) * 0.5f, 0f);

            if (def.vehicleClass == VehicleClass.Plane && def.width > def.length * 0.8f)
            {
                var wing = gameObject.AddComponent<BoxCollider>();
                wing.size = new Vector3(def.width, def.height * 0.08f, def.length * 0.20f);
                wing.center = new Vector3(0f, ride + def.height * 0.35f, 0f);
            }
        }

        private void BuildSeats(VehicleDefinition def)
        {
            foreach (var s in _seatAnchors) if (s != null) Destroy(s.gameObject);
            _seatAnchors.Clear();
            _occupants.Clear();

            var positions = _visual.SeatPositions;
            if (positions == null || positions.Length == 0)
                positions = new[] { new Vector3(0f, def.rideHeight + 0.4f, 0f) };

            for (int i = 0; i < positions.Length; i++)
            {
                var t = new GameObject("Seat" + i).transform;
                t.SetParent(transform, false);
                t.localPosition = positions[i] - new Vector3(0f, def.IsBike ? 0.42f : 0.52f, 0f);
                t.localRotation = Quaternion.identity;
                _seatAnchors.Add(t);
            }
        }

        private void BuildLights(VehicleDefinition def)
        {
            foreach (var l in _lights) if (l != null) Destroy(l.gameObject);
            _lights.Clear();
            if (_visual.HeadlightPositions == null) return;

            for (int i = 0; i < _visual.HeadlightPositions.Length && i < 2; i++)
            {
                var go = new GameObject("Headlight" + i);
                go.transform.SetParent(transform, false);
                go.transform.localPosition = _visual.HeadlightPositions[i];
                go.transform.localRotation = Quaternion.Euler(6f, 0f, 0f);
                var light = go.AddComponent<Light>();
                light.type = LightType.Spot;
                light.spotAngle = 62f;
                light.range = 42f;
                light.intensity = 3.4f;
                light.color = new Color(1f, 0.96f, 0.88f);
                light.shadows = LightShadows.None;
                light.renderMode = LightRenderMode.ForceVertex;
                light.enabled = false;
                _lights.Add(light);
            }
        }

        private void BuildWheels(VehicleDefinition def)
        {
            if (_wheelVisuals != null)
                foreach (var w in _wheelVisuals) if (w != null) Destroy(w.gameObject);
            _wheelVisuals = null;
            if (_visual.WheelPositions == null || _visual.WheelPositions.Length == 0) return;

            var mesh = VehicleMeshBuilder.BuildWheel(def.wheelRadius, def.wheelWidth);
            var mats = VehicleMeshBuilder.WheelMaterials;
            _wheelVisuals = new Transform[_visual.WheelPositions.Length];
            for (int i = 0; i < _visual.WheelPositions.Length; i++)
            {
                var go = new GameObject("WheelVisual" + i);
                go.transform.SetParent(transform, false);
                go.transform.localPosition = _visual.WheelPositions[i];
                var mf = go.AddComponent<MeshFilter>();
                var mr = go.AddComponent<MeshRenderer>();
                mf.sharedMesh = mesh;
                mr.sharedMaterials = mats;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _wheelVisuals[i] = go.transform;
            }
        }

        public Transform[] WheelVisuals => _wheelVisuals;
        public Vector3[] WheelPositions => _visual.WheelPositions;
        public Vector3 ExhaustPosition => _visual.ExhaustPosition;
        public Vector3 RotorLocalPosition => _visual.RotorPosition;
        public float RotorRadius => _visual.RotorRadius;

        // ------------------------------------------------------------------
        public void SetInput(float throttle, float brake, float steer, bool handbrake)
        {
            Throttle = Mathf.Clamp01(throttle);
            BrakeInput = Mathf.Clamp01(brake);
            SteerInput = Mathf.Clamp(steer, -1f, 1f);
            HandbrakeInput = handbrake;
        }

        public void SetAirInput(float pitch, float roll, float collective)
        {
            if (Motor != null) Motor.SetAirInput(pitch, roll, collective);
        }

        public void SoundHorn()
        {
            if (_hornTimer > 0f) return;
            _hornTimer = 0.6f;
            _audio?.PlayHorn();
            GameEvents.RaiseNoise(new NoiseEvent { Position = transform.position, Loudness = 26f, Source = gameObject });
        }

        public void SetSiren(bool on)
        {
            SirenOn = on;
            _audio?.SetSiren(on);
        }

        // ------------------------------------------------------------------
        public Transform GetSeatAnchor(int index)
        {
            if (_seatAnchors.Count == 0) return transform;
            return _seatAnchors[Mathf.Clamp(index, 0, _seatAnchors.Count - 1)];
        }

        public bool IsSeatFree(int index) => !_occupants.ContainsKey(index);

        public int NearestFreeSeat(Vector3 fromPosition)
        {
            int best = -1; float bestD = float.MaxValue;
            for (int i = 0; i < _seatAnchors.Count; i++)
            {
                if (_occupants.ContainsKey(i)) continue;
                float d = (GetExitPosition(i) - fromPosition).sqrMagnitude;
                if (d < bestD) { bestD = d; best = i; }
            }
            return best < 0 ? 0 : best;
        }

        public bool TryOccupySeat(int index, GameObject occupant, bool isPlayer)
        {
            if (IsDestroyed) return false;
            if (index < 0 || index >= _seatAnchors.Count) index = 0;
            if (_occupants.ContainsKey(index))
            {
                // Pull the current occupant out if the player takes the wheel.
                if (!isPlayer) return false;
                var current = _occupants[index];
                var ai = current != null ? current.GetComponent<SanMonica.AI.PedBrain>() : null;
                if (ai != null) ai.ForceExitVehicle();
                else _occupants.Remove(index);
                if (_occupants.ContainsKey(index)) return false;
            }
            _occupants[index] = occupant;
            if (index == 0)
            {
                DriverIsPlayer = isPlayer;
                EngineRunning = true;
            }
            return true;
        }

        public void ReleaseSeat(int index)
        {
            _occupants.Remove(index);
            if (index == 0)
            {
                DriverIsPlayer = false;
                SetInput(0f, 0f, 0f, true);
            }
        }

        public void ReleaseOccupant(GameObject occupant)
        {
            int found = -1;
            foreach (var kv in _occupants) if (kv.Value == occupant) { found = kv.Key; break; }
            if (found >= 0) ReleaseSeat(found);
        }

        public Vector3 GetExitPosition(int seat)
        {
            float side = seat % 2 == 0 ? -1f : 1f;
            float half = Definition != null ? Definition.width * 0.5f + 0.85f : 1.6f;
            Vector3 local = new Vector3(side * half, 0.2f, 0f);
            if (_seatAnchors.Count > seat && seat >= 0) local.z = _seatAnchors[seat].localPosition.z;

            Vector3 world = transform.TransformPoint(local);
            // Nudge out of any wall we would spawn inside.
            if (Physics.CheckSphere(world + Vector3.up * 0.9f, 0.4f, GameLayers.VisionBlockMask, QueryTriggerInteraction.Ignore))
                world = transform.TransformPoint(new Vector3(-side * half, 0.2f, local.z));

            if (Physics.Raycast(world + Vector3.up * 3f, Vector3.down, out var hit, 8f, GameLayers.GroundMask, QueryTriggerInteraction.Ignore))
                world.y = hit.point.y + 0.1f;
            else if (Services.Map != null)
                world.y = Services.Map.SampleHeight(world.x, world.z) + 0.1f;
            return world;
        }

        // ------------------------------------------------------------------
        private void Update()
        {
            if (_hornTimer > 0f) _hornTimer -= Time.deltaTime;

            bool wantLights = LightsOn || (Services.Clock != null && Services.Clock.HeadlightsRequired);
            for (int i = 0; i < _lights.Count; i++)
                if (_lights[i] != null) _lights[i].enabled = wantLights && !IsDestroyed && EngineRunning;

            if (SirenOn)
            {
                _sirenPhase += Time.deltaTime * 4f;
                for (int i = 0; i < _lights.Count; i++)
                    if (_lights[i] != null)
                    {
                        _lights[i].enabled = true;
                        _lights[i].color = Mathf.Sin(_sirenPhase + i * Mathf.PI) > 0f
                            ? new Color(0.2f, 0.3f, 1f) : new Color(1f, 0.2f, 0.15f);
                    }
            }

            if (_rotor != null && Definition != null && Definition.rotorSpeed > 0f)
            {
                _rotorSpin += Time.deltaTime * Definition.rotorSpeed * (EngineRunning ? 60f : 0f);
                _rotor.localRotation = Quaternion.Euler(0f, _rotorSpin, 0f);
            }

            if (_damageSmoke > 0f && Services.Effects != null && Random.value < Time.deltaTime * 6f * _damageSmoke)
                Services.Effects.SpawnSmoke(transform.TransformPoint(new Vector3(0f, 0.6f, Definition.length * 0.42f)), _damageSmoke);
        }

        public void AttachRotor(Transform rotor) => _rotor = rotor;

        // ------------------------------------------------------------------
        public void ApplyDamage(in SanMonica.Characters.DamageInfo info)
        {
            if (IsDestroyed) return;
            float amount = info.Amount / Mathf.Max(0.2f, Definition != null ? Definition.crashResistance : 1f);
            Health -= amount;
            _damageSmoke = Mathf.Clamp01(1f - Health / Mathf.Max(1f, Definition != null ? Definition.maxHealth : 1000f));
            if (Health <= 0f) Explode(info.Source);
        }

        public void Explode(GameObject cause)
        {
            if (IsDestroyed) return;
            IsDestroyed = true;
            EngineRunning = false;
            SetSiren(false);

            Vector3 pos = transform.position + Vector3.up * 0.6f;
            Services.Effects?.SpawnExplosion(pos, 9f);
            GameEvents.RaiseExplosion(pos, 9f);
            GameEvents.RaiseVehicleDestroyed(gameObject);
            GameEvents.RaiseNoise(new NoiseEvent { Position = pos, Loudness = 90f, Source = gameObject, IsGunshot = true });

            if (Body != null)
            {
                Body.AddForce(Vector3.up * Definition.mass * 3.5f, ForceMode.Impulse);
                Body.AddTorque(Random.insideUnitSphere * Definition.mass * 1.5f, ForceMode.Impulse);
            }

            // Everyone inside is killed by the blast.
            var passengers = new List<GameObject>(_occupants.Values);
            foreach (var p in passengers)
            {
                if (p == null) continue;
                var health = p.GetComponent<SanMonica.Characters.CharacterHealth>();
                var brain = p.GetComponent<SanMonica.AI.PedBrain>();
                if (brain != null) brain.ForceExitVehicle();
                if (health != null)
                    health.ApplyDamage(SanMonica.Characters.DamageInfo.Simple(500f, SanMonica.Characters.DamageKind.Explosion, cause, pos, Vector3.up, 300f));
            }
            _occupants.Clear();

            if (_renderer != null)
            {
                var burnt = new Material[_renderer.sharedMaterials.Length];
                for (int i = 0; i < burnt.Length; i++) burnt[i] = MaterialLibrary.Solid(new Color(0.09f, 0.08f, 0.08f), 0.15f, 0.4f, "burnt");
                _renderer.sharedMaterials = burnt;
            }
            _damageSmoke = 1f;
            Destroyed?.Invoke(this);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (IsDestroyed || Body == null) return;
            float impact = collision.relativeVelocity.magnitude;
            if (impact < 4f) return;
            if (Time.time - _lastCollisionTime < 0.15f) return;
            _lastCollisionTime = Time.time;

            float damage = (impact - 4f) * (impact - 4f) * 0.55f;
            ApplyDamage(SanMonica.Characters.DamageInfo.Simple(damage, SanMonica.Characters.DamageKind.Vehicle,
                Driver, collision.GetContact(0).point, -collision.relativeVelocity.normalized, impact * 30f));

            _audio?.PlayImpact(Mathf.Clamp01(impact / 25f));
            if (DriverIsPlayer) Services.Camera?.Shake(Mathf.Clamp01(impact / 30f) * 0.7f, 0.3f);

            // Running someone over.
            var ped = collision.collider.GetComponentInParent<SanMonica.Characters.CharacterHealth>();
            if (ped != null && impact > 6f)
            {
                ped.ApplyDamage(SanMonica.Characters.DamageInfo.Simple(impact * 12f, SanMonica.Characters.DamageKind.Vehicle,
                    Driver, collision.GetContact(0).point, Body.velocity.normalized, impact * 45f));
                if (DriverIsPlayer)
                    GameEvents.RaiseCrime(new CrimeEvent
                    {
                        Type = ped.IsAlive ? CrimeType.Assault : CrimeType.HitAndRun,
                        Position = transform.position,
                        Perpetrator = Driver
                    });
            }

            GameEvents.RaiseNoise(new NoiseEvent { Position = transform.position, Loudness = Mathf.Min(60f, impact * 3f), Source = gameObject });
        }

        // ------------------------------------------------------------------
        public void OnSpawned()
        {
            IsDestroyed = false;
            if (Definition != null) { Health = Definition.maxHealth; Fuel = Definition.fuelCapacity; }
            _damageSmoke = 0f;
            EngineRunning = true;
            _occupants.Clear();
            if (Body != null)
            {
                Body.velocity = Vector3.zero;
                Body.angularVelocity = Vector3.zero;
            }
            if (_renderer != null && _visual.Materials != null) _renderer.sharedMaterials = _visual.Materials;
        }

        public void OnDespawned()
        {
            SetSiren(false);
            _occupants.Clear();
            IsPlayerOwned = false;
            IsMissionVehicle = false;
        }
    }
}
