using TMPro;
using UnityEngine;

namespace Mossvale.Farming
{
    /// <summary>Small world-space label (wooden sign with icon + text) that always faces the camera and bobs gently.</summary>
    public class WorldBadge : MonoBehaviour
    {
        [SerializeField] TMP_Text label;
        [SerializeField] float bob = 0.06f;

        Vector3 basePos;
        Camera cam;

        void Awake() => basePos = transform.localPosition;

        public void SetText(string text) { if (label) label.text = text; }

        void LateUpdate()
        {
            if (cam == null) cam = Camera.main;
            if (cam == null) return;
            transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position, Vector3.up);
            transform.localPosition = basePos + Vector3.up * (Mathf.Sin(Time.time * 1.6f + basePos.x) * bob);
        }
    }
}
