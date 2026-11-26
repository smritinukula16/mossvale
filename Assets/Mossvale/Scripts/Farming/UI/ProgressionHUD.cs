using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace Mossvale.Farming
{
    /// <summary>
    /// Always-visible progression HUD: farm level widget (blue star, level, progress bar, coins, star balance),
    /// level-up celebration banner and notification cards (achievements, milestones, unlocks, daily objectives).
    /// </summary>
    public class ProgressionHUD : MonoBehaviour
    {
        [SerializeField] UIStyle style;

        FarmProgressionManager P => FarmProgressionManager.Instance;
        TMP_Text levelNumber, levelTitle, barCaption, coinsText, balanceText;
        RectTransform barFill, starIcon, bannerRoot, notifyRoot;
        CanvasGroup bannerGroup;
        TMP_Text bannerTitle, bannerSub, bannerRewards;
        RectTransform bannerStar, bannerGlow;
        readonly Queue<(int level, LevelDefinition def)> levelQueue = new Queue<(int, LevelDefinition)>();
        readonly Queue<(string title, string body)> notifyQueue = new Queue<(string, string)>();
        readonly List<RectTransform> activeNotes = new List<RectTransform>();
        bool bannerBusy;
        int shownStars = -1;

        /// <summary>Number of level-up banners shown so far (tests).</summary>
        public int BannersShown { get; private set; }
        public bool BannerVisible => bannerGroup != null && bannerGroup.alpha > 0.01f;
        public string LevelText => levelNumber ? levelNumber.text : "";
        public string CoinsText => coinsText ? coinsText.text : "";

        void Start()
        {
            BuildWidget();
            BuildBanner();
            notifyRoot = UIBuilder.At(transform, "Notifications", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -96), new Vector2(600, 400));
            if (P)
            {
                P.Changed += Refresh;
                P.LeveledUp += OnLevelUp;
                P.AchievementUnlocked += a => notifyQueue.Enqueue(("Achievement unlocked!", $"{a.title}  +{a.stars} {Glyphs.Star}"));
                P.Notification += (t, b) => notifyQueue.Enqueue((t, b));
            }
            if (Wallet.Instance) Wallet.Instance.Changed += (c, d) => Refresh();
            Refresh();
        }

        void OnDestroy()
        {
            if (P) { P.Changed -= Refresh; P.LeveledUp -= OnLevelUp; }
        }

        // ------------------------------------------------------------------ widget
        void BuildWidget()
        {
            var w = UIBuilder.At(transform, "FarmLevelWidget", new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -24), new Vector2(470, 132));
            UIBuilder.Image(w, style.wood, Color.white);
            var inner = UIBuilder.Fill(w, "Inner", 10, 10, 10, 10);
            UIBuilder.Image(inner, style.panel, style.cream);
            starIcon = UIBuilder.Icon(inner, style.star, 6, 4, 104).rectTransform;
            levelNumber = UIBuilder.Label(inner, style, "1", 40, style.textLight, 6, 18, 104, 76, TextAlignmentOptions.Center, true);
            levelTitle = UIBuilder.Label(inner, style, "Farm Level 1", 24, style.text, 118, 4, 330, 34, TextAlignmentOptions.MidlineLeft, true);
            var bar = UIBuilder.Cell(inner, "LevelBar", 118, 40, 326, 28);
            UIBuilder.Image(bar, style.bar, new Color(style.woodDark.r, style.woodDark.g, style.woodDark.b, 0.25f));
            barFill = UIBuilder.Fill(bar, "Fill", 3, 3, 3, 3);
            UIBuilder.Image(barFill, style.bar, style.blue);
            barCaption = UIBuilder.Text(UIBuilder.Fill(bar, "Caption"), style, "", 17, style.text, TextAlignmentOptions.Center, true);
            UIBuilder.Icon(inner, style.coin, 118, 76, 30);
            coinsText = UIBuilder.Label(inner, style, "0", 22, style.text, 152, 74, 120, 34, TextAlignmentOptions.MidlineLeft, true);
            balanceText = UIBuilder.Label(inner, style, "", 19, style.blue, 262, 74, 186, 34, TextAlignmentOptions.MidlineRight, true);
        }

        void Refresh()
        {
            if (P == null || levelNumber == null) return;
            levelNumber.text = P.Level.ToString();
            levelTitle.text = $"Farm Level {P.Level}  <size=18><color=#{ColorUtility.ToHtmlStringRGB(style.textMuted)}>{P.CurrentLevel?.title}</color></size>";
            barFill.anchorMax = new Vector2(Mathf.Max(0.02f, P.LevelProgress01), 1f);
            barCaption.text = P.IsMaxLevel ? $"{P.LifetimeStars} {Glyphs.Star} - MAX" : $"{P.LifetimeStars} / {P.NextLevel.starsRequired} {Glyphs.Star}";
            coinsText.text = Wallet.Instance ? Wallet.Instance.Coins.ToString() : "0";
            balanceText.text = $"{P.StarBalance} {Glyphs.Star} to spend";
            if (shownStars >= 0 && P.LifetimeStars > shownStars) StartCoroutine(Punch(starIcon));
            shownStars = P.LifetimeStars;
        }

        IEnumerator Punch(RectTransform t)
        {
            for (float a = 0; a < 0.35f; a += Time.unscaledDeltaTime)
            {
                t.localScale = Vector3.one * (1f + 0.25f * Mathf.Sin(a / 0.35f * Mathf.PI));
                yield return null;
            }
            t.localScale = Vector3.one;
        }

        // ------------------------------------------------------------------ level up banner
        void BuildBanner()
        {
            bannerRoot = UIBuilder.Fill(transform, "LevelUpBanner");
            bannerGroup = bannerRoot.gameObject.AddComponent<CanvasGroup>();
            bannerGroup.alpha = 0f;
            bannerGroup.blocksRaycasts = false;
            bannerGlow = UIBuilder.At(bannerRoot, "Glow", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 150), new Vector2(560, 560));
            UIBuilder.Image(bannerGlow, style.glow, new Color(style.blueLight.r, style.blueLight.g, style.blueLight.b, 0.9f));
            bannerStar = UIBuilder.At(bannerRoot, "Star", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 150), new Vector2(240, 240));
            UIBuilder.Image(bannerStar, style.star, Color.white);
            var plate = UIBuilder.At(bannerRoot, "Plate", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -70), new Vector2(760, 230));
            UIBuilder.Image(plate, style.wood, Color.white);
            var pin = UIBuilder.Fill(plate, "Inner", 12, 12, 12, 12);
            UIBuilder.Image(pin, style.panel, style.cream);
            bannerTitle = UIBuilder.Label(pin, style, "LEVEL UP!", 52, style.blue, 0, 6, 736, 66, TextAlignmentOptions.Center, true);
            if (style.outlineFont) bannerTitle.fontSharedMaterial = style.outlineFont;
            bannerSub = UIBuilder.Label(pin, style, "", 30, style.text, 0, 72, 736, 44, TextAlignmentOptions.Center, true);
            bannerRewards = UIBuilder.Label(pin, style, "", 21, style.textMuted, 20, 118, 696, 80, TextAlignmentOptions.Center);
        }

        void OnLevelUp(int level, LevelDefinition def)
        {
            levelQueue.Enqueue((level, def));
            Refresh();
        }

        void Update()
        {
            if (!bannerBusy && levelQueue.Count > 0) StartCoroutine(ShowBanner(levelQueue.Dequeue()));
            if (notifyQueue.Count > 0 && activeNotes.Count < 3) StartCoroutine(ShowNote(notifyQueue.Dequeue()));
            if (bannerGlow) bannerGlow.Rotate(0, 0, 25f * Time.unscaledDeltaTime);
        }

        IEnumerator ShowBanner((int level, LevelDefinition def) e)
        {
            bannerBusy = true;
            BannersShown++;
            bannerSub.text = $"Farm Level {e.level} - {e.def?.title}";
            bannerRewards.text = e.def != null && e.def.rewards.Count > 0
                ? "Rewards: " + string.Join("  •  ", e.def.rewards.Select(ProgressionConfig.Describe)) : "";
            SpawnStarBurst();
            for (float t = 0; t < 0.45f; t += Time.unscaledDeltaTime)
            {
                float k = t / 0.45f;
                bannerGroup.alpha = k;
                float s = Mathf.LerpUnclamped(0.3f, 1f, 1f + 2.4f * Mathf.Pow(k - 1f, 3) + 1.4f * Mathf.Pow(k - 1f, 2));
                bannerStar.localScale = Vector3.one * s;
                bannerStar.localRotation = Quaternion.Euler(0, 0, (1f - k) * 180f);
                yield return null;
            }
            bannerGroup.alpha = 1f;
            bannerStar.localScale = Vector3.one;
            bannerStar.localRotation = Quaternion.identity;
            yield return new WaitForSecondsRealtime(3.0f);
            for (float t = 0; t < 0.5f; t += Time.unscaledDeltaTime) { bannerGroup.alpha = 1f - t / 0.5f; yield return null; }
            bannerGroup.alpha = 0f;
            bannerBusy = false;
        }

        void SpawnStarBurst()
        {
            for (int i = 0; i < 14; i++)
            {
                var s = UIBuilder.At(bannerRoot, "BurstStar", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 150), Vector2.one * Random.Range(30f, 56f));
                UIBuilder.Image(s, style.star, Color.white);
                StartCoroutine(Fly(s, Quaternion.Euler(0, 0, i * (360f / 14f) + Random.Range(-10f, 10f)) * Vector2.up * Random.Range(260f, 420f)));
            }
        }

        IEnumerator Fly(RectTransform s, Vector2 to)
        {
            var img = s.GetComponent<UnityEngine.UI.Image>();
            for (float t = 0; t < 1.1f; t += Time.unscaledDeltaTime)
            {
                float k = t / 1.1f;
                s.anchoredPosition = new Vector2(0, 150) + to * (1f - (1f - k) * (1f - k));
                s.localRotation = Quaternion.Euler(0, 0, k * 220f);
                img.color = new Color(1, 1, 1, 1f - k);
                yield return null;
            }
            Destroy(s.gameObject);
        }

        // ------------------------------------------------------------------ notifications
        IEnumerator ShowNote((string title, string body) n)
        {
            var card = UIBuilder.At(notifyRoot, "Notification", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(580, 84));
            activeNotes.Add(card);
            var g = card.gameObject.AddComponent<CanvasGroup>();
            g.blocksRaycasts = false;
            UIBuilder.Image(card, style.wood, Color.white);
            var inner = UIBuilder.Fill(card, "Inner", 8, 8, 8, 8);
            UIBuilder.Image(inner, style.panel, style.cream);
            UIBuilder.Icon(inner, style.star, 10, 6, 56);
            UIBuilder.Label(inner, style, n.title, 22, style.blue, 78, 2, 470, 32, TextAlignmentOptions.MidlineLeft, true);
            UIBuilder.Label(inner, style, n.body, 18, style.text, 78, 32, 470, 34);
            const float life = 3.6f;
            for (float t = 0; t < life; t += Time.unscaledDeltaTime)
            {
                int slot = activeNotes.IndexOf(card);
                float targetY = -slot * 92f;
                float y = Mathf.Lerp(card.anchoredPosition.y, targetY, 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime));
                card.anchoredPosition = new Vector2(t < 0.25f ? Mathf.Lerp(-40f, 0f, t / 0.25f) : 0f, y);
                g.alpha = t < 0.25f ? t / 0.25f : t > life - 0.4f ? (life - t) / 0.4f : 1f;
                yield return null;
            }
            activeNotes.Remove(card);
            Destroy(card.gameObject);
        }

        /// <summary>Notifications waiting or on screen (tests).</summary>
        public int PendingNotifications => notifyQueue.Count + activeNotes.Count;
    }

    /// <summary>Slow breathing scale (stars, glows).</summary>
    public class UIPulse : MonoBehaviour
    {
        public float amount = 0.05f, speed = 2f;
        void Update() => transform.localScale = Vector3.one * (1f + Mathf.Sin(Time.unscaledTime * speed) * amount);
    }
}
