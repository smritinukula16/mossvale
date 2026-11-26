using TMPro;
using UnityEngine;

namespace Mossvale.Farming
{
    /// <summary>Greenhouse: shows unlock requirements while locked; seed nursery once unlocked.</summary>
    public class GreenhousePanel : UIPanel
    {
        GreenhouseBuilding G => Source as GreenhouseBuilding;
        FarmProgressionManager P => FarmProgressionManager.Instance;

        protected override void OnOpened()
        {
            if (P) P.Changed += RequestRefresh;
            if (G) G.PlayerInventory.ItemChanged += OnItems;
        }

        protected override void OnClosed()
        {
            if (P) P.Changed -= RequestRefresh;
            if (G) G.PlayerInventory.ItemChanged -= OnItems;
        }

        void OnItems(ItemData i, int c, int d) => RequestRefresh();

        protected override void BuildBody(RectTransform b)
        {
            if (G == null || P == null) return;
            if (!G.IsUnlocked) Locked(b); else Nursery(b);
        }

        void Locked(RectTransform b)
        {
            var cfg = P.Config;
            UIBuilder.Icon(b, style.lockIcon, 40, 20, 150);
            H(b, "The greenhouse is overgrown", 230, 24, BodyWidth - 230, 34);
            T(b, "Grow your farm to restore it. Once unlocked, every crop grows faster and the seed nursery opens.", 230, 70, BodyWidth - 250, 70, 20, style.textMuted);

            bool lvOk = P.Level >= cfg.greenhouseRequiredLevel, stOk = P.LifetimeStars >= cfg.greenhouseRequiredStars;
            Req(b, 190, lvOk, $"Reach Farm Level {cfg.greenhouseRequiredLevel}", $"You: Level {P.Level}", lvOk ? 1f : P.Level / (float)cfg.greenhouseRequiredLevel);
            Req(b, 290, stOk, $"Earn {cfg.greenhouseRequiredStars} {Glyphs.Star} in total", $"You: {P.LifetimeStars} {Glyphs.Star}", Mathf.Clamp01(P.LifetimeStars / (float)cfg.greenhouseRequiredStars));

            var rw = UIBuilder.Card(b, style, 0, 396, BodyWidth, 70, new Color(style.blueLight.r, style.blueLight.g, style.blueLight.b, 0.5f));
            T(rw, $"<b>Rewards:</b>  crops grow {Mathf.RoundToInt((cfg.greenhouseGrowthBonus - 1f) * 100)}% faster  •  seed nursery  •  +{StarText(cfg.starsPerAreaUnlock)}", 20, 0, BodyWidth - 40, 70, 21);
            bool met = G.RequirementsMet;
            Btn(b, met ? "Unlock the greenhouse" : "Requirements not met", BodyWidth / 2 - 200, 486, 400, 64, () => G.TryUnlock(), met, style.blue, 24);
        }

        void Req(Transform b, float y, bool ok, string what, string you, float progress)
        {
            var c = UIBuilder.Card(b, style, 0, y, BodyWidth, 88, ok ? new Color(0.84f, 0.93f, 0.74f) : style.cream);
            UIBuilder.Icon(c, ok ? style.check : style.lockIcon, 16, 16, 56);
            T(c, what, 90, 8, 520, 40, 24).fontStyle = FontStyles.Bold;
            T(c, you, 90, 46, 520, 32, 19, style.textMuted);
            UIBuilder.Bar(UIBuilder.Cell(c, "ReqBar", BodyWidth - 440, 27, 420, 34), style, progress, ok ? style.green : style.blue, ok ? "Done" : $"{Mathf.RoundToInt(progress * 100)}%");
        }

        void Nursery(RectTransform b)
        {
            var cfg = P.Config;
            var top = UIBuilder.Card(b, style, 0, 0, BodyWidth, 96, new Color(0.84f, 0.93f, 0.74f));
            UIBuilder.Icon(top, style.check, 18, 18, 60);
            T(top, $"<b>Greenhouse active</b>\nAll crops grow {Mathf.RoundToInt((cfg.greenhouseGrowthBonus - 1f) * 100)}% faster across the farm.", 96, 6, BodyWidth - 120, 84, 22);
            H(b, "Seed nursery", 0, 116, 400);
            T(b, $"Turn one crop into {cfg.nurserySeedsPerProduce} seeds of the same kind.", 0, 152, BodyWidth, 30, 18, style.textMuted);
            var db = FarmManager.Instance ? FarmManager.Instance.Database : null;
            if (db == null) return;
            for (int i = 0; i < db.crops.Length; i++)
            {
                var crop = db.crops[i];
                int have = G.PlayerInventory.Count(crop.produceItem);
                var c = UIBuilder.Card(b, style, 0, 196 + i * 100, BodyWidth, 90, style.cream);
                UIBuilder.Icon(c, crop.produceItem.icon, 14, 9, 72);
                T(c, $"1 {crop.produceItem.displayName}  →  {cfg.nurserySeedsPerProduce} {crop.seedItem.displayName}", 100, 8, 600, 40, 24).fontStyle = FontStyles.Bold;
                T(c, $"You have x{have} {crop.produceItem.displayName}", 100, 48, 600, 32, 18, style.textMuted);
                UIBuilder.Icon(c, crop.seedItem.icon, BodyWidth - 330, 13, 64);
                var cr = crop;
                Btn(c, "Grow seeds", BodyWidth - 240, 18, 220, 54, () => G.Nurse(cr), have > 0, style.green, 20);
            }
        }
    }
}
