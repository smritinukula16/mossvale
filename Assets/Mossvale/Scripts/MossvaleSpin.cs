using UnityEngine;

namespace Mossvale
{
    /// <summary>
    /// Ambient spin for the windmill sails. The Mossvale pack places the
    /// Windmill_Blades pivot at the hub, so the sails turn around local Z.
    /// </summary>
    public class MossvaleSpin : MonoBehaviour
    {
        public Vector3 localAxis = Vector3.forward;
        public float degreesPerSecond = 18f;

        void Update()
        {
            transform.Rotate(localAxis, degreesPerSecond * Time.deltaTime, Space.Self);
        }
    }
}
