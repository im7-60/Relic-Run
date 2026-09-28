using UnityEngine;

namespace CrystalRuins
{
    [RequireComponent(typeof(Collider2D))]
    public class RelicFallZone : MonoBehaviour
    {
        public RelicGameManager manager;
        private void Awake() { GetComponent<Collider2D>().isTrigger = true; }
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (manager == null) manager = RelicGameManager.Instance;
            if (manager != null && manager.IsPlayer(other)) manager.Lose();
        }
    }
}
