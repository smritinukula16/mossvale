using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Mossvale.Farming
{
    [Serializable]
    public struct ItemStack
    {
        public ItemData item;
        public int count;
        public ItemStack(ItemData item, int count) { this.item = item; this.count = count; }
    }

    /// <summary>Tracks tools, seeds and produce the player owns.</summary>
    public class Inventory : MonoBehaviour, ISaveable
    {
        [SerializeField] List<ItemStack> startingItems = new List<ItemStack>();
        [Tooltip("Save key; defaults to the GameObject name.")]
        [SerializeField] string saveId = "";

        readonly Dictionary<ItemData, int> counts = new Dictionary<ItemData, int>();
        readonly List<ItemData> order = new List<ItemData>();
        bool initialised;

        /// <summary>(item, newCount, delta)</summary>
        public event Action<ItemData, int, int> ItemChanged;

        public IReadOnlyList<ItemData> Items { get { Init(); return order; } }

        void Awake() => Init();

        void Init()
        {
            if (initialised) return;
            initialised = true;
            foreach (var s in startingItems)
                if (s.item != null) AddSilently(s.item, s.count);
        }

        void AddSilently(ItemData item, int amount)
        {
            if (!counts.ContainsKey(item)) { counts[item] = 0; order.Add(item); }
            counts[item] += amount;
        }

        public int Count(ItemData item) { Init(); return item != null && counts.TryGetValue(item, out var c) ? c : 0; }
        public bool Has(ItemData item, int amount = 1) => Count(item) >= amount;

        public void Add(ItemData item, int amount = 1)
        {
            if (item == null || amount <= 0) return;
            Init();
            AddSilently(item, amount);
            ItemChanged?.Invoke(item, counts[item], amount);
            SaveManager.MarkDirty();
        }

        public bool TryRemove(ItemData item, int amount = 1)
        {
            if (item == null || amount <= 0 || !Has(item, amount)) return false;
            counts[item] -= amount;
            ItemChanged?.Invoke(item, counts[item], -amount);
            SaveManager.MarkDirty();
            return true;
        }

        /// <summary>Sets an exact count (used by dev tools and tests).</summary>
        public void SetCount(ItemData item, int amount)
        {
            if (item == null) return;
            int delta = Mathf.Max(0, amount) - Count(item);
            if (delta > 0) Add(item, delta);
            else if (delta < 0) TryRemove(item, -delta);
        }

        public IEnumerable<ItemData> ItemsOf(ItemCategory category) => Items.Where(i => i.category == category);

        /// <summary>Total number of units held (tools included).</summary>
        public int TotalCount(Func<ItemData, bool> filter = null) { Init(); return counts.Where(kv => filter == null || filter(kv.Key)).Sum(kv => kv.Value); }

        // ------------------------------------------------------------------ save
        [Serializable] class SavedStack { public string id; public int count; }
        [Serializable] class State { public List<SavedStack> items = new List<SavedStack>(); }

        public string SaveKey => "inventory:" + (string.IsNullOrEmpty(saveId) ? gameObject.name : saveId);
        public int RestoreOrder => 20;

        public string CaptureState()
        {
            var s = new State();
            foreach (var item in Items) s.items.Add(new SavedStack { id = item.itemId, count = Count(item) });
            return JsonUtility.ToJson(s);
        }

        public void RestoreState(string json)
        {
            var s = JsonUtility.FromJson<State>(json);
            var seen = new HashSet<ItemData>();
            foreach (var st in s.items)
            {
                var item = FarmManager.FindItem(st.id);
                if (item == null) { Debug.LogWarning("[Mossvale] Unknown saved item: " + st.id); continue; }
                SetCount(item, st.count);
                seen.Add(item);
            }
            foreach (var item in Items.ToList())
                if (!seen.Contains(item)) SetCount(item, 0);
        }

        public ItemData FindTool(ToolType type) =>
            Items.FirstOrDefault(i => i.category == ItemCategory.Tool && i.toolType == type && Count(i) > 0);
    }
}
