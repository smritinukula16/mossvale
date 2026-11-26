using System.Collections.Generic;
using UnityEngine;

namespace Mossvale.Farming
{
    /// <summary>Scene-level farming hub: settings, growth speed and a registry of plots.</summary>
    [DefaultExecutionOrder(-100)]
    public class FarmManager : MonoBehaviour
    {
        public static FarmManager Instance { get; private set; }

        [SerializeField] FarmSettings settings;
        [Tooltip("Every item and crop, used to restore saved games.")]
        [SerializeField] ItemDatabase database;
        [Tooltip("Extra multiplier set by the dev tools (F5). Not saved.")]
        [SerializeField] float devSpeedMultiplier = 1f;

        static readonly List<FarmPlot> plots = new List<FarmPlot>();
        public static IReadOnlyList<FarmPlot> Plots => plots;

        public FarmSettings Settings => settings;
        public ItemDatabase Database => database;

        /// <summary>Growth multiplier from progression (upgrades, greenhouse). Set by FarmProgressionManager.</summary>
        public float BonusGrowthMultiplier { get; set; } = 1f;
        /// <summary>Extra watering-can capacity from progression rewards/upgrades.</summary>
        public int BonusCanCapacity { get; set; }

        public static ItemData FindItem(string id) => Instance != null && Instance.database ? Instance.database.FindItem(id) : null;
        public static CropData FindCrop(string id) => Instance != null && Instance.database ? Instance.database.FindCrop(id) : null;
        public float DevSpeedMultiplier { get => devSpeedMultiplier; set => devSpeedMultiplier = Mathf.Max(0.01f, value); }

        /// <summary>Combined growth speed applied to every plot.</summary>
        public static float GrowthMultiplier =>
            Instance == null ? 1f : (Instance.settings ? Instance.settings.growthSpeedMultiplier : 1f) * Instance.devSpeedMultiplier * Instance.BonusGrowthMultiplier;

        public static bool RequireWaterEachStage => Instance == null || Instance.settings == null || Instance.settings.requireWaterEachStage;
        public static float InteractionRange => Instance != null && Instance.settings ? Instance.settings.interactionRange : 2.2f;
        public static int WateringCanCapacity => (Instance != null && Instance.settings ? Instance.settings.wateringCanCapacity : 10) + (Instance != null ? Instance.BonusCanCapacity : 0);

        void Awake()
        {
            if (Instance != null && Instance != this) { Debug.LogWarning("Second FarmManager ignored", this); return; }
            Instance = this;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        internal static void Register(FarmPlot p) { if (!plots.Contains(p)) plots.Add(p); }
        internal static void Unregister(FarmPlot p) => plots.Remove(p);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { plots.Clear(); Instance = null; }
    }
}
