using UnityEngine;

namespace CrystalRuins
{
    [SelectionBase]
    [RequireComponent(typeof(Collider2D))]
    public class RelicPickup : MonoBehaviour
    {
        public RelicGameManager manager;
        [Tooltip("Unique within this scene; retained when revisiting the level.")]
        public string pickupId;
        public string StableId
        {
            get
            {
                if (!string.IsNullOrEmpty(pickupId)) return pickupId;
                // Existing authored pickups still have a deterministic ID until stamped by the builder.
                string path = "";
                for (Transform node = transform; node != null; node = node.parent)
                    path = "/" + node.name + "[" + node.GetSiblingIndex() + "]" + path;
                return path;
            }
        }
        [Tooltip("A sprite child; only the visual bobs, never the trigger.")]
        public Transform visual;
        public float bobHeight = 0.18f;
        public float bobSpeed = 2.8f;
        private Vector3 visualOrigin;
        private Vector3 visualScale;
        private float phase;
        private bool collected;

        private void Awake()
        {
            Collider2D pickupCollider = GetComponent<Collider2D>();
            pickupCollider.isTrigger = true;
            if (visual == null && transform.childCount > 0) visual = transform.GetChild(0);
            if (visual == transform) visual = null;
            if (visual != null)
            {
                visualOrigin = visual.localPosition;
                visualScale = visual.localScale;
                // Honor the authored sprite position without moving the trigger during bobbing.
                pickupCollider.offset = (Vector2)transform.InverseTransformPoint(visual.position);
            }
            phase = transform.position.x * 0.7f;
            if (RelicCampaignState.HasCollected(gameObject.scene.name, StableId))
            {
                collected = true;
                gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            if (visual != null)
            {
                visual.localPosition = visualOrigin + Vector3.up * Mathf.Sin(Time.time * bobSpeed + phase) * bobHeight;
                float pulse = 1f + Mathf.Sin(Time.time * 2f + phase) * 0.035f;
                visual.localScale = new Vector3(visualScale.x * pulse, visualScale.y * pulse, visualScale.z);
            }
        }

        private void OnTriggerEnter2D(Collider2D other) { TryCollect(other); }
        private void OnTriggerStay2D(Collider2D other) { TryCollect(other); }

        private void TryCollect(Collider2D other)
        {
            if (collected) return;
            if (manager == null) manager = RelicGameManager.Instance;
            if (manager == null || !manager.IsPlayer(other) || !manager.AddRelic(StableId)) return;
            collected = true;
            gameObject.SetActive(false);
        }
    }
}
