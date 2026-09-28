using UnityEngine;

namespace CrystalRuins
{
    /// <summary>A safe player-feet landing point for travel from an adjacent level.</summary>
    [DisallowMultipleComponent]
    public class RelicCampaignArrival : MonoBehaviour
    {
        public string arrivalId = "start";
        [Tooltip("The player root/feet position, clear of portal triggers and solid terrain.")]
        public Transform feet;

        private void OnDrawGizmosSelected()
        {
            Vector3 position = feet != null ? feet.position : transform.position;
            Gizmos.color = new Color(.65f, .55f, 1f);
            Gizmos.DrawWireCube(position + Vector3.up, new Vector3(.6f, 2f, .1f));
        }
    }
}
