using UnityEngine;

namespace Mossvale.Farming
{
    /// <summary>Gentle float + spin (the Heart crystal).</summary>
    public class MossvaleFloat : MonoBehaviour
    {
        public float amplitude = 0.15f, speed = 1.2f, spin = 35f;
        Vector3 basePos;
        void Awake() => basePos = transform.localPosition;
        void Update()
        {
            transform.localPosition = basePos + Vector3.up * (Mathf.Sin(Time.time * speed) * amplitude);
            transform.Rotate(Vector3.up, spin * Time.deltaTime, Space.World);
        }
    }
}
