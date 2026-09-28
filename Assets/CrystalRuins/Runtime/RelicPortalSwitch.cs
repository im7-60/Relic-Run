using UnityEngine;
using UnityEngine.InputSystem;

namespace CrystalRuins
{
    /// <summary>A nearby player's E key changes the portal route; shots cannot operate it.</summary>
    public class RelicPortalSwitch : MonoBehaviour
    {
        public RelicGameManager manager;
        public RelicPortal controlledPortal;
        public RelicPortal[] destinations;
        public string[] routeNames;
        public TextMesh routeLabel;
        public float interactionDistance = 2.2f;
        [SerializeField] private int currentRouteIndex;
        public int CurrentRouteIndex => currentRouteIndex;
        private bool wasNear;

        private void Start() { SelectRoute(currentRouteIndex); }

        private void Update()
        {
            if (manager == null) manager = RelicGameManager.Instance;
            if (manager == null || !manager.IsPlaying) return;
            bool near = IsPlayerInRange();
            if (near && !wasNear) ShowRoute();
            wasNear = near;
            if (near && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame) CycleRoute();
        }

        public bool CycleRoute()
        {
            if (manager == null) manager = RelicGameManager.Instance;
            if (manager == null || !manager.IsPlaying || !IsPlayerInRange() || destinations == null || destinations.Length == 0)
                return false;
            bool selected = SelectRoute((currentRouteIndex + 1) % destinations.Length);
            if (selected) ShowRoute();
            return selected;
        }

        public bool SelectRoute(int index)
        {
            if (controlledPortal == null || destinations == null || destinations.Length == 0) return false;
            index = Mathf.Clamp(index, 0, destinations.Length - 1);
            if (destinations[index] == null) return false;
            currentRouteIndex = index;
            controlledPortal.destination = destinations[index];
            if (routeLabel != null)
                routeLabel.text = "E  CHANGE ROUTE\n" + (index + 1) + "/" + destinations.Length + "  " + RouteName();
            return true;
        }

        private bool IsPlayerInRange()
        {
            if (manager == null || manager.player == null) return false;
            CapsuleCollider2D body = manager.player.GetComponent<CapsuleCollider2D>();
            if (!manager.IsPlayer(body) || !body.enabled) return false;
            return Vector2.Distance(transform.position, body.ClosestPoint(transform.position)) <= Mathf.Max(0.1f, interactionDistance);
        }

        private string RouteName()
        {
            if (destinations == null || currentRouteIndex < 0 || currentRouteIndex >= destinations.Length ||
                destinations[currentRouteIndex] == null) return "UNLINKED";
            if (routeNames != null && currentRouteIndex < routeNames.Length && !string.IsNullOrEmpty(routeNames[currentRouteIndex]))
                return routeNames[currentRouteIndex];
            RelicPortal target = destinations[currentRouteIndex];
            return !string.IsNullOrEmpty(target.portalName) ? target.portalName : target.name;
        }

        private void ShowRoute()
        {
            if (manager != null && controlledPortal != null && destinations != null && destinations.Length > 0)
                manager.ShowHint("ROUTE: " + RouteName() + "  /  Press E near the console to change route.", 4f);
        }
    }
}
