using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Mossvale.Farming
{
    /// <summary>Small runtime helpers to build styled uGUI with explicit, predictable layout.</summary>
    public static class UIBuilder
    {
        public static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.layer = parent != null ? parent.gameObject.layer : 5;
            return (RectTransform)go.transform;
        }

        /// <summary>Stretch inside the parent with insets.</summary>
        public static RectTransform Fill(Transform parent, string name, float left = 0, float top = 0, float right = 0, float bottom = 0)
        {
            var rt = Rect(parent, name);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom); rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        /// <summary>Fixed-size rect positioned from the parent's top-left corner (x right, y down).</summary>
        public static RectTransform Cell(Transform parent, string name, float x, float y, float w, float h)
        {
            var rt = Rect(parent, name);
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -y); rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        /// <summary>Fixed-size rect anchored at a point of the parent.</summary>
        public static RectTransform At(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var rt = Rect(parent, name);
            rt.anchorMin = rt.anchorMax = anchor; rt.pivot = pivot;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            return rt;
        }

        public static UnityEngine.UI.Image Image(RectTransform rt, Sprite sprite, Color color, bool raycast = false)
        {
            var img = rt.gameObject.AddComponent<UnityEngine.UI.Image>();
            img.sprite = sprite;
            img.type = sprite != null && sprite.border.sqrMagnitude > 0 ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            img.preserveAspect = img.type == UnityEngine.UI.Image.Type.Simple && sprite != null;
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        public static TMP_Text Text(RectTransform rt, UIStyle st, string text, float size, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft, bool bold = false)
        {
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (st != null && st.font != null) t.font = st.font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            t.raycastTarget = false;
            t.richText = true;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Ellipsis;
            return t;
        }

        /// <summary>Text filling a new child rect.</summary>
        public static TMP_Text Label(Transform parent, UIStyle st, string text, float size, Color color, float x, float y, float w, float h,
            TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft, bool bold = false) =>
            Text(Cell(parent, "Label", x, y, w, h), st, text, size, color, align, bold);

        public static UnityEngine.UI.Image Icon(Transform parent, Sprite sprite, float x, float y, float size, Color? tint = null)
        {
            var img = Image(Cell(parent, "Icon", x, y, size, size), sprite, tint ?? Color.white);
            img.preserveAspect = true;
            img.type = UnityEngine.UI.Image.Type.Simple;
            return img;
        }

        /// <summary>Rounded, tinted button with hover/press animation.</summary>
        public static UnityEngine.UI.Button Button(RectTransform rt, UIStyle st, string label, Color color, Action onClick,
            bool interactable = true, float fontSize = 22f, Sprite icon = null)
        {
            var img = Image(rt, st.button, interactable ? color : st.disabled, true);
            var b = rt.gameObject.AddComponent<UnityEngine.UI.Button>();
            b.targetGraphic = img;
            var cb = b.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.08f, 1.08f, 1.08f);
            cb.pressedColor = new Color(0.85f, 0.85f, 0.85f);
            cb.disabledColor = new Color(1f, 1f, 1f, 0.85f);
            cb.colorMultiplier = 1.1f;
            b.colors = cb;
            b.interactable = interactable;
            if (onClick != null) b.onClick.AddListener(() => onClick());
            rt.gameObject.AddComponent<UIHoverScale>();
            float pad = 10f;
            if (icon != null)
            {
                float s = rt.sizeDelta.y - 16f;
                var ic = Image(At(rt, "Icon", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(12, 0), new Vector2(s, s)), icon, Color.white);
                ic.type = UnityEngine.UI.Image.Type.Simple; ic.preserveAspect = true;
                pad = 12f + s;
            }
            var t = Text(Fill(rt, "Text", pad, 2, 10, 4), st, label, fontSize, interactable ? st.textLight : st.textMuted, TextAlignmentOptions.Center, true);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            return b;
        }

        /// <summary>Rounded progress bar.</summary>
        public static void Bar(RectTransform rt, UIStyle st, float value01, Color fill, string caption = null)
        {
            Image(rt, st.bar, new Color(st.woodDark.r, st.woodDark.g, st.woodDark.b, 0.25f));
            var f = Fill(rt, "Fill", 3, 3, 3, 3);
            f.anchorMax = new Vector2(Mathf.Clamp01(value01), 1f);
            f.offsetMax = new Vector2(Mathf.Clamp01(value01) > 0 ? -0f : 0f, -3f);
            var img = Image(f, st.bar, fill);
            img.enabled = value01 > 0.001f;
            if (caption != null)
            {
                var t = Text(Fill(rt, "Caption"), st, caption, rt.sizeDelta.y * 0.62f, st.text, TextAlignmentOptions.Center, true);
                t.textWrappingMode = TextWrappingModes.NoWrap;
            }
        }

        /// <summary>Card background.</summary>
        public static RectTransform Card(Transform parent, UIStyle st, float x, float y, float w, float h, Color? color = null)
        {
            var rt = Cell(parent, "Card", x, y, w, h);
            Image(rt, st.card, color ?? st.creamDark);
            return rt;
        }

        public static void Clear(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
            {
                var c = t.GetChild(i).gameObject;
                c.SetActive(false);
                UnityEngine.Object.Destroy(c);
            }
        }
    }

    /// <summary>Gentle hover / press scale for buttons and tabs.</summary>
    public class UIHoverScale : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        float target = 1f;
        public void OnPointerEnter(PointerEventData e) => target = 1.05f;
        public void OnPointerExit(PointerEventData e) => target = 1f;
        public void OnPointerDown(PointerEventData e) => target = 0.95f;
        public void OnPointerUp(PointerEventData e) => target = 1.05f;
        void Update()
        {
            float s = Mathf.Lerp(transform.localScale.x, target, 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime));
            transform.localScale = new Vector3(s, s, 1f);
        }
    }
}
