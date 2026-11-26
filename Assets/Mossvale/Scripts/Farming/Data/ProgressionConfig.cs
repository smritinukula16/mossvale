using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Mossvale.Farming
{
    public enum RewardType { Coins, UnlockBed, WateringCanCapacity, BarnCapacity, GiveItem, Message }
    public enum UpgradeEffect { WateringCanCapacity, BarnCapacity, GrowthSpeedPercent }

    /// <summary>Stat keys tracked by FarmProgressionManager (used by achievements, milestones and daily objectives).</summary>
    public static class Stats
    {
        public const string Harvests = "harvests";
        public const string ProduceTotal = "produce_total";
        public const string KindsHarvested = "kinds_harvested";
        public const string ItemsSold = "items_sold";
        public const string CoinsFromSales = "coins_from_sales";
        public const string Waterings = "waterings";
        public const string Tills = "tills";
        public const string Plantings = "plantings";
        public const string ItemsStored = "items_stored";
        public const string HeartStage = "heart_stage";
        public const string BedsUnlocked = "beds_unlocked";
        public const string GreenhouseUnlocked = "greenhouse_unlocked";
        public static string HarvestOf(string cropId) => "harvest_" + cropId;
    }

    [Serializable]
    public class RewardDefinition
    {
        public RewardType type;
        public int amount;
        [Tooltip("UnlockBed: bed id (carrot / tomato / wheat).")] public string targetId;
        public ItemData item;
        [Tooltip("Shown in the UI. Leave empty to generate one.")] public string description;
    }

    [Serializable]
    public class LevelDefinition
    {
        public int level = 1;
        [Tooltip("Lifetime blue stars needed to reach this level.")] public int starsRequired;
        public string title = "Farm";
        public List<RewardDefinition> rewards = new List<RewardDefinition>();
    }

    [Serializable]
    public class AchievementDefinition
    {
        public string id, title, description, statKey;
        public int threshold = 1, stars = 2, coins;
    }

    [Serializable]
    public class MilestoneTrack
    {
        public string id, title, statKey;
        public CropData crop;
        public int[] thresholds = { 5, 15, 40, 100 };
        public int[] stars = { 2, 3, 5, 8 };
    }

    [Serializable]
    public class DailyObjectiveDefinition
    {
        public string id, title, statKey;
        public int amount = 5, rewardStars = 2, rewardCoins = 15;
    }

    [Serializable]
    public class UpgradeDefinition
    {
        public string id, title, description;
        public int cost = 50, requiredLevel = 1;
        public UpgradeEffect effect;
        public int amount;
    }

    [Serializable]
    public class HeartStageDefinition
    {
        public string title, description;
        [Tooltip("Blue stars spent to restore to this stage (stage 0 = starting state, ignored).")] public int starCost;
        public int requiredLevel = 1;
    }

    /// <summary>All tunable progression data: levels, rewards, stars, achievements, milestones, dailies, upgrades, greenhouse, Heart.</summary>
    [CreateAssetMenu(menuName = "Mossvale/Farming/Progression Config", fileName = "ProgressionConfig")]
    public class ProgressionConfig : ScriptableObject
    {
        [Header("Levels (sorted by level)")]
        public List<LevelDefinition> levels = new List<LevelDefinition>();

        [Header("Blue star rules")]
        public int starsPerHarvest = 1;
        [Tooltip("1 star for every N coins earned from sales.")] public int coinsPerStar = 30;
        public int starsPerAreaUnlock = 3;

        [Header("Farm areas")]
        [Tooltip("Bed ids unlocked at the start.")] public List<string> startingBeds = new List<string> { "carrot" };
        public int barnBaseCapacity = 30;

        [Header("Greenhouse")]
        public int greenhouseRequiredLevel = 4;
        [Tooltip("Lifetime stars earned (not spent) required.")] public int greenhouseRequiredStars = 35;
        [Tooltip("Growth speed multiplier for all crops once the greenhouse is unlocked.")] public float greenhouseGrowthBonus = 1.25f;
        public int nurserySeedsPerProduce = 3;

        [Header("Achievements, milestones, daily objectives, upgrades")]
        public List<AchievementDefinition> achievements = new List<AchievementDefinition>();
        public List<MilestoneTrack> milestones = new List<MilestoneTrack>();
        public int dailyObjectivesPerDay = 3;
        public List<DailyObjectiveDefinition> dailyPool = new List<DailyObjectiveDefinition>();
        public List<UpgradeDefinition> upgrades = new List<UpgradeDefinition>();

        [Header("Heart of Mossvale (index 0 = starting stage)")]
        public List<HeartStageDefinition> heartStages = new List<HeartStageDefinition>();

        public int MaxLevel => levels.Count == 0 ? 1 : levels.Max(l => l.level);
        public LevelDefinition GetLevel(int level) => levels.FirstOrDefault(l => l.level == level);

        public int LevelFor(int lifetimeStars)
        {
            int best = 1;
            foreach (var l in levels) if (lifetimeStars >= l.starsRequired && l.level > best) best = l.level;
            return best;
        }

        /// <summary>Level whose rewards unlock the given bed, or 0.</summary>
        public int LevelUnlockingBed(string bedId) =>
            levels.Where(l => l.rewards.Any(r => r.type == RewardType.UnlockBed && r.targetId == bedId)).Select(l => l.level).DefaultIfEmpty(0).Min();

        public static string Describe(RewardDefinition r)
        {
            if (!string.IsNullOrEmpty(r.description)) return r.description;
            switch (r.type)
            {
                case RewardType.Coins: return $"+{r.amount} coins";
                case RewardType.UnlockBed: return $"Unlocks the {Capitalise(r.targetId)} bed";
                case RewardType.WateringCanCapacity: return $"Watering can +{r.amount}";
                case RewardType.BarnCapacity: return $"Barn storage +{r.amount}";
                case RewardType.GiveItem: return r.item ? $"+{r.amount} {r.item.displayName}" : "Gift";
                default: return r.description ?? "";
            }
        }

        static string Capitalise(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s.Substring(1);
    }
}
