using System.Collections.Generic;
using Gamekit2D;
using UnityEngine;

namespace CrystalRuins
{
    /// <summary>Routes the actual player body and teleport beacons, never pooled normal bullets.</summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class RelicPortal : MonoBehaviour
    {
        public RelicGameManager manager;
        public RelicPortal destination;
        [Tooltip("Player root/feet arrival when this portal is a destination. Leave some clearance above the floor.")]
        public Transform playerArrival;
        [Tooltip("Beacon center arrival when this portal is a destination.")]
        public Transform projectileArrival;
        public Vector2 outgoingDirection = Vector2.right;
        public bool allowPlayer = true;
        public bool allowProjectiles = true;
        public string portalName = "Portal";

        private BoxCollider2D trigger;
        private float nextBlockedHint;
        private static readonly List<RelicPortal> ActivePortals = new List<RelicPortal>();
        private static readonly Dictionary<Collider2D, float> PlayerCooldowns = new Dictionary<Collider2D, float>();
        private static readonly Dictionary<Collider2D, RelicPortal> PlayerArrivalLocks = new Dictionary<Collider2D, RelicPortal>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            ActivePortals.Clear();
            PlayerCooldowns.Clear();
            PlayerArrivalLocks.Clear();
        }

        private void Awake()
        {
            trigger = GetComponent<BoxCollider2D>();
            trigger.isTrigger = true;
        }

        private void OnEnable()
        {
            if (trigger == null) trigger = GetComponent<BoxCollider2D>();
            if (!ActivePortals.Contains(this)) ActivePortals.Add(this);
        }

        private void OnDisable() { ActivePortals.Remove(this); }
        private void OnTriggerEnter2D(Collider2D other) { TryTeleportPlayer(other); }
        private void OnTriggerStay2D(Collider2D other) { TryTeleportPlayer(other); }
        private void OnTriggerExit2D(Collider2D other)
        {
            if (other != null && PlayerArrivalLocks.TryGetValue(other, out RelicPortal locked) && locked == this)
                PlayerArrivalLocks.Remove(other);
        }

        public bool TryTeleportPlayer(Collider2D other)
        {
            if (manager == null) manager = RelicGameManager.Instance;
            if (!isActiveAndEnabled || !allowPlayer || manager == null || !manager.IsPlaying ||
                !manager.IsPlayer(other) || !DestinationAvailable() || destination.playerArrival == null) return false;
            if (PlayerArrivalLocks.TryGetValue(other, out RelicPortal arrivalLock) && arrivalLock == this) return false;
            if (PlayerCooldowns.TryGetValue(other, out float until) && Time.time < until) return false;

            var player = manager.player;
            var ability = player.GetComponent<RelicTeleportAbility>();
            var controller = player.GetComponent<CharacterController2D>();
            Vector2 arrival = destination.playerArrival.position;
            if (ability == null || controller == null || !ability.IsDestinationClear(arrival))
            {
                if (Time.time >= nextBlockedHint)
                {
                    manager.ShowHint("Portal arrival is blocked. Clear the landing area first.", 2f);
                    nextBlockedHint = Time.time + 2f;
                }
                return false;
            }

            // Apply the cooldown before syncing physics, which can deliver arrival callbacks.
            PlayerCooldowns[other] = Time.time + 0.5f;
            player.SetMoveVector(Vector2.zero);
            Rigidbody2D body = controller.Rigidbody2D;
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
            }
            controller.Teleport(arrival);
            if (body != null) body.position = arrival;
            player.transform.position = new Vector3(arrival.x, arrival.y, player.transform.position.z);
            Physics2D.SyncTransforms();
            // A deliberately overlapping two-way receiver must be exited before it can
            // send Ellen back. A timer alone would bounce a stationary player forever.
            if (destination.trigger != null && destination.trigger.enabled && Physics2D.Distance(other, destination.trigger).isOverlapped)
                PlayerArrivalLocks[other] = destination;
            else
                PlayerArrivalLocks.Remove(other);
            controller.CheckCapsuleEndCollisions();
            controller.CheckCapsuleEndCollisions(false);
            var animator = player.GetComponent<Animator>();
            if (animator != null)
            {
                animator.SetBool("Grounded", controller.IsGrounded);
                animator.SetFloat("HorizontalSpeed", 0f);
                animator.SetFloat("VerticalSpeed", 0f);
            }
            foreach (RelicCamera camera in FindObjectsByType<RelicCamera>(FindObjectsSortMode.None))
                if (camera.target == player.transform) camera.SnapToTarget();
            manager.ShowHint("PORTAL ARRIVAL  /  " + destination.portalName, 2.5f);
            return true;
        }

        private bool DestinationAvailable()
        {
            // Entry permissions belong to the source. A receiver can intentionally have both off.
            return destination != null && destination != this && destination.isActiveAndEnabled;
        }

        public bool TryGetProjectileDestination(out Vector2 arrival, out Vector2 direction)
        {
            arrival = Vector2.zero;
            direction = Vector2.right;
            if (!isActiveAndEnabled || !allowProjectiles || trigger == null || !trigger.enabled ||
                !DestinationAvailable() || destination.projectileArrival == null) return false;
            arrival = destination.projectileArrival.position;
            direction = destination.outgoingDirection.sqrMagnitude > 0.0001f ?
                destination.outgoingDirection.normalized : Vector2.right;
            return true;
        }

        internal static bool FindProjectileEntry(Vector2 origin, Vector2 direction, float distance, float radius,
            RelicPortal justExited, Dictionary<RelicPortal, float> cooldowns, float age,
            out RelicPortal portal, out float entryDistance)
        {
            portal = null;
            entryDistance = float.PositiveInfinity;
            foreach (RelicPortal candidate in ActivePortals)
            {
                if (candidate == null || !candidate.TryGetProjectileDestination(out _, out _)) continue;
                if (cooldowns.TryGetValue(candidate, out float until) && age < until) continue;
                if (candidate == justExited && candidate.IntersectsCircle(origin, radius)) continue;
                if (candidate.SweepCircle(origin, direction, distance, radius, out float hit) && hit < entryDistance)
                {
                    entryDistance = hit;
                    portal = candidate;
                }
            }
            return portal != null;
        }

        internal bool IntersectsCircle(Vector2 center, float radius)
        {
            return SweepCircle(center, Vector2.right, 0f, radius, out _);
        }

        // An exact swept circle against an oriented box: four face segments and four
        // rounded corners. Unlike a point trigger callback this also catches a fast beacon.
        private bool SweepCircle(Vector2 origin, Vector2 direction, float length, float radius, out float distance)
        {
            distance = float.PositiveInfinity;
            if (trigger == null || !trigger.enabled) return false;
            Vector2 center = trigger.transform.TransformPoint(trigger.offset);
            Vector2 right = trigger.transform.right;
            Vector2 up = trigger.transform.up;
            Vector3 scale = trigger.transform.lossyScale;
            Vector2 half = new Vector2(Mathf.Abs(trigger.size.x * scale.x), Mathf.Abs(trigger.size.y * scale.y)) * 0.5f;
            Vector2 relative = origin - center;
            Vector2 p = new Vector2(Vector2.Dot(relative, right), Vector2.Dot(relative, up));
            Vector2 d = new Vector2(Vector2.Dot(direction, right), Vector2.Dot(direction, up));
            float r = Mathf.Max(0f, radius) + trigger.edgeRadius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
            Vector2 outside = new Vector2(Mathf.Max(0f, Mathf.Abs(p.x) - half.x), Mathf.Max(0f, Mathf.Abs(p.y) - half.y));
            if (outside.sqrMagnitude <= r * r)
            {
                distance = 0f;
                return true;
            }
            if (length <= 0f) return false;

            if (Mathf.Abs(d.x) > 0.00001f)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    float t = (side * (half.x + r) - p.x) / d.x;
                    if (t >= 0f && t <= length && Mathf.Abs(p.y + d.y * t) <= half.y)
                        distance = Mathf.Min(distance, t);
                }
            }
            if (Mathf.Abs(d.y) > 0.00001f)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    float t = (side * (half.y + r) - p.y) / d.y;
                    if (t >= 0f && t <= length && Mathf.Abs(p.x + d.x * t) <= half.x)
                        distance = Mathf.Min(distance, t);
                }
            }
            float a = d.sqrMagnitude;
            if (a > 0.00001f)
            {
                for (int x = -1; x <= 1; x += 2)
                    for (int y = -1; y <= 1; y += 2)
                    {
                        Vector2 delta = p - new Vector2(x * half.x, y * half.y);
                        float b = Vector2.Dot(delta, d);
                        float discriminant = b * b - a * (delta.sqrMagnitude - r * r);
                        if (discriminant < 0f) continue;
                        float t = (-b - Mathf.Sqrt(discriminant)) / a;
                        if (t >= 0f && t <= length) distance = Mathf.Min(distance, t);
                    }
            }
            return distance <= length;
        }
    }
}
