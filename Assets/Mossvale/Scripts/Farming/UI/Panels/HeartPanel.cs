using TMPro;
using UnityEngine;

namespace Mossvale.Farming
{
    /// <summary>Heart of Mossvale: restoration level, star requirements, next visual stage, spend stars to restore.</summary>
    public class HeartPanel : UIPanel
    {
        HeartOfMossvale Heart => Source as HeartOfMossvale;
        FarmProgressionManager P => FarmProgressionManager.Instance;

        protected override void OnOpened() { if (P) P.Changed += RequestRefresh; }
        protected override void OnClosed() { if (P) P.Changed -= RequestRefresh; }

        protected override void BuildBody(RectTransform b)
        {
            var h = Heart;
            if (h == null || P == null) return;
            var cur = h.CurrentStage;

            var left = UIBuilder.Card(b, style, 0, 0, 420, BodyHeight, style.creamDark);
            float glow = h.MaxStage > 0 ? h.Stage / (float)h.MaxStage : 1f;
            var g = UIBuilder.Icon(left, style.glow, 60, 6, 300, new Color(style.blueLight.r, style.blueLight.g, style.blueLight.b, 0.25f + 0.6f * glow));
            g.gameObject.AddComponent<UIPulse>();
            UIBuilder.Icon(left, style.heart, 120, 66, 180, Color.Lerp(new Color(0.62f, 0.58f, 0.52f), style.blue, glow));
            T(left, cur.title, 20, 300, 380, 44, 30, style.text, TextAlignmentOptions.Center).fontStyle = FontStyles.Bold;
            T(left, $"Restoration {h.Stage} / {h.MaxStage}", 20, 342, 380, 30, 20, style.textMuted, TextAlignmentOptions.Center);
            float pipW = 44, total = h.MaxStage * pipW + (h.MaxStage - 1) * 12;
            for (int i = 1; i <= h.MaxStage; i++)
                UIBuilder.Icon(left, style.circle, 210 - total / 2 + (i - 1) * (pipW + 12), 382, pipW, i <= h.Stage ? style.blue : new Color(0, 0, 0, 0.15f));
            T(left, cur.description, 24, 440, 372, 140, 18, style.text, TextAlignmentOptions.Top);

            float rx = 440, rw = BodyWidth - rx;
            T(b, $"Your stars: {StarText(P.StarBalance)}", rx, 0, rw, 44, 26, style.text, TextAlignmentOptions.MidlineRight).fontStyle = FontStyles.Bold;
            var next = h.NextStage;
            if (next == null)
            {
                var done = UIBuilder.Card(b, style, rx, 60, rw, 200, new Color(0.84f, 0.93f, 0.74f));
                T(done, "<b>The Heart of Mossvale is fully restored.</b>\nThe whole valley feels brighter. Thank you, farmer!", 24, 20, rw - 48, 160, 24, style.text, TextAlignmentOptions.Center);
                return;
            }
            H(b, "Next restoration", rx, 52, rw, 28);
            var nc = UIBuilder.Card(b, style, rx, 96, rw, 160, style.cream);
            UIBuilder.Icon(nc, style.star, 18, 18, 70);
            T(nc, next.title, 110, 14, rw - 130, 40, 28).fontStyle = FontStyles.Bold;
            T(nc, next.description, 110, 56, rw - 130, 96, 19, style.textMuted, TextAlignmentOptions.TopLeft);

            bool starsOk = P.StarBalance >= next.starCost, levelOk = P.Level >= next.requiredLevel;
            Requirement(b, rx, 272, rw, starsOk, $"Spend {StarText(next.starCost)}", $"You have {StarText(P.StarBalance)}");
            Requirement(b, rx, 352, rw, levelOk, $"Farm Level {next.requiredLevel}", $"You are Level {P.Level}");
            bool can = h.CanRestore(out var reason);
            Btn(b, can ? $"Restore  (-{next.starCost})" : reason, rx + rw / 2 - 230, 452, 460, 70, () => h.TryRestore(), can, style.blue, 24);
            T(b, "Spent stars still count towards your farm level.", rx, 530, rw, 34, 17, style.textMuted, TextAlignmentOptions.Center);
        }

        void Requirement(Transform b, float x, float y, float w, bool ok, string what, string you)
        {
            var c = UIBuilder.Card(b, style, x, y, w, 70, ok ? new Color(0.84f, 0.93f, 0.74f) : style.cream);
            UIBuilder.Icon(c, ok ? style.check : style.lockIcon, 14, 11, 48);
            T(c, what, 76, 0, w / 2, 70, 22).fontStyle = FontStyles.Bold;
            T(c, you, w / 2, 0, w / 2 - 20, 70, 19, style.textMuted, TextAlignmentOptions.MidlineRight);
        }
    }
}
