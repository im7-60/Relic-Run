using UnityEngine;

namespace CrystalRuins
{
    [RequireComponent(typeof(Collider2D))]
    public class RelicExit : MonoBehaviour
    {
        public RelicGameManager manager;
        public SpriteRenderer glow;
        private void Awake() { GetComponent<Collider2D>().isTrigger = true; }
        private void Update()
        {
            if (manager == null) manager = RelicGameManager.Instance;
            if (glow != null && manager != null)
            {
                Color color = manager.ExitUnlocked ? new Color(1f, 0.81f, 0.35f) : new Color(0.34f, 0.68f, 0.64f);
                color.a = 0.68f + Mathf.Sin(Time.time * 2.2f) * 0.16f;
                glow.color = color;
            }
        }
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (manager == null) manager = RelicGameManager.Instance;
            if (manager == null || !manager.IsPlayer(other) || !manager.IsPlaying) return;
            if (manager.ExitUnlocked) manager.Win();
            else manager.ShowHint("The exit needs " + (manager.requiredRelics - manager.CollectedRelics) + " more relics. Explore the ruins!", 4f);
        }
        private void OnTriggerStay2D(Collider2D other)
        {
            if (manager != null && manager.IsPlaying && manager.ExitUnlocked && manager.IsPlayer(other)) manager.Win();
        }
    }
}
