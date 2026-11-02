using UnityEngine;
namespace AeroMorphET
{
    public sealed class CameraFollow : MonoBehaviour
    {
        public Transform target;
        public Vector3 offset = new(0, 4.8f, -8f);
        public float smooth = 7f;
        private void Awake() => ApplyPosition(true);
        private void LateUpdate() => ApplyPosition(false);
        private void ApplyPosition(bool snap)
        {
            if (target == null) return;
            Vector3 desired = target.position + offset;
            transform.position = snap ? desired : Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-smooth * Time.deltaTime));
            transform.LookAt(target.position + Vector3.up * 1.2f + Vector3.forward * 5f);
        }
    }
}
