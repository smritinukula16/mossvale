using System.Linq;
using TMPro;
using UnityEngine;

namespace Mossvale.Farming
{
    /// <summary>Barn: move seeds and produce between the backpack and barn storage. "Animals" tab reserved for later.</summary>
    public class BarnPanel : UIPanel
    {
        int tab;
        BarnStorage Barn => Source as BarnStorage;
        public void SelectTab(int i) { tab = i; RequestRefresh(); }

        protected override void OnOpened()
        {
            if (Barn) { Barn.PlayerInventory.ItemChanged += OnItems; Barn.Storage.ItemChanged += OnItems; }
        }

        protected override void OnClosed()
        {
            if (Barn) { Barn.PlayerInventory.ItemChanged -= OnItems; Barn.Storage.ItemChanged -= OnItems; }
        }

        void OnItems(ItemData i, int c, int d) => RequestRefresh();

        protected override void BuildBody(RectTransform b)
        {
            float y = Tabs(b, new[] { "Storage", "Animals (soon)" }, tab, SelectTab);
            if (tab == 1) { Animals(b, y); return; }
            var barn = Barn;
            if (barn == null) return;

            T(b, "<b>Barn storage</b>", 0, y, 220, 40, 24);
            UIBuilder.Bar(UIBuilder.Cell(b, "CapacityBar", 220, y + 4, 520, 34), style, barn.Used / (float)Mathf.Max(1, barn.Capacity),
                barn.Free == 0 ? style.red : style.green, $"{barn.Used} / {barn.Capacity}  ({barn.Free} free)");
            T(b, "Upgrade capacity in the Farmhouse", 760, y, BodyWidth - 760, 40, 17, style.textMuted, TextAlignmentOptions.MidlineRight);

            var items = barn.PlayerInventory.Items.Concat(barn.Storage.Items).Where(BarnStorage.IsStorable).Distinct()
                .OrderBy(i => i.category).ThenBy(i => i.displayName).ToList();
            float cw = (BodyWidth - 24) / 2f, top = y + 56;
            H(b, "Backpack", 0, top, cw);
            H(b, "In the barn", cw + 24, top, cw);
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                float ry = top + 44 + i * 60;
                Row(b, item, 0, ry, cw, barn.PlayerInventory.Count(item), "Store", n => barn.Store(item, n), barn.Free);
                Row(b, item, cw + 24, ry, cw, barn.Storage.Count(item), "Take", n => barn.Retrieve(item, n), int.MaxValue);
            }
        }

        void Row(Transform b, ItemData item, float x, float y, float w, int count, string verb, System.Func<int, bool> act, int limit)
        {
            var c = UIBuilder.Card(b, style, x, y, w, 52, style.cream);
            UIBuilder.Icon(c, item.icon, 8, 4, 44);
            T(c, item.displayName, 60, 0, w - 330, 52, 19);
            T(c, "x" + count, w - 270, 0, 60, 52, 20, style.text, TextAlignmentOptions.MidlineRight).fontStyle = FontStyles.Bold;
            int all = Mathf.Min(count, limit);
            Btn(c, verb + " 1", w - 200, 6, 104, 40, () => act(1), count > 0 && limit > 0, style.green, 18);
            Btn(c, "All", w - 88, 6, 80, 40, () => act(all), all > 0, style.wood1, 18);
        }

        void Animals(RectTransform b, float y)
        {
            var c = UIBuilder.Card(b, style, BodyWidth / 2 - 380, y + 40, 760, 360, style.creamDark);
            UIBuilder.Icon(c, style.lockIcon, 320, 30, 120);
            T(c, "Animal pens - coming soon", 20, 170, 720, 46, 32, style.text, TextAlignmentOptions.Center).fontStyle = FontStyles.Bold;
            T(c, "The barn has room for chickens, cows and sheep.\nAnimal care will arrive in a future update of Mossvale.", 40, 222, 680, 100, 20, style.textMuted, TextAlignmentOptions.Center);
        }
    }
}
