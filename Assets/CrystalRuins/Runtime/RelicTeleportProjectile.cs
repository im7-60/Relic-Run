using System.Collections.Generic;
using UnityEngine;

namespace CrystalRuins
{
    /// <summary>A non-damaging beacon. Swept casts prevent tunnelling; no physics trigger can collect relics.</summary>
    public class RelicTeleportProjectile : MonoBehaviour
    {
        public enum EndReason { Impact, Range, Lifetime, Replaced, Teleported, Cancelled }
        public bool IsFlying { get; private set; }
        public float TravelledDistance { get; private set; }
        public float Age { get; private set; }
        public int PortalHops { get; private set; }
        public float Remaining01 => !IsFlying ? 0f : Mathf.Clamp01(Mathf.Min(1f - Age / lifetime, 1f - TravelledDistance / range));

        private RelicTeleportAbility owner;
        private Vector2 direction;
        private float speed;
        private float range;
        private float lifetime;
        private float radius;
        private ContactFilter2D collisionFilter;
        private SpriteRenderer glow;
        private Transform core;
        private readonly RaycastHit2D[] hits = new RaycastHit2D[16];
        private readonly Collider2D[] overlaps = new Collider2D[16];
        private readonly Transform[] trails = new Transform[4];
        private readonly Dictionary<RelicPortal, float> portalCooldowns = new Dictionary<RelicPortal, float>();
        private RelicPortal justExitedPortal;
        private RelicCamera previewCamera;
        private static Sprite glowSprite;

        public static Sprite GlowSprite
        {
            get
            {
                if (glowSprite != null) return glowSprite;
                const int size = 48;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                texture.name = "Teleport Beacon Glow";
                texture.filterMode = FilterMode.Bilinear;
                texture.wrapMode = TextureWrapMode.Clamp;
                var pixels = new Color[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), Vector2.one * size * 0.5f) / (size * 0.5f);
                        float alpha = Mathf.Pow(Mathf.Clamp01(1f - distance), 1.5f);
                        pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                    }
                texture.SetPixels(pixels);
                texture.Apply(false, true);
                glowSprite = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * 0.5f, size);
                glowSprite.name = "Teleport Beacon Glow";
                return glowSprite;
            }
        }

        public void Initialize(RelicTeleportAbility ability, Vector2 origin, Vector2 travelDirection,
            float travelSpeed, float maximumRange, float maximumLifetime, float collisionRadius,
            ContactFilter2D filter, Sprite sprite, Material material, int sortingLayer, int sortingOrder)
        {
            owner = ability;
            transform.position = origin;
            direction = travelDirection.normalized;
            speed = travelSpeed;
            range = maximumRange;
            lifetime = maximumLifetime;
            radius = collisionRadius;
            collisionFilter = filter;
            IsFlying = true;
            glow = AddVisual("Cyan Aura", GlowSprite, new Color(0.1f, 1f, 0.92f, 0.8f), Vector3.zero,
                new Vector3(1.3f, 1.3f, 1f), material, sortingLayer, sortingOrder);
            SpriteRenderer center = AddVisual("Beacon Core", sprite != null ? sprite : GlowSprite,
                new Color(0.82f, 1f, 0.98f, 1f), Vector3.zero,
                new Vector3(0.44f, 0.44f, 1f), material, sortingLayer, sortingOrder + 1);
            core = center.transform;
            for (int i = 0; i < 4; i++)
            {
                float scale = 0.5f - i * 0.085f;
                trails[i] = AddVisual("Comet Trail " + i, GlowSprite, new Color(0.18f, 0.95f, 1f, 0.48f - i * 0.09f),
                    -(Vector3)direction * (0.25f + i * 0.18f), new Vector3(scale, scale, 1f), material, sortingLayer, sortingOrder - 1).transform;
            }
        }

        private void Update()
        {
            if (!IsFlying) return;
            if (owner == null)
            {
                Dissipate(EndReason.Cancelled);
                return;
            }
            if (!owner.ProjectileCanAdvance) return;

            // A moving wall may enter the beacon between swept checks. Arrival positions
            // after a portal are checked in the same way, before any further movement.
            if (!PositionIsClear(transform.position))
            {
                Dissipate(EndReason.Impact);
                return;
            }

            float timeRemaining = lifetime - Age;
            float distanceRemaining = range - TravelledDistance;
            float stepTime = Mathf.Min(Time.deltaTime, Mathf.Max(0f, timeRemaining));
            float step = Mathf.Min(speed * stepTime, Mathf.Max(0f, distanceRemaining));
            while (step > 0.00001f)
            {
                int count = Physics2D.CircleCast(transform.position, radius, direction, collisionFilter, hits, step);
                float nearest = float.PositiveInfinity;
                for (int i = 0; i < count; i++)
                    if (hits[i].collider != null && !owner.BelongsToPlayer(hits[i].collider))
                        nearest = Mathf.Min(nearest, hits[i].distance);
                // Fail closed if the cast buffer is full, even when a portal is nearby.
                if (count >= hits.Length)
                {
                    Dissipate(EndReason.Impact);
                    return;
                }

                bool hitsPortal = RelicPortal.FindProjectileEntry(transform.position, direction, step, radius,
                    justExitedPortal, portalCooldowns, Age, out RelicPortal portal, out float portalDistance);
                // A solid surface wins ties. A portal can never swallow a shot through a wall.
                if (hitsPortal && portalDistance + 0.0001f < nearest)
                {
                    if (PortalHops >= 8 || !portal.TryGetProjectileDestination(out Vector2 arrival, out Vector2 outgoing) ||
                        !PositionIsClear(arrival))
                    {
                        Dissipate(EndReason.Impact);
                        return;
                    }
                    TravelledDistance += portalDistance;
                    step -= portalDistance;
                    // Portals change location/direction but do not refresh range or lifetime.
                    transform.position = arrival;
                    direction = outgoing;
                    if (Camera.main != null)
                    {
                        previewCamera = Camera.main.GetComponent<RelicCamera>();
                        if (previewCamera != null) previewCamera.PreviewPortalShot(transform);
                    }
                    PortalHops++;
                    portalCooldowns[portal] = Age + 0.15f;
                    justExitedPortal = portal.destination;
                    portalCooldowns[justExitedPortal] = Age + 0.15f;
                    for (int i = 0; i < trails.Length; i++)
                        if (trails[i] != null) trails[i].localPosition = -(Vector3)direction * (0.25f + i * 0.18f);
                    continue;
                }
                if (nearest < float.PositiveInfinity)
                {
                    transform.position += (Vector3)direction * Mathf.Max(0f, nearest - 0.02f);
                    Dissipate(EndReason.Impact);
                    return;
                }
                transform.position += (Vector3)direction * step;
                TravelledDistance += step;
                step = 0f;
            }
            Age += stepTime;
            if (TravelledDistance >= range - 0.0001f)
            {
                Dissipate(EndReason.Range);
                return;
            }
            if (Age >= lifetime - 0.0001f)
            {
                Dissipate(EndReason.Lifetime);
                return;
            }
            float pulse = 1f + Mathf.Sin(Age * 18f) * 0.14f;
            if (glow != null)
            {
                glow.transform.localScale = Vector3.one * (1.3f * pulse);
                glow.color = new Color(0.1f, 1f, 0.92f, 0.35f + Remaining01 * 0.45f);
            }
            if (core != null) core.localScale = Vector3.one * (0.44f * pulse);
        }

        private bool PositionIsClear(Vector2 position)
        {
            // The Kit switches queriesStartInColliders off, so use an explicit overlap.
            int count = Physics2D.OverlapCircle(position, radius, collisionFilter, overlaps);
            if (count >= overlaps.Length) return false;
            for (int i = 0; i < count; i++)
                if (overlaps[i] != null && !owner.BelongsToPlayer(overlaps[i])) return false;
            return true;
        }

        public void Dissipate(EndReason reason = EndReason.Cancelled)
        {
            if (!IsFlying) return;
            IsFlying = false;
            ClearPreview();
            if (owner != null) owner.ProjectileEnded(this, reason);
            Destroy(gameObject);
        }

        private SpriteRenderer AddVisual(string objectName, Sprite sprite, Color color, Vector3 position,
            Vector3 scale, Material material, int sortingLayer, int sortingOrder)
        {
            var child = new GameObject(objectName);
            child.transform.SetParent(transform, false);
            child.transform.localPosition = position;
            child.transform.localScale = scale;
            var renderer = child.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            if (material != null) renderer.sharedMaterial = material;
            renderer.sortingLayerID = sortingLayer;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        private void OnDestroy()
        {
            ClearPreview();
            if (IsFlying && owner != null)
            {
                IsFlying = false;
                owner.ProjectileEnded(this, EndReason.Cancelled);
            }
        }

        private void ClearPreview()
        {
            if (previewCamera != null) previewCamera.ClearPortalPreview(transform);
            previewCamera = null;
        }
    }
}
