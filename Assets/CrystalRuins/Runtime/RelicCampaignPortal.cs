using UnityEngine;

namespace CrystalRuins
{
    /// <summary>A return gate between levels; only Ellen's actual body can use it.</summary>
    [SelectionBase]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider2D))]
    public class RelicCampaignPortal : MonoBehaviour
    {
        public RelicGameManager manager;
        public string destinationScene;
        public string arrivalId = "end";
        private float nextAttempt;

        private void Awake() { GetComponent<BoxCollider2D>().isTrigger = true; }
        private void OnTriggerEnter2D(Collider2D other) { TryTravel(other); }
        private void OnTriggerStay2D(Collider2D other) { TryTravel(other); }

        public bool TryTravel(Collider2D other)
        {
            if (manager == null) manager = RelicGameManager.Instance;
            if (!isActiveAndEnabled || manager == null || !manager.IsPlaying ||
                !manager.IsPlayer(other) || Time.unscaledTime < nextAttempt) return false;
            nextAttempt = Time.unscaledTime + 1f;
            return manager.TravelToScene(destinationScene, arrivalId);
        }
    }
}
