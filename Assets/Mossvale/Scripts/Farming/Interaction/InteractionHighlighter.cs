using UnityEngine;

namespace Mossvale.Farming
{
    /// <summary>Draws a pulsing outline around the current interaction target (green = can act, amber = can't, white = too far).</summary>
    [RequireComponent(typeof(LineRenderer))]
    public class InteractionHighlighter : MonoBehaviour
    {
        [SerializeField] PlayerInteractor interactor;
        [SerializeField] Color canColor = new Color(0.55f, 1f, 0.45f, 0.95f);
        [SerializeField] Color cannotColor = new Color(1f, 0.72f, 0.3f, 0.95f);
        [SerializeField] Color outOfRangeColor = new Color(1f, 1f, 1f, 0.6f);
        [SerializeField] float padding = 0.06f;

        LineRenderer line;
        readonly Vector3[] corners = new Vector3[4];

        void Awake()
        {
            line = GetComponent<LineRenderer>();
            line.loop = true;
            line.positionCount = 4;
            line.useWorldSpace = true;
        }

        void LateUpdate()
        {
            var t = interactor != null ? interactor.Current : null;
            if (t == null) { line.enabled = false; return; }
            var b = t.WorldBounds;
            float y = t.HighlightHeight;
            float x0 = b.min.x - padding, x1 = b.max.x + padding, z0 = b.min.z - padding, z1 = b.max.z + padding;
            corners[0] = new Vector3(x0, y, z0);
            corners[1] = new Vector3(x0, y, z1);
            corners[2] = new Vector3(x1, y, z1);
            corners[3] = new Vector3(x1, y, z0);
            line.SetPositions(corners);
            var c = !interactor.CurrentInRange ? outOfRangeColor : interactor.CurrentPrompt.canPerform ? canColor : cannotColor;
            c.a *= 0.75f + 0.25f * Mathf.Sin(Time.time * 6f);
            line.startColor = line.endColor = c;
            line.widthMultiplier = 0.06f;
            line.enabled = true;
        }
    }
}
