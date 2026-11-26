using UnityEngine;

namespace Mossvale.Farming
{
    /// <summary>
    /// Barn: stores seeds and produce in a separate Inventory with limited capacity (upgradable via progression).
    /// Animal features can be added later as extra barn modules / panel tabs (see BarnPanel "Animals" tab).
    /// </summary>
    public class BarnStorage : Building
    {
        [SerializeField] Inventory storage;

        public Inventory Storage => storage;
        public int Capacity => Progression ? Progression.BarnCapacity : 30;
        public int Used => storage ? storage.TotalCount() : 0;
        public int Free => Mathf.Max(0, Capacity - Used);
        public override string PromptDetail => $"Barn storage {Used}/{Capacity}";

        public static bool IsStorable(ItemData item) => item != null && (item.category == ItemCategory.Seed || item.category == ItemCategory.Produce);

        /// <summary>Moves items from the player into the barn. Fails if the player lacks them or the barn is full.</summary>
        public bool Store(ItemData item, int qty)
        {
            if (!IsStorable(item) || qty <= 0) return Fail("Tools can't be stored");
            if (!playerInventory.Has(item, qty)) return Fail($"You don't have {qty} {item.displayName}");
            if (qty > Free) return Fail(Free == 0 ? "The barn is full" : $"Only {Free} space left");
            playerInventory.TryRemove(item, qty);
            storage.Add(item, qty);
            FarmEvents.RaiseStored(item, qty);
            FarmFeedback.Raise($"Stored {qty} {item.displayName}", InteractionPoint, FeedbackKind.Success);
            return true;
        }

        /// <summary>Moves items from the barn back to the player.</summary>
        public bool Retrieve(ItemData item, int qty)
        {
            if (item == null || qty <= 0 || !storage.Has(item, qty)) return Fail("Not in storage");
            storage.TryRemove(item, qty);
            playerInventory.Add(item, qty);
            FarmFeedback.Raise($"Took {qty} {item.displayName}", InteractionPoint, FeedbackKind.Success);
            return true;
        }

        bool Fail(string msg)
        {
            FarmFeedback.Raise(msg, InteractionPoint, FeedbackKind.Failure);
            return false;
        }
    }
}
