using System.Linq;
using TMPro;
using UnityEngine;

namespace Mossvale.Farming
{
    /// <summary>Farmhouse: farm overview, level progression, achievements, daily objectives and upgrades.</summary>
    public class FarmhousePanel : UIPanel
    {
        int tab;
        static readonly string[] TabNames = { "Overview", "Achievements", "Daily", "Upgrades" };
        FarmProgressionManager P => FarmProgressionManager.Instance;

        public int SelectedTab => tab;
        public void SelectTab(int i) { tab = Mathf.Clamp(i, 0, TabNames.Length - 1); RequestRefresh(); }

        protected override void OnOpened()
        {
            if (P) P.Changed += RequestRefresh;
            if (Wallet.Instance) Wallet.Instance.Changed += OnWallet;
        }

        protected override void OnClosed()
        {
            if (P) P.Changed -= RequestRefresh;
            if (Wallet.Instance) Wallet.Instance.Changed -= OnWallet;
        }

        void OnWallet(int balance, int delta) => RequestRefresh();

        protected override void BuildBody(RectTransform b)
        {
            float y = Tabs(b, TabNames, tab, SelectTab);
            switch (tab)
            {
                case 0: Overview(b, y); break;
                case 1: Achievements(b, y); break;
                case 2: Daily(b, y); break;
                case 3: Upgrades(b, y); break;
            }
        }

        // ------------------------------------------------------------------ overview
        void Overview(RectTransform b, float y)
        {
            var cfg = P.Config;
            var left = UIBuilder.Card(b, style, 0, y, 380, BodyHeight - y, style.creamDark);
            var star = UIBuilder.Icon(left, style.star, 115, 18, 150);
            star.gameObject.AddComponent<UIPulse>();
            T(left, P.Level.ToString(), 115, 58, 150, 80, 56, style.textLight, TextAlignmentOptions.Center).fontStyle = FontStyles.Bold;
            T(left, $"Farm Level {P.Level}", 20, 176, 340, 40, 32, style.text, TextAlignmentOptions.Center).fontStyle = FontStyles.Bold;
            T(left, P.CurrentLevel?.title ?? "", 20, 214, 340, 30, 21, style.textMuted, TextAlignmentOptions.Center);
            string cap = P.IsMaxLevel ? $"{P.LifetimeStars} {Glyphs.Star}  MAX" : $"{P.LifetimeStars} / {P.NextLevel.starsRequired} {Glyphs.Star}";
            UIBuilder.Bar(UIBuilder.Cell(left, "LevelBar", 20, 256, 340, 36), style, P.LevelProgress01, style.blue, cap);
            T(left, P.IsMaxLevel ? "Maximum farm level reached!" : $"{P.NextLevel.starsRequired - P.LifetimeStars} more {Glyphs.Star} to Level {P.Level + 1}",
                20, 298, 340, 30, 19, style.text, TextAlignmentOptions.Center);
            var bal = UIBuilder.Card(left, style, 20, 344, 340, 104, style.cream);
            T(bal, $"<b>Star balance: {StarText(P.StarBalance)}</b>\nSpend stars to restore the Heart of Mossvale", 16, 8, 308, 88, 18, style.text, TextAlignmentOptions.Center);
            T(left, "Earn stars by harvesting, selling, milestones, achievements and daily objectives.", 20, 456, 340, 60, 16, style.textMuted, TextAlignmentOptions.Center);

            float rx = 400, rw = BodyWidth - rx, tw = (rw - 20) / 3f;
            var heart = FindFirstObjectByType<HeartOfMossvale>();
            (string label, string value)[] tiles =
            {
                ("Crops harvested", P.Stat(Stats.Harvests).ToString()),
                ("Coins earned", $"{(Wallet.Instance ? Wallet.Instance.TotalEarned : 0)} {Glyphs.Coin}"),
                ("Plots unlocked", $"{P.UnlockedPlotCount} / {FarmManager.Plots.Count}"),
                ("Achievements", $"{P.CompletedAchievements} / {cfg.achievements.Count}"),
                ("Heart restored", heart ? $"{heart.Stage} / {heart.MaxStage}" : "-"),
                ("Greenhouse", P.GreenhouseUnlocked ? "Unlocked" : "Locked"),
            };
            for (int i = 0; i < tiles.Length; i++)
            {
                var c = UIBuilder.Card(b, style, rx + (i % 3) * (tw + 10), y + (i / 3) * 102, tw, 92, style.cream);
                T(c, tiles[i].label, 14, 8, tw - 28, 26, 17, style.textMuted);
                T(c, tiles[i].value, 14, 36, tw - 28, 46, 30, style.text).fontStyle = FontStyles.Bold;
            }

            float my = y + 214;
            H(b, "Crop milestones", rx, my, rw);
            int row = 0;
            foreach (var m in P.Milestones)
            {
                float ry = my + 42 + row * 50;
                if (m.track.crop && m.track.crop.produceItem) UIBuilder.Icon(b, m.track.crop.produceItem.icon, rx, ry, 42);
                T(b, m.track.title, rx + 52, ry, 220, 42, 20);
                bool done = m.next < 0;
                int prev = m.tier > 0 ? m.track.thresholds[m.tier - 1] : 0;
                float v = done ? 1f : (m.value - prev) / (float)Mathf.Max(1, m.next - prev);
                UIBuilder.Bar(UIBuilder.Cell(b, "MilestoneBar", rx + 280, ry + 6, 280, 30), style, v, style.green, done ? "All milestones done" : $"{m.value} / {m.next}");
                if (!done) T(b, $"+{StarText(m.tier < m.track.stars.Length ? m.track.stars[m.tier] : 1)}", rx + 574, ry, 140, 42, 20, style.blue);
                row++;
            }

            var next = P.NextLevel;
            var nc = UIBuilder.Card(b, style, rx, BodyHeight - 96, rw, 96, new Color(style.blueLight.r, style.blueLight.g, style.blueLight.b, 0.55f));
            UIBuilder.Icon(nc, style.star, 14, 18, 60);
            if (next != null)
            {
                string rewards = string.Join("  •  ", next.rewards.Select(ProgressionConfig.Describe));
                T(nc, $"<b>Next unlock - Level {next.level}: {next.title}</b>\n{rewards}", 90, 8, rw - 104, 80, 19);
            }
            else T(nc, "<b>Your farm is fully grown!</b>\nKeep farming to restore the Heart of Mossvale.", 90, 8, rw - 104, 80, 19);
        }

        // ------------------------------------------------------------------ achievements
        void Achievements(RectTransform b, float y)
        {
            var list = P.Config.achievements;
            float cw = (BodyWidth - 16) / 2f, ch = 86;
            for (int i = 0; i < list.Count; i++)
            {
                var a = list[i];
                bool done = P.HasAchievement(a.id);
                var c = UIBuilder.Card(b, style, (i % 2) * (cw + 16), y + (i / 2) * (ch + 8), cw, ch, done ? new Color(0.84f, 0.93f, 0.74f) : style.cream);
                UIBuilder.Icon(c, done ? style.check : style.lockIcon, 14, 17, 52);
                T(c, a.title, 80, 8, cw - 200, 34, 22).fontStyle = FontStyles.Bold;
                T(c, a.description, 80, 40, cw - 200, 40, 16, style.textMuted);
                T(c, $"+{StarText(a.stars)}" + (a.coins > 0 ? $"  +{a.coins} {Glyphs.Coin}" : ""), cw - 118, 8, 108, 34, 20, style.blue, TextAlignmentOptions.MidlineRight);
                T(c, done ? "Complete" : $"{Mathf.Min(P.Stat(a.statKey), a.threshold)} / {a.threshold}", cw - 118, 44, 108, 30, 17, style.textMuted, TextAlignmentOptions.MidlineRight);
            }
        }

        // ------------------------------------------------------------------ daily objectives
        void Daily(RectTransform b, float y)
        {
            H(b, "Today's objectives", 0, y, 500);
            T(b, System.DateTime.Today.ToString("dddd d MMMM"), BodyWidth - 400, y, 400, 38, 18, style.textMuted, TextAlignmentOptions.MidlineRight);
            int i = 0;
            foreach (var d in P.DailyObjectives)
            {
                var c = UIBuilder.Card(b, style, 0, y + 50 + i * 116, BodyWidth, 104, d.done ? new Color(0.84f, 0.93f, 0.74f) : style.cream);
                UIBuilder.Icon(c, d.done ? style.check : style.star, 18, 22, 60);
                T(c, d.def.title, 96, 10, 560, 36, 24).fontStyle = FontStyles.Bold;
                UIBuilder.Bar(UIBuilder.Cell(c, "DailyBar", 96, 56, 560, 30), style, d.progress / (float)Mathf.Max(1, d.def.amount), style.green, d.done ? "Complete!" : $"{d.progress} / {d.def.amount}");
                T(c, $"Reward\n+{StarText(d.def.rewardStars)}   +{d.def.rewardCoins} {Glyphs.Coin}", BodyWidth - 300, 10, 280, 84, 21, style.text, TextAlignmentOptions.MidlineRight);
                i++;
            }
            T(b, "New objectives arrive every day. Completed objectives give their reward automatically.", 0, y + 50 + 3 * 116 + 6, BodyWidth, 40, 18, style.textMuted, TextAlignmentOptions.Center);
        }

        // ------------------------------------------------------------------ upgrades
        void Upgrades(RectTransform b, float y)
        {
            var coins = Wallet.Instance ? Wallet.Instance.Coins : 0;
            H(b, "Farm upgrades", 0, y, 500);
            T(b, $"You have {coins} {Glyphs.Coin}", BodyWidth - 400, y, 400, 38, 22, style.text, TextAlignmentOptions.MidlineRight).fontStyle = FontStyles.Bold;
            var list = P.Config.upgrades;
            for (int i = 0; i < list.Count; i++)
            {
                var u = list[i];
                bool owned = P.OwnsUpgrade(u.id);
                bool can = P.CanBuyUpgrade(u, out var reason);
                var c = UIBuilder.Card(b, style, 0, y + 50 + i * 118, BodyWidth, 106, owned ? new Color(0.84f, 0.93f, 0.74f) : style.cream);
                T(c, u.title, 20, 10, 620, 36, 24).fontStyle = FontStyles.Bold;
                T(c, u.description, 20, 46, 620, 30, 18, style.textMuted);
                T(c, $"Requires Farm Level {u.requiredLevel}", 20, 72, 620, 28, 16, P.Level >= u.requiredLevel ? style.greenDark : style.red);
                T(c, $"{u.cost} {Glyphs.Coin}", BodyWidth - 420, 30, 160, 46, 26, style.text, TextAlignmentOptions.MidlineRight).fontStyle = FontStyles.Bold;
                string label = owned ? "Owned" : can ? "Buy" : reason;
                var upg = u;
                Btn(c, label, BodyWidth - 240, 26, 220, 54, () => P.TryBuyUpgrade(upg), can, owned ? style.greenDark : style.green, 20);
            }
        }
    }
}
