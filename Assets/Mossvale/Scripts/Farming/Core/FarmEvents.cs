using System;
using UnityEngine;

namespace Mossvale.Farming
{
    /// <summary>Game-wide farming events that progression, achievements and objectives listen to.</summary>
    public static class FarmEvents
    {
        public static event Action<FarmPlot> PlotTilled;
        public static event Action<FarmPlot, CropData> SeedPlanted;
        public static event Action<FarmPlot> PlotWatered;
        /// <summary>(plot, crop, produce units)</summary>
        public static event Action<FarmPlot, CropData, int> CropHarvested;
        /// <summary>(item, quantity, coins received)</summary>
        public static event Action<ItemData, int, int> ItemSold;
        /// <summary>(item, quantity) moved into barn storage</summary>
        public static event Action<ItemData, int> ItemStored;

        public static void RaiseTilled(FarmPlot p) => PlotTilled?.Invoke(p);
        public static void RaisePlanted(FarmPlot p, CropData c) => SeedPlanted?.Invoke(p, c);
        public static void RaiseWatered(FarmPlot p) => PlotWatered?.Invoke(p);
        public static void RaiseHarvested(FarmPlot p, CropData c, int n) => CropHarvested?.Invoke(p, c, n);
        public static void RaiseSold(ItemData i, int q, int coins) => ItemSold?.Invoke(i, q, coins);
        public static void RaiseStored(ItemData i, int q) => ItemStored?.Invoke(i, q);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            PlotTilled = null; SeedPlanted = null; PlotWatered = null; CropHarvested = null; ItemSold = null; ItemStored = null;
        }
    }
}
