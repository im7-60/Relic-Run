using System;
using System.Collections;
using Gamekit2D;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CrystalRuins
{
    /// <summary>Switches Ellen's stock gun between normal shots and one travelling teleport beacon.</summary>
    [DefaultExecutionOrder(-50)]
    [RequireComponent(typeof(PlayerCharacter), typeof(CapsuleCollider2D))]
    public class RelicTeleportAbility : MonoBehaviour
    {
        public RelicGameManager manager;
        public PlayerCharacter player;
        public Sprite projectileSprite;
        public AudioClip fireSound;
        public AudioClip teleportSound;
        public float projectileSpeed = 14f;
        public float maximumDistance = 18f;
        public float projectileLifetime = 1.5f;
        public float shotCooldown = 0.3f;
        public float projectileRadius = 0.13f;
        [Tooltip("Zero uses Ellen's grounded layers for projectile collisions.")]
        public LayerMask projectileBlockingLayers;
        [Tooltip("Zero checks every solid collider when validating Ellen's destination.")]
        public LayerMask destinationBlockingLayers;
        [Tooltip("Extra clearance around Ellen's capsule at a teleport destination.")]
        public float destinationClearance = 0.035f;

        public bool IsTeleportMode { get; private set; }
        public RelicTeleportProjectile ActiveProjectile { get; private set; }
        public bool HasActiveProjectile => ActiveProjectile != null && ActiveProjectile.IsFlying;
        public float ProjectileRemaining01 => HasActiveProjectile ? ActiveProjectile.Remaining01 : 0f;
        public float ProjectileRemainingSeconds => !HasActiveProjectile ? 0f : Mathf.Max(0f, Mathf.Min(
            Mathf.Max(0.1f, projectileLifetime) - ActiveProjectile.Age,
            (Mathf.Max(0.5f, maximumDistance) - ActiveProjectile.TravelledDistance) / Mathf.Max(1f, projectileSpeed)));
        public string LastResult { get; private set; } = "Normal shots equipped.";
        public string StatusText => !IsTeleportMode ? "NORMAL  |  O SHOOT" :
            HasActiveProjectile ? "TELEPORT  |  L WARP  " + ProjectileRemainingSeconds.ToString("0.0") + "s" : "TELEPORT  |  O FIRE";
        public event Action ModeChanged;
        public event Action ProjectileChanged;
        public event Action<string> Feedback;

        private CapsuleCollider2D body;
        private Vector2 standingSize;
        private Vector2 standingOffset;
        private CapsuleDirection2D standingDirection;
        private CharacterController2D controller;
        private AudioSource audioSource;
        private float nextShotTime;
        private bool normalAttackWasEnabled;
        private bool suppressingNormalAttack;
        private readonly Collider2D[] overlaps = new Collider2D[32];
        private readonly RaycastHit2D[] castHits = new RaycastHit2D[16];
        private static readonly Color BeaconColor = new Color(0.25f, 0.97f, 0.92f, 1f);

        private void Awake()
        {
            if (player == null) player = GetComponent<PlayerCharacter>();
            body = GetComponent<CapsuleCollider2D>();
            standingSize = body.size;
            standingOffset = body.offset;
            standingDirection = body.direction;
            controller = GetComponent<CharacterController2D>();
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            audioSource.volume = 0.7f;
        }

        private void Update()
        {
            if (manager == null) manager = RelicGameManager.Instance;
            if (manager == null) return;
            if (manager.State == RelicGameManager.GameState.Won || manager.State == RelicGameManager.GameState.Lost)
            {
                ClearProjectile();
                return;
            }
            if (!manager.IsPlaying) return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.bKey.wasPressedThisFrame) ToggleMode();
            if (IsTeleportMode)
            {
                SuppressNormalAttack();
                if (keyboard != null && keyboard.oKey.wasPressedThisFrame) TryFire();
                if (keyboard != null && keyboard.lKey.wasPressedThisFrame) TryTeleport();
            }
        }

        public bool ToggleMode()
        {
            if (!CanAct()) return false;
            return SetTeleportMode(!IsTeleportMode);
        }

        public bool SetTeleportMode(bool teleportMode)
        {
            if (!CanAct()) return false;
            if (IsTeleportMode == teleportMode) return true;
            IsTeleportMode = teleportMode;
            if (teleportMode)
            {
                var input = Gamekit2D.PlayerInput.Instance;
                normalAttackWasEnabled = input != null && input.RangedAttack.Enabled;
                suppressingNormalAttack = true;
                SuppressNormalAttack(true);
                Report("TELEPORT SHOT  /  O fires a beacon. Press L before it fades.", 4f);
            }
            else
            {
                ClearProjectile();
                RestoreNormalAttack();
                Report("NORMAL SHOT  /  O fires Ellen's blaster. B switches modes.", 3f);
            }
            ModeChanged?.Invoke();
            return true;
        }

        public bool TryFire()
        {
            if (!CanAct() || !IsTeleportMode || Time.time < nextShotTime) return false;
            SuppressNormalAttack();
            Vector2 direction = player.GetFacing() < 0f ? Vector2.left : Vector2.right;
            Transform muzzle = direction.x < 0f ? player.facingLeftBulletSpawnPoint : player.facingRightBulletSpawnPoint;
            Vector2 origin = muzzle != null ? (Vector2)muzzle.position : (Vector2)body.bounds.center + direction * 0.8f;
            Vector2 shoulder = new Vector2(body.bounds.center.x, origin.y);
            Vector2 offset = origin - shoulder;
            ContactFilter2D filter = ProjectileFilter();
            int count = Physics2D.CircleCast(shoulder, Mathf.Max(0.02f, projectileRadius), offset.normalized,
                filter, castHits, offset.magnitude);
            for (int i = 0; i < count; i++)
            {
                Collider2D hit = castHits[i].collider;
                if (hit != null && !BelongsToPlayer(hit))
                {
                    Report("Move away from the wall before firing a teleport shot.", 2f);
                    return false;
                }
            }

            ClearProjectile(RelicTeleportProjectile.EndReason.Replaced);
            var beaconObject = new GameObject("Teleport Beacon");
            beaconObject.transform.position = origin;
            ActiveProjectile = beaconObject.AddComponent<RelicTeleportProjectile>();
            ActiveProjectile.Initialize(this, origin, direction, Mathf.Max(1f, projectileSpeed),
                Mathf.Max(0.5f, maximumDistance), Mathf.Max(0.1f, projectileLifetime),
                Mathf.Max(0.02f, projectileRadius), filter, projectileSprite,
                player.spriteRenderer != null ? player.spriteRenderer.sharedMaterial : null,
                player.spriteRenderer != null ? player.spriteRenderer.sortingLayerID : 0,
                player.spriteRenderer != null ? player.spriteRenderer.sortingOrder + 20 : 20);
            nextShotTime = Time.time + Mathf.Max(0.05f, shotCooldown);
            // Reuse Ellen's stock standing/running, crouching, and airborne gun animations.
            // The ranged input stays disabled, so the animation cannot spawn a normal bullet.
            player.PlayRangedAttackAnimation();
            if (fireSound != null) audioSource.PlayOneShot(fireSound, 0.6f);
            else if (player.rangedAttackAudioPlayer != null) player.rangedAttackAudioPlayer.PlayRandomSound();
            Report("BEACON IN FLIGHT  /  L to teleport", 1.1f);
            ProjectileChanged?.Invoke();
            return true;
        }

        public bool TryTeleport()
        {
            if (!CanAct() || !IsTeleportMode) return false;
            if (!HasActiveProjectile)
            {
                Report("No beacon in flight. Press O, then L before it fades.", 2.5f);
                return false;
            }
            // Align Ellen's capsule center, rather than her feet, with the beacon's current position.
            Vector2 destination = (Vector2)ActiveProjectile.transform.position - BodyCenterOffset();
            if (!IsDestinationClear(destination))
            {
                Report("That spot is too tight. Wait until the beacon reaches open space.", 1.7f);
                return false;
            }

            Vector2 previousCenter = body.bounds.center;
            player.SetMoveVector(Vector2.zero);
            Rigidbody2D rigidbody = controller.Rigidbody2D;
            if (rigidbody != null)
            {
                rigidbody.linearVelocity = Vector2.zero;
                rigidbody.angularVelocity = 0f;
            }
            controller.Teleport(destination);
            // Teleport is immediate; avoid sweeping Ellen's body through the intervening world.
            if (rigidbody != null) rigidbody.position = destination;
            player.transform.position = new Vector3(destination.x, destination.y, player.transform.position.z);
            Physics2D.SyncTransforms();
            controller.CheckCapsuleEndCollisions();
            controller.CheckCapsuleEndCollisions(false);
            Animator animator = player.GetComponent<Animator>();
            if (animator != null)
            {
                animator.SetBool("Grounded", controller.IsGrounded);
                animator.SetFloat("HorizontalSpeed", 0f);
                animator.SetFloat("VerticalSpeed", 0f);
            }
            RelicCamera follow = Camera.main != null ? Camera.main.GetComponent<RelicCamera>() : null;
            if (follow != null) follow.SnapToTarget();
            Vector2 arrivalCenter = body.bounds.center;
            ClearProjectile(RelicTeleportProjectile.EndReason.Teleported);
            StartCoroutine(TeleportPulse(previousCenter));
            StartCoroutine(TeleportPulse(arrivalCenter));
            if (teleportSound != null) audioSource.PlayOneShot(teleportSound, 0.8f);
            Report("TELEPORT COMPLETE", 1.2f);
            return true;
        }

        /// <summary>Checks both the animated and standing capsules. Triggers do not block teleportation.</summary>
        public bool IsDestinationClear(Vector2 playerRootPosition)
        {
            if (body == null || !body.enabled) return false;
            return CapsuleFits(playerRootPosition, body.size, body.offset, body.direction) &&
                CapsuleFits(playerRootPosition, standingSize, standingOffset, standingDirection);
        }

        private bool CapsuleFits(Vector2 playerRootPosition, Vector2 capsuleSize, Vector2 capsuleOffset, CapsuleDirection2D capsuleDirection)
        {
            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(destinationBlockingLayers.value == 0 ? Physics2D.AllLayers : destinationBlockingLayers.value);
            filter.useTriggers = false;
            Vector3 scale = body.transform.lossyScale;
            Vector2 size = new Vector2(Mathf.Abs(capsuleSize.x * scale.x), Mathf.Abs(capsuleSize.y * scale.y));
            size += Vector2.one * Mathf.Max(0f, destinationClearance) * 2f;
            Vector2 centerOffset = (Vector2)body.transform.TransformPoint(capsuleOffset) - (Vector2)player.transform.position;
            int count = Physics2D.OverlapCapsule(playerRootPosition + centerOffset, size, capsuleDirection,
                body.transform.eulerAngles.z, filter, overlaps);
            // Fail closed if an unusually crowded destination fills the query buffer.
            if (count >= overlaps.Length) return false;
            for (int i = 0; i < count; i++)
                if (overlaps[i] != null && !BelongsToPlayer(overlaps[i])) return false;
            return true;
        }

        public void ClearProjectile()
        {
            ClearProjectile(RelicTeleportProjectile.EndReason.Cancelled);
        }

        private void ClearProjectile(RelicTeleportProjectile.EndReason reason)
        {
            if (ActiveProjectile != null) ActiveProjectile.Dissipate(reason);
        }

        internal void ProjectileEnded(RelicTeleportProjectile projectile, RelicTeleportProjectile.EndReason reason)
        {
            if (ActiveProjectile != projectile) return;
            ActiveProjectile = null;
            if (manager != null && manager.IsPlaying &&
                (reason == RelicTeleportProjectile.EndReason.Impact || reason == RelicTeleportProjectile.EndReason.Range || reason == RelicTeleportProjectile.EndReason.Lifetime))
                Report(reason == RelicTeleportProjectile.EndReason.Impact ? "Beacon hit a wall. Press O to fire again." : "Beacon faded. Press O to fire again.", 1.8f);
            ProjectileChanged?.Invoke();
        }

        internal bool BelongsToPlayer(Collider2D collider)
        {
            // Do not use ancestor PlayerCharacter tests: Kit's pooled bullets are children of Ellen.
            return collider == body || (collider != null && controller != null &&
                controller.Rigidbody2D != null &&
                collider.attachedRigidbody == controller.Rigidbody2D);
        }

        internal bool ProjectileCanAdvance => manager != null && manager.IsPlaying;

        private Vector2 BodyCenterOffset()
        {
            return (Vector2)body.transform.TransformPoint(body.offset) - (Vector2)player.transform.position;
        }

        private ContactFilter2D ProjectileFilter()
        {
            var filter = new ContactFilter2D();
            int mask = projectileBlockingLayers.value;
            if (mask == 0 && controller != null) mask = controller.groundedLayerMask.value;
            filter.SetLayerMask(mask == 0 ? Physics2D.DefaultRaycastLayers : mask);
            filter.useTriggers = false;
            return filter;
        }

        private bool CanAct()
        {
            if (manager == null) manager = RelicGameManager.Instance;
            return isActiveAndEnabled && manager != null && manager.IsPlaying && player != null &&
                controller != null && Gamekit2D.PlayerInput.Instance != null && Gamekit2D.PlayerInput.Instance.HaveControl;
        }

        private void SuppressNormalAttack(bool resetGunPose = false)
        {
            var input = Gamekit2D.PlayerInput.Instance;
            if (input == null || player == null) return;
            input.DisableRangedAttacking();
            input.RangedAttack.Get(false); // Clears the last Held value before the kit reads it.
            if (resetGunPose)
            {
                // Stop an existing normal burst once when switching modes. Clearing this
                // every frame would also erase the teleport shot's shared gun animation.
                player.ForceNotHoldingGun();
                player.CheckAndFireGun();
            }
        }

        private void RestoreNormalAttack()
        {
            if (!suppressingNormalAttack) return;
            suppressingNormalAttack = false;
            if (Gamekit2D.PlayerInput.Instance != null && normalAttackWasEnabled)
                Gamekit2D.PlayerInput.Instance.EnableRangedAttacking();
        }

        private void Report(string message, float duration)
        {
            LastResult = message;
            if (manager != null && manager.IsPlaying) manager.ShowHint(message, duration);
            Feedback?.Invoke(message);
        }

        private IEnumerator TeleportPulse(Vector2 position)
        {
            var pulseObject = new GameObject("Teleport Flash");
            pulseObject.transform.position = position;
            SpriteRenderer renderer = pulseObject.AddComponent<SpriteRenderer>();
            renderer.sprite = RelicTeleportProjectile.GlowSprite;
            renderer.color = BeaconColor;
            if (player.spriteRenderer != null)
            {
                renderer.sharedMaterial = player.spriteRenderer.sharedMaterial;
                renderer.sortingLayerID = player.spriteRenderer.sortingLayerID;
                renderer.sortingOrder = player.spriteRenderer.sortingOrder + 21;
            }
            float elapsed = 0f;
            while (elapsed < 0.25f && pulseObject != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / 0.25f);
                pulseObject.transform.localScale = Vector3.one * Mathf.Lerp(0.5f, 2.4f, t);
                renderer.color = new Color(BeaconColor.r, BeaconColor.g, BeaconColor.b, (1f - t) * 0.6f);
                yield return null;
            }
            if (pulseObject != null) Destroy(pulseObject);
        }

        private void OnDisable()
        {
            ClearProjectile();
            RestoreNormalAttack();
            IsTeleportMode = false;
        }
    }
}
