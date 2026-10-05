using UnityEngine;
using UnityEngine.InputSystem;

namespace ColonyGame.Presentation
{
    [RequireComponent(typeof(Camera))]
    public sealed class PrototypeCamera : MonoBehaviour
    {
        public System.Func<Vector2, bool> IsPointerOverUI { get; set; }
        private Camera view;
        private Vector3 focus = new Vector3(10, 0, 10);
        private readonly Plane ground = new Plane(Vector3.up, Vector3.zero);
        private Rect inspectionBounds = Rect.MinMaxRect(-6, -6, 26, 26);
        public void SetInspectionBounds(float minX, float minZ, float maxX, float maxZ)
        { inspectionBounds = Rect.MinMaxRect(minX, minZ, maxX, maxZ); }
        private void Awake() { view = GetComponent<Camera>(); }
        private void Update()
        {
            var keys = Keyboard.current;
            var direction = Vector3.zero;
            if (keys != null)
            {
                if (keys.wKey.isPressed || keys.upArrowKey.isPressed) direction.z += 1;
                if (keys.sKey.isPressed || keys.downArrowKey.isPressed) direction.z -= 1;
                if (keys.aKey.isPressed || keys.leftArrowKey.isPressed) direction.x -= 1;
                if (keys.dKey.isPressed || keys.rightArrowKey.isPressed) direction.x += 1;
            }
            focus += direction.normalized * (view.orthographicSize * 1.2f * Time.unscaledDeltaTime);
            var mouse = Mouse.current;
            if (mouse != null && !(IsPointerOverUI?.Invoke(mouse.position.ReadValue()) ?? false))
            {
                view.orthographicSize = Mathf.Clamp(view.orthographicSize * Mathf.Exp(-mouse.scroll.ReadValue().y * 0.002f), 5, 28);
                if (mouse.middleButton.isPressed)
                {
                    var current = mouse.position.ReadValue();
                    var previous = current - mouse.delta.ReadValue();
                    if (GroundPoint(previous, out var before) && GroundPoint(current, out var after)) focus += before - after;
                }
            }
            // Presentation bounds keep the finite prototype in reach; this is not player territory enforcement.
            focus.x = Mathf.Clamp(focus.x, inspectionBounds.xMin, inspectionBounds.xMax);
            focus.z = Mathf.Clamp(focus.z, inspectionBounds.yMin, inspectionBounds.yMax);
            transform.position = focus - transform.forward * 45;
        }
        private bool GroundPoint(Vector2 screen, out Vector3 point)
        {
            var ray = view.ScreenPointToRay(screen);
            if (ground.Raycast(ray, out var distance)) { point = ray.GetPoint(distance); return true; }
            point = default;
            return false;
        }
    }
}
