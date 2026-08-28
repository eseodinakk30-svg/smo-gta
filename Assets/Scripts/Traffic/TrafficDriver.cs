using UnityEngine;
using SanMonica.Core;
using SanMonica.Vehicles;
using SanMonica.World;

namespace SanMonica.Traffic
{
    public enum DriverMood { Normal, Hurried, Cautious, Panicked, Pursuing }

    /// <summary>
    /// AI driver that follows the lane graph, obeys signals, brakes for
    /// obstacles, overtakes when blocked, and panics when shots are fired.
    /// One component drives everything from a taxi to a police interceptor.
    /// </summary>
    public class TrafficDriver : MonoBehaviour
    {
        public Vehicle Vehicle;
        public DriverMood Mood = DriverMood.Normal;
        public bool IsPolice;
        public Transform ChaseTarget;
        public float AggressionScale = 1f;

        [Header("Lane state")]
        public int SegmentIndex = -1;
        public bool Forward = true;
        public int Lane;
        public float LaneT;

        private RoadNetwork _roads;
        private float _speedLimit = 12f;
        private float _blockedTimer;
        private float _laneChangeCooldown;
        private float _panicTimer;
        private float _stuckTimer;
        private Vector3 _lastPosition;
        private float _repathTimer;
        private float _updateTimer;
        private int _lodLevel;

        public bool HasLane => SegmentIndex >= 0;

        public void Initialize(Vehicle vehicle, RoadNetwork roads, int segment, bool forward, int lane, float t)
        {
            Vehicle = vehicle;
            _roads = roads;
            SegmentIndex = segment;
            Forward = forward;
            Lane = lane;
            LaneT = t;
            _lastPosition = transform.position;
            _speedLimit = roads.SegmentSpeedLimit(segment);
        }

        public void SetLod(int lod) => _lodLevel = lod;

        public void Panic(float duration = 8f)
        {
            _panicTimer = Mathf.Max(_panicTimer, duration);
            Mood = DriverMood.Panicked;
        }

        private void FixedUpdate()
        {
            if (Vehicle == null || Vehicle.IsDestroyed || _roads == null) return;

            // Distant traffic updates less often - the AI level of detail.
            if (_lodLevel > 0)
            {
                _updateTimer -= Time.fixedDeltaTime;
                if (_updateTimer > 0f) return;
                _updateTimer = _lodLevel == 1 ? 0.08f : 0.25f;
            }

            if (_panicTimer > 0f)
            {
                _panicTimer -= Time.fixedDeltaTime;
                if (_panicTimer <= 0f && Mood == DriverMood.Panicked) Mood = DriverMood.Normal;
            }

            if (ChaseTarget != null) DriveChase();
            else DriveLane();
        }

        // ------------------------------------------------------------------
        private void DriveLane()
        {
            if (SegmentIndex < 0 || SegmentIndex >= _roads.Segments.Count) { Vehicle.SetInput(0f, 1f, 0f, true); return; }

            var seg = _roads.Segments[SegmentIndex];
            Vector3 pos = transform.position;

            // Advance the lane parameter to just ahead of the car.
            Vector2 flat = new Vector2(pos.x, pos.z);
            RoadNetwork.DistanceToSegment(flat, in seg, out float tOnSeg);
            LaneT = Forward ? tOnSeg : 1f - tOnSeg;

            float lookaheadDistance = Mathf.Clamp(6f + Vehicle.AbsSpeedKph * 0.22f, 6f, 26f);
            float lookaheadT = LaneT + lookaheadDistance / Mathf.Max(4f, seg.Length);

            Vector3 target;
            if (lookaheadT >= 1f)
            {
                int node = Forward ? seg.NodeB : seg.NodeA;
                target = ChooseNextTarget(node, lookaheadT - 1f, out bool arrivedAtJunction, out bool mustStop);
                if (mustStop)
                {
                    float distanceToNode = Vector3.Distance(pos, new Vector3(_roads.Nodes[node].Pos.x, pos.y, _roads.Nodes[node].Pos.y));
                    if (distanceToNode < 16f)
                    {
                        Steer(target, 0f, Mathf.Clamp01(1f - distanceToNode / 16f) * 0.9f + 0.1f, false);
                        return;
                    }
                }
                if (arrivedAtJunction) { /* the segment was swapped inside ChooseNextTarget */ }
            }
            else target = _roads.LanePoint(SegmentIndex, Lane, Forward, lookaheadT);

            // Obstacle avoidance.
            float throttle = 1f;
            float brake = 0f;
            if (ScanAhead(out float distance, out bool isVehicle))
            {
                float safe = 5f + Vehicle.AbsSpeedKph * 0.28f;
                if (distance < safe)
                {
                    float k = Mathf.Clamp01(1f - distance / safe);
                    throttle = 1f - k;
                    brake = k * (isVehicle ? 0.85f : 1f);
                    _blockedTimer += Time.fixedDeltaTime;
                }
                else _blockedTimer = 0f;
            }
            else _blockedTimer = 0f;

            // Overtake when stuck behind something for a while.
            _laneChangeCooldown -= Time.fixedDeltaTime;
            if (_blockedTimer > 2.2f && _laneChangeCooldown <= 0f && seg.LanesPerDirection > 1)
            {
                Lane = (Lane + 1) % seg.LanesPerDirection;
                _laneChangeCooldown = 4f;
                _blockedTimer = 0f;
            }

            // Unstick: reverse out if we have not moved.
            float moved = (transform.position - _lastPosition).sqrMagnitude;
            _lastPosition = transform.position;
            if (moved < 0.0004f && Vehicle.Throttle > 0.2f) _stuckTimer += Time.fixedDeltaTime;
            else _stuckTimer = Mathf.Max(0f, _stuckTimer - Time.fixedDeltaTime * 0.5f);

            if (_stuckTimer > 2.5f)
            {
                Vehicle.SetInput(0f, 1f, -Vehicle.SteerInput, false);
                if (_stuckTimer > 4.5f) { _stuckTimer = 0f; Reseat(); }
                return;
            }

            float moodScale = Mood == DriverMood.Hurried ? 1.25f : Mood == DriverMood.Cautious ? 0.75f : Mood == DriverMood.Panicked ? 1.5f : 1f;
            Steer(target, throttle * moodScale, brake, false);
        }

        private Vector3 ChooseNextTarget(int node, float overshoot, out bool switched, out bool mustStop)
        {
            switched = false;
            mustStop = false;
            var roadNode = _roads.Nodes[node];

            if (roadNode.HasTrafficLight && Mood != DriverMood.Panicked && !IsPolice)
            {
                if (Services.Traffic != null && !Services.Traffic.Signals.IsGreen(node, SegmentIndex))
                {
                    mustStop = true;
                    return _roads.LanePoint(SegmentIndex, Lane, Forward, 0.995f);
                }
            }

            // Pick a continuation, preferring to go straight on.
            var seg = _roads.Segments[SegmentIndex];
            Vector2 heading = Forward ? seg.Dir : -seg.Dir;
            int best = -1;
            float bestScore = float.MinValue;
            var rng = new Rng(Mathf.RoundToInt(Time.time * 13f) + node * 31 + GetInstanceID());

            for (int i = 0; i < roadNode.Segments.Count; i++)
            {
                int candidate = roadNode.Segments[i];
                if (candidate == SegmentIndex) continue;
                var cs = _roads.Segments[candidate];
                if (cs.Kind == RoadKind.Runway || cs.Kind == RoadKind.Taxiway) continue;
                bool candidateForward = cs.NodeA == node;
                Vector2 candidateDir = candidateForward ? cs.Dir : -cs.Dir;
                float align = Vector2.Dot(heading, candidateDir);
                float score = align * 2.2f + rng.Value * 0.9f;
                if (score > bestScore) { bestScore = score; best = candidate; }
            }

            if (best < 0)
            {
                // Dead end: turn around.
                Forward = !Forward;
                switched = true;
                return _roads.LanePoint(SegmentIndex, Lane, Forward, 0.05f);
            }

            var next = _roads.Segments[best];
            SegmentIndex = best;
            Forward = next.NodeA == node;
            Lane = Mathf.Min(Lane, next.LanesPerDirection - 1);
            LaneT = 0f;
            _speedLimit = _roads.SegmentSpeedLimit(best);
            switched = true;
            return _roads.LanePoint(SegmentIndex, Lane, Forward, Mathf.Clamp01(overshoot));
        }

        private void Reseat()
        {
            int seg = _roads.NearestSegment(new Vector2(transform.position.x, transform.position.z), 200f);
            if (seg < 0) return;
            SegmentIndex = seg;
            var s = _roads.Segments[seg];
            RoadNetwork.DistanceToSegment(new Vector2(transform.position.x, transform.position.z), in s, out float t);
            Vector2 dir = s.Dir;
            Vector3 fwd = transform.forward;
            Forward = Vector2.Dot(dir, new Vector2(fwd.x, fwd.z)) >= 0f;
            LaneT = Forward ? t : 1f - t;
            Lane = 0;
        }

        // ------------------------------------------------------------------
        private void DriveChase()
        {
            if (ChaseTarget == null) return;
            Vector3 target = ChaseTarget.position;
            float distance = Vector3.Distance(transform.position, target);

            float throttle = 1f;
            float brake = 0f;
            if (distance < 8f && Vehicle.AbsSpeedKph > 30f) { throttle = 0.2f; brake = 0.5f; }

            if (ScanAhead(out float obstacle, out bool isVehicle) && obstacle < 6f && isVehicle)
            {
                throttle *= 0.4f;
                brake = Mathf.Max(brake, 0.5f);
            }

            Steer(target, throttle * AggressionScale, brake, distance < 14f);
        }

        // ------------------------------------------------------------------
        private void Steer(Vector3 target, float throttleScale, float brake, bool allowHandbrake)
        {
            Vector3 local = transform.InverseTransformPoint(new Vector3(target.x, transform.position.y, target.z));
            float steer = Mathf.Clamp(local.x / Mathf.Max(2.5f, Mathf.Abs(local.z)), -1f, 1f);

            float desiredSpeed = _speedLimit * Mathf.Clamp01(throttleScale);
            if (Mathf.Abs(steer) > 0.45f) desiredSpeed *= 0.6f;
            if (IsPolice && ChaseTarget != null) desiredSpeed = Vehicle.Definition.TopSpeedMs * 0.9f;

            float speed = Vehicle.SpeedMs;
            float throttle = 0f;
            if (speed < desiredSpeed - 0.5f) throttle = Mathf.Clamp01((desiredSpeed - speed) / 5f);
            else if (speed > desiredSpeed + 1.5f) brake = Mathf.Max(brake, Mathf.Clamp01((speed - desiredSpeed) / 8f));

            // Reverse out of a wall.
            if (local.z < -1f && speed < 2f) { throttle = 0f; brake = 1f; steer = -steer; }

            bool handbrake = allowHandbrake && Mathf.Abs(steer) > 0.8f && speed > 12f;
            Vehicle.SetInput(throttle, brake, steer, handbrake);
        }

        private bool ScanAhead(out float distance, out bool isVehicle)
        {
            distance = float.MaxValue;
            isVehicle = false;
            float length = Vehicle.Definition != null ? Vehicle.Definition.length : 4.5f;
            float width = Vehicle.Definition != null ? Vehicle.Definition.width : 1.9f;
            Vector3 origin = transform.position + transform.forward * (length * 0.5f + 0.2f) + Vector3.up * 0.6f;
            float range = Mathf.Clamp(4f + Vehicle.AbsSpeedKph * 0.42f, 6f, 40f);

            int mask = GameLayers.VehicleMask | (1 << GameLayers.Ped) | (1 << GameLayers.Player) | (1 << GameLayers.Building) | (1 << GameLayers.Prop);
            if (Physics.BoxCast(origin, new Vector3(width * 0.45f, 0.5f, 0.2f), transform.forward,
                    out var hit, transform.rotation, range, mask, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(transform)) return false;
                distance = hit.distance;
                isVehicle = hit.collider.gameObject.layer == GameLayers.Vehicle;
                return true;
            }
            return false;
        }
    }
}
