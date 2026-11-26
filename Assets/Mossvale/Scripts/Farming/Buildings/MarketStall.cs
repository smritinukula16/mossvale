using UnityEngine;

namespace Mossvale.Farming
{
    /// <summary>Market stall: sells harvested produce for coins at each item's ItemData.sellPrice.</summary>
    public class MarketStall : Building
    {
        public override string PromptDetail => Wallet.Instance ? $"Market - {Wallet.Instance.Coins} coins" : "Market";

        public static bool IsSellable(ItemData item) => item != null && item.category == ItemCategory.Produce && item.sellPrice > 0;

        public bool CanSell(ItemData item, int qty) => IsSellable(item) && qty > 0 && playerInventory.Has(item, qty);

        /// <summary>Sells qty units. Refuses (and changes nothing) if the item isn't sellable or the player lacks it.</summary>
        public bool Sell(ItemData item, int qty)
        {
            if (!IsSellable(item))
            {
                FarmFeedback.Raise("The market only buys harvested crops", InteractionPoint, FeedbackKind.Failure);
                return false;
            }
            if (qty <= 0 || !playerInventory.Has(item, qty))
            {
                FarmFeedback.Raise($"You don't have {Mathf.Max(1, qty)} {item.displayName} to sell", InteractionPoint, FeedbackKind.Failure);
                return false;
            }
            playerInventory.TryRemove(item, qty);
            int coins = item.sellPrice * qty;
            Wallet.Instance.Add(coins);
            FarmEvents.RaiseSold(item, qty, coins);
            FarmFeedback.Raise($"Sold {qty} {item.displayName}  +{coins} coins", InteractionPoint, FeedbackKind.Harvest);
            return true;
        }
    }
}
