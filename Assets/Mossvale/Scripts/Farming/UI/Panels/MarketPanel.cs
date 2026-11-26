using System.Linq;
using TMPro;
using UnityEngine;

namespace Mossvale.Farming
{
    /// <summary>Market stall: sell harvested crops for coins.</summary>
    public class MarketPanel : UIPanel
    {
        MarketStall Market => Source as MarketStall;

        protected override void OnOpened()
        {
            if (Market) Market.PlayerInventory.ItemChanged += OnItems;
            if (Wallet.Instance) Wallet.Instance.Changed += OnWallet;
        }

        protected override void OnClosed()
        {
            if (Market) Market.PlayerInventory.ItemChanged -= OnItems;
            if (Wallet.Instance) Wallet.Instance.Changed -= OnWallet;
        }

        void OnItems(ItemData i, int c, int d) => RequestRefresh();
        void OnWallet(int a, int b) => RequestRefresh();

        protected override void BuildBody(RectTransform b)
        {
            var m = Market;
            if (m == null) return;
            var w = Wallet.Instance;
            var top = UIBuilder.Card(b, style, 0, 0, BodyWidth, 96, style.creamDark);
            UIBuilder.Icon(top, style.coin, 18, 14, 68);
            T(top, $"<b>{(w ? w.Coins : 0)} coins</b>", 100, 6, 400, 50, 34);
            T(top, $"Total earned: {(w ? w.TotalEarned : 0)} {Glyphs.Coin}", 100, 54, 400, 34, 19, style.textMuted);
            var pr = FarmProgressionManager.Instance;
            if (pr) T(top, $"Every {pr.Config.coinsPerStar} coins from sales earns {StarText(1)}", BodyWidth - 520, 0, 500, 96, 20, style.blue, TextAlignmentOptions.MidlineRight);

            var produce = m.PlayerInventory.ItemsOf(ItemCategory.Produce).Where(MarketStall.IsSellable).ToList();
            H(b, "Today's prices", 0, 112, 400);
            for (int i = 0; i < produce.Count; i++)
            {
                var item = produce[i];
                int have = m.PlayerInventory.Count(item);
                var c = UIBuilder.Card(b, style, 0, 156 + i * 104, BodyWidth, 94, style.cream);
                UIBuilder.Icon(c, item.icon, 14, 9, 76);
                T(c, item.displayName, 104, 10, 300, 40, 26).fontStyle = FontStyles.Bold;
                T(c, $"You have x{have}", 104, 50, 300, 34, 19, have > 0 ? style.text : style.textMuted);
                T(c, $"{item.sellPrice} {Glyphs.Coin} each", 420, 0, 220, 94, 24, style.text, TextAlignmentOptions.Center);
                var it = item;
                Btn(c, "Sell 1", BodyWidth - 440, 20, 160, 54, () => m.Sell(it, 1), have > 0, style.green, 20);
                Btn(c, have > 0 ? $"Sell all (+{have * item.sellPrice})" : "Sell all", BodyWidth - 268, 20, 250, 54, () => m.Sell(it, have), have > 0, style.wood1, 20);
            }
            T(b, "The market buys harvested crops. Seeds and tools can't be sold.", 0, 156 + produce.Count * 104 + 8, BodyWidth, 36, 18, style.textMuted, TextAlignmentOptions.Center);
        }
    }
}
