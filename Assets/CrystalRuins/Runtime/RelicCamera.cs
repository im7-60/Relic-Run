using UnityEngine;

namespace CrystalRuins
{
    [RequireComponent(typeof(Camera))]
    public class RelicCamera : MonoBehaviour
    {
        public Transform target;
        public float minX = 9f;
        public float maxX = 67f;
        public float height = 5f;
        public float orthographicSize = 7.5f;
        public float smoothTime = 0.2f;
        public float lookAhead = 1.8f;
        private Vector3 velocity;
        private Transform portalPreview;

        private void Start()
        {
            Camera camera = GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = orthographicSize;
            if (target != null) transform.position = DesiredPosition();
        }

        private Vector3 DesiredPosition()
        {
            float x = portalPreview != null ? portalPreview.position.x : target.position.x + lookAhead;
            return new Vector3(Mathf.Clamp(x, minX, maxX), height, -10f);
        }

        public void SnapToTarget()
        {
            portalPreview = null;
            velocity = Vector3.zero;
            if (target != null) transform.position = DesiredPosition();
        }

        public void PreviewPortalShot(Transform beacon)
        {
            portalPreview = beacon;
            velocity = Vector3.zero;
            if (target != null) transform.position = DesiredPosition();
        }

        public void ClearPortalPreview(Transform beacon)
        {
            if (portalPreview == beacon) SnapToTarget();
        }

        private void LateUpdate()
        {
            if (target == null) return;
            transform.position = Vector3.SmoothDamp(transform.position, DesiredPosition(), ref velocity, smoothTime);
        }
    }
}
