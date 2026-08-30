using System;
using System.Collections.Generic;

namespace UnityEngine
{
    public enum QueryTriggerInteraction { UseGlobal, Ignore, Collide }
    public enum ForceMode { Force, Acceleration, Impulse, VelocityChange }
    public enum RigidbodyInterpolation { None, Interpolate, Extrapolate }
    public enum CollisionDetectionMode { Discrete, Continuous, ContinuousDynamic, ContinuousSpeculative }

    public struct RaycastHit
    {
        public Vector3 point;
        public Vector3 normal;
        public float distance;
        public Collider collider;
        public Transform transform;
        public Rigidbody rigidbody;
    }

    public class Collider : Component
    {
        public bool enabled { get; set; }
        public bool isTrigger { get; set; }
        public Rigidbody attachedRigidbody => null;
        public Bounds bounds => default;
        public Material material { get; set; }
        public Vector3 ClosestPoint(Vector3 p) => p;
        public Vector3 ClosestPointOnBounds(Vector3 p) => p;
    }

    public class BoxCollider : Collider
    {
        public Vector3 center { get; set; }
        public Vector3 size { get; set; }
    }

    public class SphereCollider : Collider
    {
        public Vector3 center { get; set; }
        public float radius { get; set; }
    }

    public class CapsuleCollider : Collider
    {
        public Vector3 center { get; set; }
        public float radius { get; set; }
        public float height { get; set; }
        public int direction { get; set; }
    }

    public class MeshCollider : Collider
    {
        public Mesh sharedMesh { get; set; }
        public bool convex { get; set; }
    }

    public class CharacterController : Collider
    {
        public float height { get; set; }
        public float radius { get; set; }
        public Vector3 center { get; set; }
        public float slopeLimit { get; set; }
        public float stepOffset { get; set; }
        public float skinWidth { get; set; }
        public bool isGrounded => true;
        public Vector3 velocity => default;
        public CollisionFlags Move(Vector3 motion) => CollisionFlags.None;
        public bool SimpleMove(Vector3 speed) => true;
    }

    public enum CollisionFlags { None = 0, Sides = 1, Above = 2, Below = 4 }

    public class ControllerColliderHit
    {
        public Collider collider;
        public Vector3 point;
        public Vector3 normal;
        public Vector3 moveDirection;
        public Transform transform;
    }

    public class Rigidbody : Component
    {
        public float mass { get; set; }
        public float drag { get; set; }
        public float angularDrag { get; set; }
        public bool isKinematic { get; set; }
        public bool useGravity { get; set; }
        public bool detectCollisions { get; set; }
        public Vector3 velocity { get; set; }
        public Vector3 angularVelocity { get; set; }
        public Vector3 centerOfMass { get; set; }
        public Vector3 position { get; set; }
        public Quaternion rotation { get; set; }
        public RigidbodyInterpolation interpolation { get; set; }
        public CollisionDetectionMode collisionDetectionMode { get; set; }
        public void AddForce(Vector3 f) { }
        public void AddForce(Vector3 f, ForceMode m) { }
        public void AddTorque(Vector3 t) { }
        public void AddTorque(Vector3 t, ForceMode m) { }
        public void AddForceAtPosition(Vector3 f, Vector3 p) { }
        public void AddForceAtPosition(Vector3 f, Vector3 p, ForceMode m) { }
        public void AddExplosionForce(float force, Vector3 pos, float radius) { }
        public void AddExplosionForce(float force, Vector3 pos, float radius, float upward) { }
        public void AddExplosionForce(float force, Vector3 pos, float radius, float upward, ForceMode m) { }
        public Vector3 GetPointVelocity(Vector3 p) => default;
        public void MovePosition(Vector3 p) { }
        public void MoveRotation(Quaternion r) { }
        public void WakeUp() { }
        public void Sleep() { }
    }

    public class Joint : Component
    {
        public Rigidbody connectedBody { get; set; }
        public bool enableCollision { get; set; }
        public bool enablePreprocessing { get; set; }
        public Vector3 anchor { get; set; }
        public Vector3 axis { get; set; }
    }

    public struct SoftJointLimit { public float limit; public float bounciness; public float contactDistance; }
    public struct SoftJointLimitSpring { public float spring; public float damper; }

    public class CharacterJoint : Joint
    {
        public SoftJointLimit lowTwistLimit { get; set; }
        public SoftJointLimit highTwistLimit { get; set; }
        public SoftJointLimit swing1Limit { get; set; }
        public SoftJointLimit swing2Limit { get; set; }
    }

    public struct JointSpring { public float spring; public float damper; public float targetPosition; }
    public struct WheelFrictionCurve { public float extremumSlip; public float extremumValue; public float asymptoteSlip; public float asymptoteValue; public float stiffness; }
    public struct WheelHit
    {
        public Vector3 point;
        public Vector3 normal;
        public Collider collider;
        public float force;
        public float forwardSlip;
        public float sidewaysSlip;
    }

    public class WheelCollider : Collider
    {
        public float radius { get; set; }
        public float mass { get; set; }
        public float wheelDampingRate { get; set; }
        public float suspensionDistance { get; set; }
        public float forceAppPointDistance { get; set; }
        public JointSpring suspensionSpring { get; set; }
        public WheelFrictionCurve forwardFriction { get; set; }
        public WheelFrictionCurve sidewaysFriction { get; set; }
        public float motorTorque { get; set; }
        public float brakeTorque { get; set; }
        public float steerAngle { get; set; }
        public float rpm => 0f;
        public bool isGrounded => false;
        public bool GetGroundHit(out WheelHit hit) { hit = default; return false; }
        public void GetWorldPose(out Vector3 pos, out Quaternion rot) { pos = default; rot = default; }
    }

    public class ContactPoint
    {
        public Vector3 point;
        public Vector3 normal;
        public Collider thisCollider;
        public Collider otherCollider;
    }

    public class Collision
    {
        public Collider collider;
        public Transform transform;
        public GameObject gameObject;
        public Rigidbody rigidbody;
        public Vector3 relativeVelocity;
        public int contactCount => 0;
        public ContactPoint GetContact(int index) => new ContactPoint();
        public ContactPoint[] contacts => new ContactPoint[0];
    }

    public static class Physics
    {
        public static Vector3 gravity { get; set; }
        public static int defaultSolverIterations { get; set; }
        public static int defaultSolverVelocityIterations { get; set; }
        public static float sleepThreshold { get; set; }
        public static float defaultContactOffset { get; set; }
        public static bool queriesHitTriggers { get; set; }

        public static bool Raycast(Vector3 origin, Vector3 direction, float maxDistance) => false;
        public static bool Raycast(Vector3 origin, Vector3 direction, float maxDistance, int mask) => false;
        public static bool Raycast(Vector3 origin, Vector3 direction, float maxDistance, int mask, QueryTriggerInteraction q) => false;
        public static bool Raycast(Ray ray, out RaycastHit hit, float maxDistance, int mask) { hit = default; return false; }
        public static bool Raycast(Ray ray, out RaycastHit hit, float maxDistance, int mask, QueryTriggerInteraction q) { hit = default; return false; }
        public static bool Raycast(Vector3 origin, Vector3 direction, out RaycastHit hit, float maxDistance) { hit = default; return false; }
        public static bool Raycast(Vector3 origin, Vector3 direction, out RaycastHit hit, float maxDistance, int mask) { hit = default; return false; }
        public static bool Raycast(Vector3 origin, Vector3 direction, out RaycastHit hit, float maxDistance, int mask, QueryTriggerInteraction q) { hit = default; return false; }
        public static bool Linecast(Vector3 a, Vector3 b, int mask, QueryTriggerInteraction q) => false;
        public static bool Linecast(Vector3 a, Vector3 b) => false;
        public static bool SphereCast(Vector3 origin, float radius, Vector3 direction, out RaycastHit hit, float maxDistance) { hit = default; return false; }
        public static bool SphereCast(Vector3 origin, float radius, Vector3 direction, out RaycastHit hit, float maxDistance, int mask) { hit = default; return false; }
        public static bool SphereCast(Vector3 origin, float radius, Vector3 direction, out RaycastHit hit, float maxDistance, int mask, QueryTriggerInteraction q) { hit = default; return false; }
        public static bool BoxCast(Vector3 center, Vector3 halfExtents, Vector3 direction, out RaycastHit hit, Quaternion orientation, float maxDistance, int mask, QueryTriggerInteraction q) { hit = default; return false; }
        public static bool CheckSphere(Vector3 position, float radius) => false;
        public static bool CheckSphere(Vector3 position, float radius, int mask) => false;
        public static bool CheckSphere(Vector3 position, float radius, int mask, QueryTriggerInteraction q) => false;
        public static bool CheckCapsule(Vector3 start, Vector3 end, float radius, int mask, QueryTriggerInteraction q) => false;
        public static int RaycastNonAlloc(Vector3 origin, Vector3 direction, RaycastHit[] results, float maxDistance, int mask, QueryTriggerInteraction q) => 0;
        public static int RaycastNonAlloc(Ray ray, RaycastHit[] results, float maxDistance, int mask, QueryTriggerInteraction q) => 0;
        public static Collider[] OverlapSphere(Vector3 position, float radius) => new Collider[0];
        public static Collider[] OverlapSphere(Vector3 position, float radius, int mask) => new Collider[0];
        public static Collider[] OverlapSphere(Vector3 position, float radius, int mask, QueryTriggerInteraction q) => new Collider[0];
        public static int OverlapSphereNonAlloc(Vector3 position, float radius, Collider[] results, int mask, QueryTriggerInteraction q) => 0;
        public static void IgnoreLayerCollision(int a, int b, bool ignore) { }
        public static void IgnoreCollision(Collider a, Collider b, bool ignore) { }
    }

    public static class LayerMask
    {
        public static int GetMask(params string[] names) => 0;
        public static int NameToLayer(string name) => 0;
        public static string LayerToName(int layer) => "";
    }
}
