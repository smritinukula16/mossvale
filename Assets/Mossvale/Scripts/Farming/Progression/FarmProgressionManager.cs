using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Mossvale.Farming
{
    /// <summary>
    /// Central farm progression: blue stars, farm level + rewards, stats, achievements, crop milestones,
    /// daily objectives, upgrades and area unlocks (beds, greenhouse). Listens to FarmEvents; saved via ISaveable.
    /// Stars have two numbers: LifetimeStars (drives the level, never decreases) and StarBalance (spendable on the Heart).
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class FarmProgressionManager : MonoBehaviour, ISaveable
    {
        public static FarmProgressionManager Instance { get; private set; }

        [SerializeField] ProgressionConfig config;
        [SerializeField] Inventory playerInventory;

        int lifetimeStars, starBalance, level = 1, salesStarsAwarded, devDayOffset;
        bool greenhouseUnlocked;
        readonly Dictionary<string, int> stats = new Dictionary<string, int>();
        readonly HashSet<string> achievements = new HashSet<string>();
        readonly HashSet<string> unlockedBeds = new HashSet<string>();
        readonly HashSet<string> upgrades = new HashSet<string>();
        readonly Dictionary<string, int> milestoneTiers = new Dictionary<string, int>();
        DailyState daily = new DailyState();
        float dailyCheck;
        Transform player;

        public event Action Changed;
        /// <summary>(new level, definition)</summary>
        public event Action<int, LevelDefinition> LeveledUp;
        public event Action<AchievementDefinition> AchievementUnlocked;
        /// <summary>(title, body) for unlocks, milestones and completed objectives.</summary>
        public event Action<string, string> Notification;

        public ProgressionConfig Config => config;
        public int LifetimeStars => lifetimeStars;
        public int StarBalance => starBalance;
        public int Level => level;
        public bool GreenhouseUnlocked => greenhouseUnlocked;
        public LevelDefinition CurrentLevel => config.GetLevel(level);
        public LevelDefinition NextLevel => config.GetLevel(level + 1);
        public bool IsMaxLevel => NextLevel == null;
        public int CompletedAchievements => achievements.Count;
        public bool HasAchievement(string id) => achievements.Contains(id);
        public bool OwnsUpgrade(string id) => upgrades.Contains(id);
        public bool IsBedUnlocked(string bedId) => unlockedBeds.Contains(bedId);

        /// <summary>Progress 0..1 from the current level's threshold to the next.</summary>
        public float LevelProgress01
        {
            get
            {
                if (IsMaxLevel) return 1f;
                int a = CurrentLevel?.starsRequired ?? 0, b = NextLevel.starsRequired;
                return Mathf.Clamp01((lifetimeStars - a) / (float)Mathf.Max(1, b - a));
            }
        }

        public int Stat(string key) => stats.TryGetValue(key, out var v) ? v : 0;

        // ------------------------------------------------------------------ lifecycle
        void Awake()
        {
            Instance = this;
            foreach (var b in config.startingBeds) unlockedBeds.Add(b);
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void OnEnable()
        {
            FarmEvents.CropHarvested += OnHarvested;
            FarmEvents.PlotWatered += OnWatered;
            FarmEvents.PlotTilled += OnTilled;
            FarmEvents.SeedPlanted += OnPlanted;
            FarmEvents.ItemSold += OnSold;
            FarmEvents.ItemStored += OnStored;
        }

        void OnDisable()
        {
            FarmEvents.CropHarvested -= OnHarvested;
            FarmEvents.PlotWatered -= OnWatered;
            FarmEvents.PlotTilled -= OnTilled;
            FarmEvents.SeedPlanted -= OnPlanted;
            FarmEvents.ItemSold -= OnSold;
            FarmEvents.ItemStored -= OnStored;
        }

        void Start()
        {
            var p = GameObject.FindWithTag("Player");
            player = p ? p.transform : null;
            stats[Stats.BedsUnlocked] = unlockedBeds.Count;
            EnsureDaily();
            ApplyWorldState();
        }

        void Update()
        {
            dailyCheck += Time.unscaledDeltaTime;
            if (dailyCheck > 30f) { dailyCheck = 0f; if (EnsureDaily()) Changed?.Invoke(); }
        }

        Vector3 PlayerPos => player ? player.position + Vector3.up * 2.4f : Vector3.zero;

        // ------------------------------------------------------------------ event handlers
        void OnHarvested(FarmPlot plot, CropData crop, int produce)
        {
            Add(Stats.Harvests, 1);
            Add(Stats.HarvestOf(crop.cropId), 1);
            Add(Stats.ProduceTotal, produce);
            stats[Stats.KindsHarvested] = config.milestones.Select(m => m.crop).Where(c => c).Count(c => Stat(Stats.HarvestOf(c.cropId)) > 0);
            AwardStars(config.starsPerHarvest, $"Harvested {crop.displayName}", plot.InteractionPoint + Vector3.up * 0.6f);
            Evaluate();
        }

        void OnWatered(FarmPlot p) { Add(Stats.Waterings, 1); Evaluate(); }
        void OnTilled(FarmPlot p) { Add(Stats.Tills, 1); Evaluate(); }
        void OnPlanted(FarmPlot p, CropData c) { Add(Stats.Plantings, 1); Evaluate(); }
        void OnStored(ItemData item, int qty) { Add(Stats.ItemsStored, qty); Evaluate(); }

        void OnSold(ItemData item, int qty, int coins)
        {
            Add(Stats.ItemsSold, qty);
            Add(Stats.CoinsFromSales, coins);
            int due = Stat(Stats.CoinsFromSales) / Mathf.Max(1, config.coinsPerStar) - salesStarsAwarded;
            if (due > 0)
            {
                salesStarsAwarded += due;
                AwardStars(due, "Market sales");
            }
            Evaluate();
        }

        void Add(string key, int n) => stats[key] = Stat(key) + n;

        /// <summary>Sets a stat (if higher) and re-checks achievements; e.g. heart_stage.</summary>
        public void ReportStat(string key, int value)
        {
            if (value <= Stat(key)) return;
            stats[key] = value;
            Evaluate();
            Changed?.Invoke();
        }

        // ------------------------------------------------------------------ stars + levels
        public void AwardStars(int amount, string reason, Vector3? where = null)
        {
            if (amount <= 0) return;
            lifetimeStars += amount;
            starBalance += amount;
            FarmFeedback.Raise($"+{amount} {Glyphs.Star}  {reason}", where ?? PlayerPos, FeedbackKind.Star);
            CheckLevel();
            Changed?.Invoke();
            SaveManager.MarkDirty();
        }

        public bool TrySpendStars(int amount)
        {
            if (amount < 0 || amount > starBalance) return false;
            starBalance -= amount;
            Changed?.Invoke();
            SaveManager.MarkDirty();
            return true;
        }

        void CheckLevel()
        {
            int target = config.LevelFor(lifetimeStars);
            while (level < target)
            {
                // Announce first, then grant: a reward (e.g. a bed unlock's stars) can trigger the next level
                // re-entrantly, and each level must be reported once, in order, with its own definition.
                int reached = ++level;
                var def = config.GetLevel(reached);
                LeveledUp?.Invoke(reached, def);
                Debug.Log($"[MossvaleProgress] Level up -> {reached}");
                if (def != null) foreach (var r in def.rewards) GrantReward(r);
                ApplyWorldState();
                target = config.LevelFor(lifetimeStars);
            }
        }

        void GrantReward(RewardDefinition r)
        {
            switch (r.type)
            {
                case RewardType.Coins: if (Wallet.Instance) Wallet.Instance.Add(r.amount); break;
                case RewardType.UnlockBed: UnlockBed(r.targetId); break;
                case RewardType.GiveItem: if (r.item && playerInventory) playerInventory.Add(r.item, r.amount); break;
                // WateringCanCapacity / BarnCapacity are derived from the level each frame (see bonuses), Message is display-only.
            }
        }

        // ------------------------------------------------------------------ bonuses (derived from level + upgrades)
        int SumLevelRewards(RewardType type) =>
            config.levels.Where(l => l.level <= level).SelectMany(l => l.rewards).Where(r => r.type == type).Sum(r => r.amount);

        int SumUpgrades(UpgradeEffect effect) =>
            config.upgrades.Where(u => upgrades.Contains(u.id) && u.effect == effect).Sum(u => u.amount);

        public int CanCapacityBonus => SumLevelRewards(RewardType.WateringCanCapacity) + SumUpgrades(UpgradeEffect.WateringCanCapacity);
        public int BarnCapacity => config.barnBaseCapacity + SumLevelRewards(RewardType.BarnCapacity) + SumUpgrades(UpgradeEffect.BarnCapacity);
        public float GrowthBonus => (1f + SumUpgrades(UpgradeEffect.GrowthSpeedPercent) / 100f) * (greenhouseUnlocked ? config.greenhouseGrowthBonus : 1f);

        /// <summary>Pushes unlocks and bonuses into the world (bed locks, growth speed, can capacity).</summary>
        public void ApplyWorldState()
        {
            if (FarmManager.Instance)
            {
                FarmManager.Instance.BonusGrowthMultiplier = GrowthBonus;
                FarmManager.Instance.BonusCanCapacity = CanCapacityBonus;
            }
            foreach (var bed in FindObjectsByType<BedLock>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                bool open = unlockedBeds.Contains(bed.BedId);
                int lv = config.LevelUnlockingBed(bed.BedId);
                bed.SetLocked(!open, lv > 0 ? $"Unlocks at Farm Level {lv}" : "Locked");
            }
        }

        public int UnlockedPlotCount => FarmManager.Plots.Count(p => !p.IsLocked);

        public void UnlockBed(string bedId)
        {
            if (string.IsNullOrEmpty(bedId) || !unlockedBeds.Add(bedId)) return;
            stats[Stats.BedsUnlocked] = unlockedBeds.Count;
            ApplyWorldState();
            var bed = FindObjectsByType<BedLock>(FindObjectsSortMode.None).FirstOrDefault(b => b.BedId == bedId);
            if (bed) bed.PlayUnlockEffect();
            Notification?.Invoke("New farm area!", $"The {bed?.DisplayName ?? bedId} bed is ready to plant");
            AwardStars(config.starsPerAreaUnlock, "New farm area", bed ? bed.transform.position + Vector3.up * 1.5f : (Vector3?)null);
            Evaluate();
        }

        // ------------------------------------------------------------------ greenhouse
        public bool GreenhouseRequirementsMet =>
            level >= config.greenhouseRequiredLevel && lifetimeStars >= config.greenhouseRequiredStars;

        public bool TryUnlockGreenhouse()
        {
            if (greenhouseUnlocked) return false;
            if (!GreenhouseRequirementsMet)
            {
                FarmFeedback.Raise($"Needs Farm Level {config.greenhouseRequiredLevel} and {config.greenhouseRequiredStars} {Glyphs.Star}", PlayerPos, FeedbackKind.Failure);
                return false;
            }
            greenhouseUnlocked = true;
            stats[Stats.GreenhouseUnlocked] = 1;
            ApplyWorldState();
            Notification?.Invoke("Greenhouse unlocked!", $"All crops grow {Mathf.RoundToInt((config.greenhouseGrowthBonus - 1f) * 100)}% faster");
            AwardStars(config.starsPerAreaUnlock, "Greenhouse unlocked");
            Evaluate();
            Changed?.Invoke();
            return true;
        }

        // ------------------------------------------------------------------ upgrades
        public bool CanBuyUpgrade(UpgradeDefinition u, out string reason)
        {
            reason = null;
            if (upgrades.Contains(u.id)) { reason = "Owned"; return false; }
            if (level < u.requiredLevel) { reason = $"Needs Level {u.requiredLevel}"; return false; }
            if (Wallet.Instance == null || !Wallet.Instance.CanAfford(u.cost)) { reason = "Not enough coins"; return false; }
            return true;
        }

        public bool TryBuyUpgrade(UpgradeDefinition u)
        {
            if (!CanBuyUpgrade(u, out var reason))
            {
                FarmFeedback.Raise($"{u.title}: {reason}", PlayerPos, FeedbackKind.Failure);
                return false;
            }
            Wallet.Instance.TrySpend(u.cost);
            upgrades.Add(u.id);
            ApplyWorldState();
            Notification?.Invoke("Upgrade bought", u.title);
            FarmFeedback.Raise($"Upgrade: {u.title}", PlayerPos, FeedbackKind.Success);
            Changed?.Invoke();
            SaveManager.MarkDirty();
            return true;
        }

        // ------------------------------------------------------------------ achievements, milestones, dailies
        /// <summary>Checks one-time achievements, crop milestones and daily objectives. Each is rewarded once.</summary>
        void Evaluate()
        {
            bool again = true;
            int guard = 0;
            while (again && guard++ < 20)
            {
                again = false;
                foreach (var a in config.achievements)
                {
                    if (achievements.Contains(a.id) || Stat(a.statKey) < a.threshold) continue;
                    achievements.Add(a.id);
                    AchievementUnlocked?.Invoke(a);
                    if (a.coins > 0 && Wallet.Instance) Wallet.Instance.Add(a.coins);
                    AwardStars(a.stars, "Achievement: " + a.title);
                    again = true;
                }
                foreach (var m in config.milestones)
                {
                    while (true)
                    {
                        // Re-read every step: awarding stars can level up -> unlock a bed -> re-enter Evaluate().
                        milestoneTiers.TryGetValue(m.id, out int tier);
                        if (tier >= m.thresholds.Length || Stat(m.statKey) < m.thresholds[tier]) break;
                        milestoneTiers[m.id] = tier + 1;   // mark first so it can never be awarded twice
                        int stars = tier < m.stars.Length ? m.stars[tier] : 1;
                        Notification?.Invoke("Milestone reached!", $"{m.title}: {m.thresholds[tier]}");
                        AwardStars(stars, $"{m.title} milestone");
                        again = true;
                    }
                }
                again |= CheckDaily();
            }
            Changed?.Invoke();
            SaveManager.MarkDirty();
        }

        /// <summary>(track, tier reached, next threshold or -1, current value)</summary>
        public IEnumerable<(MilestoneTrack track, int tier, int next, int value)> Milestones =>
            config.milestones.Select(m =>
            {
                milestoneTiers.TryGetValue(m.id, out int t);
                return (m, t, t < m.thresholds.Length ? m.thresholds[t] : -1, Stat(m.statKey));
            });

        [Serializable] class DailyState { public string date; public List<string> ids = new List<string>(); public List<int> baselines = new List<int>(); public List<bool> done = new List<bool>(); }

        string Today => DateTime.Today.AddDays(devDayOffset).ToString("yyyy-MM-dd");

        /// <summary>Rolls new daily objectives when the (local) date changes. Returns true if it rolled.</summary>
        bool EnsureDaily()
        {
            if (daily.date == Today && daily.ids.Count > 0) return false;
            daily = new DailyState { date = Today };
            var pool = config.dailyPool.ToList();
            uint seed = 2166136261;
            foreach (char ch in Today) seed = (seed ^ ch) * 16777619;   // stable FNV hash of the date
            var rng = new System.Random((int)(seed & 0x7fffffff));
            for (int i = 0; i < config.dailyObjectivesPerDay && pool.Count > 0; i++)
            {
                var pick = pool[rng.Next(pool.Count)];
                pool.Remove(pick);
                daily.ids.Add(pick.id);
                daily.baselines.Add(Stat(pick.statKey));
                daily.done.Add(false);
            }
            SaveManager.MarkDirty();
            return true;
        }

        bool CheckDaily()
        {
            bool any = false;
            for (int i = 0; i < daily.ids.Count; i++)
            {
                if (daily.done[i]) continue;
                var def = config.dailyPool.FirstOrDefault(d => d.id == daily.ids[i]);
                if (def == null || Stat(def.statKey) - daily.baselines[i] < def.amount) continue;
                daily.done[i] = true;
                Notification?.Invoke("Daily objective complete!", def.title);
                if (def.rewardCoins > 0 && Wallet.Instance) Wallet.Instance.Add(def.rewardCoins);
                AwardStars(def.rewardStars, "Daily: " + def.title);
                any = true;
            }
            return any;
        }

        /// <summary>(definition, progress, done) for today's objectives.</summary>
        public IEnumerable<(DailyObjectiveDefinition def, int progress, bool done)> DailyObjectives
        {
            get
            {
                for (int i = 0; i < daily.ids.Count; i++)
                {
                    var def = config.dailyPool.FirstOrDefault(d => d.id == daily.ids[i]);
                    if (def == null) continue;
                    yield return (def, Mathf.Min(def.amount, Stat(def.statKey) - daily.baselines[i]), daily.done[i]);
                }
            }
        }

        // ------------------------------------------------------------------ dev / tests
        public void DevAddStars(int n) => AwardStars(n, "Dev");
        public void DevNewDay() { devDayOffset++; daily = new DailyState(); EnsureDaily(); Changed?.Invoke(); }
        public void DevUnlockAllBeds()
        {
            foreach (var b in FindObjectsByType<BedLock>(FindObjectsInactive.Include, FindObjectsSortMode.None)) unlockedBeds.Add(b.BedId);
            stats[Stats.BedsUnlocked] = unlockedBeds.Count;
            ApplyWorldState();
            Changed?.Invoke();
        }

        // ------------------------------------------------------------------ save
        [Serializable]
        class State
        {
            public int lifetimeStars, starBalance, level = 1, salesStarsAwarded, devDayOffset;
            public bool greenhouse;
            public List<string> statKeys = new List<string>();
            public List<int> statValues = new List<int>();
            public List<string> achievements = new List<string>(), beds = new List<string>(), upgrades = new List<string>();
            public List<string> milestoneIds = new List<string>();
            public List<int> milestoneTiers = new List<int>();
            public DailyState daily;
        }

        public string SaveKey => "progression";
        public int RestoreOrder => 10;

        public string CaptureState()
        {
            var s = new State
            {
                lifetimeStars = lifetimeStars, starBalance = starBalance, level = level, salesStarsAwarded = salesStarsAwarded,
                devDayOffset = devDayOffset, greenhouse = greenhouseUnlocked, daily = daily,
                statKeys = stats.Keys.ToList(), statValues = stats.Values.ToList(),
                achievements = achievements.ToList(), beds = unlockedBeds.ToList(), upgrades = upgrades.ToList(),
                milestoneIds = milestoneTiers.Keys.ToList(), milestoneTiers = milestoneTiers.Values.ToList(),
            };
            return JsonUtility.ToJson(s);
        }

        public void RestoreState(string json)
        {
            var s = JsonUtility.FromJson<State>(json);
            lifetimeStars = s.lifetimeStars; starBalance = s.starBalance; level = Mathf.Max(1, s.level);
            salesStarsAwarded = s.salesStarsAwarded; devDayOffset = s.devDayOffset; greenhouseUnlocked = s.greenhouse;
            stats.Clear();
            for (int i = 0; i < s.statKeys.Count && i < s.statValues.Count; i++) stats[s.statKeys[i]] = s.statValues[i];
            achievements.Clear(); foreach (var a in s.achievements) achievements.Add(a);
            unlockedBeds.Clear(); foreach (var b in config.startingBeds) unlockedBeds.Add(b); foreach (var b in s.beds) unlockedBeds.Add(b);
            upgrades.Clear(); foreach (var u in s.upgrades) upgrades.Add(u);
            milestoneTiers.Clear();
            for (int i = 0; i < s.milestoneIds.Count && i < s.milestoneTiers.Count; i++) milestoneTiers[s.milestoneIds[i]] = s.milestoneTiers[i];
            daily = s.daily ?? new DailyState();
            EnsureDaily();
            ApplyWorldState();
            Changed?.Invoke();
        }
    }
}
