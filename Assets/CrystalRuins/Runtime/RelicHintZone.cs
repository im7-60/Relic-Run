using UnityEngine;

namespace CrystalRuins
{
    [RequireComponent(typeof(Collider2D))]
    public class RelicHintZone : MonoBehaviour
    {
        public RelicGameManager manager;
        [TextArea] public string message = "Keep exploring the ruins.";
        public float duration = 5f;
        public bool showOnce = true;
        private bool shown;
        private void Awake() { GetComponent<Collider2D>().isTrigger = true; }
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (shown && showOnce) return;
            if (manager == null) manager = RelicGameManager.Instance;
            if (manager == null || !manager.IsPlaying || !manager.IsPlayer(other)) return;
            shown = true;
            manager.ShowHint(message, duration);
        }
    }
}
