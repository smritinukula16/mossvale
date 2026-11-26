using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Mossvale.Farming
{
    /// <summary>Screen-space text that floats up from a world position ("+3 Carrot", "Needs water"...).</summary>
    public class FloatingFeedbackUI : MonoBehaviour
    {
        [SerializeField] TMP_Text template;
        [SerializeField] float lifetime = 1.6f;
        [SerializeField] float risePixels = 70f;
        [SerializeField] float worldLift = 1.3f;

        class Item { public TMP_Text text; public Vector3 world; public float age; }
        readonly List<Item> active = new List<Item>();
        readonly Stack<TMP_Text> pool = new Stack<TMP_Text>();
        Camera cam;
        RectTransform rect;

        void Awake()
        {
            rect = (RectTransform)transform;
            if (template) template.gameObject.SetActive(false);
        }

        void OnEnable() => FarmFeedback.Raised += Show;
        void OnDisable() => FarmFeedback.Raised -= Show;

        public void Show(string message, Vector3 world, FeedbackKind kind)
        {
            if (template == null || string.IsNullOrEmpty(message)) return;
            var t = pool.Count > 0 ? pool.Pop() : Instantiate(template, transform);
            t.gameObject.SetActive(true);
            t.text = message;
            t.color = FarmFeedback.ColorFor(kind);
            t.fontSize = kind == FeedbackKind.Harvest ? template.fontSize * 1.25f : template.fontSize;
            // Stack messages raised at the same spot so they don't overlap.
            int sameSpot = active.FindAll(a => (a.world - world).sqrMagnitude < 0.25f && a.age < 0.5f).Count;
            active.Add(new Item { text = t, world = world + Vector3.up * (worldLift + sameSpot * 0.45f), age = 0f });
            Position(active[active.Count - 1]);
        }

        void LateUpdate()
        {
            if (cam == null) cam = Camera.main;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var it = active[i];
                it.age += Time.unscaledDeltaTime;
                if (it.age >= lifetime)
                {
                    it.text.gameObject.SetActive(false);
                    pool.Push(it.text);
                    active.RemoveAt(i);
                    continue;
                }
                Position(it);
                var c = it.text.color;
                c.a = 1f - Mathf.Clamp01((it.age - lifetime * 0.6f) / (lifetime * 0.4f));
                it.text.color = c;
            }
        }

        void Position(Item it)
        {
            if (cam == null) cam = Camera.main;
            if (cam == null) return;
            var sp = cam.WorldToScreenPoint(it.world);
            bool visible = sp.z > 0f;
            it.text.enabled = visible;
            if (!visible) return;
            sp.y += it.age / lifetime * risePixels;
            var canvas = GetComponentInParent<Canvas>();
            var uiCam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, sp, uiCam, out var local))
                it.text.rectTransform.anchoredPosition = local;
        }

        /// <summary>Number of messages currently floating (tests).</summary>
        public int ActiveCount => active.Count;
    }
}
