using System.Collections;
using TMPro;
using UnityEngine;

namespace Mossvale.Farming
{
    /// <summary>
    /// Base for every building window: wooden frame, title bar with icon, cream body, close button, Esc to close,
    /// open/close animation. While open, world input is paused (GameplayInput.Blocked) and the backdrop eats clicks.
    /// Subclasses draw their content in BuildBody(); call RequestRefresh() when data changes.
    /// </summary>
    public abstract class UIPanel : MonoBehaviour
    {
        public static UIPanel Current { get; private set; }
        public static bool AnyOpen => Current != null;

        [SerializeField] protected UIStyle style;
        [SerializeField] protected string title = "Panel";
        [SerializeField] protected Sprite titleIcon;
        [SerializeField] protected Vector2 windowSize = new Vector2(1200, 740);

        CanvasGroup group;
        RectTransform window;
        protected RectTransform body;
        TMP_Text titleText;
        bool built, refreshQueued;
        Coroutine anim;

        public bool IsOpen { get; private set; }
        public Building Source { get; private set; }
        public string Title => title;

        protected virtual void Awake()
        {
            Build();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
            window.gameObject.SetActive(false);
        }

        void Build()
        {
            if (built) return;
            built = true;
            var root = (RectTransform)transform;
            if (!TryGetComponent(out group)) group = gameObject.AddComponent<CanvasGroup>();

            var backdrop = UIBuilder.Fill(root, "Backdrop");
            UIBuilder.Image(backdrop, null, new Color(0.12f, 0.08f, 0.04f, 0.45f), true);

            window = UIBuilder.At(root, "Window", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, windowSize);
            UIBuilder.Image(window, style.wood, Color.white, true);
            var inner = UIBuilder.Fill(window, "Inner", 18, 92, 18, 18);
            UIBuilder.Image(inner, style.panel, style.cream);

            // Title bar on the wood, with a little plaque behind the icon
            if (titleIcon != null) UIBuilder.Icon(window, titleIcon, 28, 14, 64);
            titleText = UIBuilder.Label(window, style, title, 40, style.textLight, titleIcon != null ? 104 : 32, 14, windowSize.x - 240, 64,
                TextAlignmentOptions.MidlineLeft, true);
            if (style.outlineFont) titleText.fontSharedMaterial = style.outlineFont;

            var close = UIBuilder.At(window, "CloseButton", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-20, -18), new Vector2(60, 60));
            UIBuilder.Button(close, style, "", style.red, Close, true, 22, null);
            var x = UIBuilder.Fill(close, "X", 14, 14, 14, 14);
            UIBuilder.Image(x, style.close, Color.white).type = UnityEngine.UI.Image.Type.Simple;

            body = UIBuilder.Fill(inner, "Body", 24, 22, 24, 22);
        }

        public void SetTitle(string t) { title = t; if (titleText) titleText.text = t; }

        public void Open(Building source)
        {
            Build();
            if (Current != null && Current != this) Current.Close();
            Source = source;
            Current = this;
            IsOpen = true;
            GameplayInput.Blocked = true;
            window.gameObject.SetActive(true);
            group.blocksRaycasts = true;
            group.interactable = true;
            OnOpened();
            Refresh();
            if (anim != null) StopCoroutine(anim);
            anim = StartCoroutine(Animate(true));
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            if (Current == this) Current = null;
            GameplayInput.Blocked = AnyOpen;
            group.blocksRaycasts = false;
            group.interactable = false;
            OnClosed();
            if (anim != null) StopCoroutine(anim);
            if (isActiveAndEnabled) anim = StartCoroutine(Animate(false));
            else { group.alpha = 0f; window.gameObject.SetActive(false); }
        }

        IEnumerator Animate(bool open)
        {
            float from = group.alpha, to = open ? 1f : 0f;
            const float duration = 0.22f;
            for (float t = 0; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = t / duration;
                float e = open ? 1f + 2.2f * Mathf.Pow(k - 1f, 3) + 1.2f * Mathf.Pow(k - 1f, 2) : 1f - k * k; // ease-out-back / ease-in
                group.alpha = Mathf.Lerp(from, to, k);
                window.localScale = Vector3.one * (open ? Mathf.LerpUnclamped(0.9f, 1f, e) : Mathf.Lerp(0.94f, 1f, e));
                yield return null;
            }
            group.alpha = to;
            window.localScale = Vector3.one;
            if (!open) window.gameObject.SetActive(false);
            anim = null;
        }

        protected virtual void Update()
        {
            if (IsOpen && Input.GetKeyDown(KeyCode.Escape)) Close();
        }

        void LateUpdate()
        {
            if (refreshQueued && IsOpen) { refreshQueued = false; Refresh(); }
        }

        /// <summary>Rebuild the body at the end of this frame (safe to call from button handlers and events).</summary>
        public void RequestRefresh() { if (IsOpen) refreshQueued = true; }

        /// <summary>Rebuilds the body content now.</summary>
        public void Refresh()
        {
            Build();
            UIBuilder.Clear(body);
            BuildBody(body);
        }

        protected abstract void BuildBody(RectTransform body);
        protected virtual void OnOpened() { }
        protected virtual void OnClosed() { }

        protected float BodyWidth => windowSize.x - 36 - 48;
        protected float BodyHeight => windowSize.y - 110 - 44;

        // ------------------------------------------------------------------ shared widgets
        protected TMP_Text H(Transform p, string text, float x, float y, float w, float size = 26) =>
            UIBuilder.Label(p, style, text, size, style.text, x, y, w, size + 12, TextAlignmentOptions.MidlineLeft, true);

        protected TMP_Text T(Transform p, string text, float x, float y, float w, float h, float size = 20, Color? c = null,
            TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft) =>
            UIBuilder.Label(p, style, text, size, c ?? style.text, x, y, w, h, align);

        protected UnityEngine.UI.Button Btn(Transform p, string label, float x, float y, float w, float h, System.Action onClick,
            bool enabled = true, Color? color = null, float size = 20) =>
            UIBuilder.Button(UIBuilder.Cell(p, "Button_" + label, x, y, w, h), style, label, color ?? style.green, () => { onClick?.Invoke(); RequestRefresh(); }, enabled, size);

        /// <summary>Row of tab buttons; returns the y below the tabs.</summary>
        protected float Tabs(Transform p, string[] names, int selected, System.Action<int> onSelect, bool[] enabled = null)
        {
            float w = Mathf.Min(240f, (BodyWidth - (names.Length - 1) * 10f) / names.Length);
            for (int i = 0; i < names.Length; i++)
            {
                int idx = i;
                bool en = enabled == null || enabled[i];
                var b = Btn(p, names[i], i * (w + 10), 0, w, 52, () => onSelect(idx), en, i == selected ? style.green : style.wood1, 22);
                if (i == selected) b.transform.localScale = Vector3.one * 1.02f;
            }
            return 68f;
        }

        protected string StarText(int n) => $"{n} {Glyphs.Star}";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Current = null;
    }
}
