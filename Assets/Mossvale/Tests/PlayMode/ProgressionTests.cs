using System;
using System.Collections;
using System.IO;
using System.Linq;
using Mossvale.Farming;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Mossvale.Tests
{
    /// Play Mode tests for buildings, blue-star progression, the Heart of Mossvale and save/load.
    public class ProgressionTests
    {
        GameObject player;
        PlayerToolController tools;
        PlayerInteractor interactor;
        Inventory inv;
        FarmProgressionManager prog;
        Wallet wallet;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Application.targetFrameRate = 60;
            SaveManager.SlotOverride = "tests";
            SaveManager.DeleteSlot("tests");
            GameplayInput.Blocked = false;
            yield return Load();
        }

        IEnumerator Load()
        {
            SceneManager.LoadScene("Mossvale_Main");
            yield return null;
            yield return null;
            player = GameObject.FindWithTag("Player");
            tools = player.GetComponent<PlayerToolController>();
            interactor = player.GetComponent<PlayerInteractor>();
            inv = player.GetComponent<Inventory>();
            prog = FarmProgressionManager.Instance;
            wallet = Wallet.Instance;
            Assert.IsNotNull(prog, "FarmProgressionManager missing");
            Assert.IsNotNull(wallet, "Wallet missing");
            Assert.IsNotNull(SaveManager.Instance, "SaveManager missing");
        }

        [UnityTearDown]
        public IEnumerator CloseUI()
        {
            if (UIPanel.Current != null) UIPanel.Current.Close();
            GameplayInput.Blocked = false;
            yield return null;
        }

        // ------------------------------------------------------------------ helpers
        static T Find<T>() where T : UnityEngine.Object => UnityEngine.Object.FindFirstObjectByType<T>();
        ItemData Item(string id) => FarmManager.FindItem(id);
        CropData Crop(string id) => FarmManager.FindCrop(id);

        void Teleport(Vector3 pos, Vector3 lookAt)
        {
            var cc = player.GetComponent<CharacterController>();
            cc.enabled = false;
            player.transform.position = new Vector3(pos.x, 0.02f, pos.z);
            var d = lookAt - pos; d.y = 0;
            if (d.sqrMagnitude > 0.01f) player.transform.rotation = Quaternion.LookRotation(d);
            cc.enabled = true;
        }

        /// Puts the player just outside the object's bounds on the given side.
        void StandBeside(Interactable it, Vector3 side)
        {
            var b = it.WorldBounds;
            var edge = b.ClosestPoint(b.center + side.normalized * 100f);
            Teleport(edge + side.normalized * 0.7f, b.center);
        }

        static bool PanelShows(UIPanel p, string text) =>
            p.GetComponentsInChildren<TMP_Text>(true).Any(t => t.gameObject.activeInHierarchy && t.text.Contains(text));

        int DailyStarsEarned() => prog.DailyObjectives.Where(d => d.done).Sum(d => d.def.rewardStars);

        FarmPlot Plot(string bed, string plot) => FarmManager.Plots.First(p => p.name == plot && p.transform.parent.parent.name == bed);

        IEnumerator HarvestOnce(FarmPlot plot)
        {
            plot.DevSetState(PlotState.Grown, Crop("carrot"), CropStage.Full);
            tools.SelectTool(ToolType.Basket);
            Assert.IsTrue(interactor.TryInteract(plot), "Harvest failed: " + FarmFeedback.LastMessage);
            yield return null;
        }

        // ------------------------------------------------------------------ buildings open the right UI
        [UnityTest]
        public IEnumerator ClickingTheFarmhouseOpensTheFarmhouseUI()
        {
            var house = UnityEngine.Object.FindObjectsByType<Building>(FindObjectsSortMode.None).First(b => b.DisplayName == "Farmhouse");
            StandBeside(house, Vector3.back);
            yield return null;
            var b = house.WorldBounds;
            Assert.IsTrue(Physics.Raycast(new Ray(b.center + Vector3.up * 30f, Vector3.down), out var hit, 60f));
            Assert.AreEqual(house, hit.collider.GetComponentInParent<Interactable>(), "Click ray should hit the farmhouse");
            interactor.HandleClick(hit);
            yield return null;
            var panel = Find<FarmhousePanel>();
            Assert.IsTrue(panel.IsOpen, "Farmhouse panel did not open: " + FarmFeedback.LastMessage);
            Assert.AreEqual(panel, UIPanel.Current);
            Assert.IsTrue(GameplayInput.Blocked, "World input should pause while a panel is open");
            Assert.IsTrue(PanelShows(panel, "Farm Level 1"));
            Assert.IsTrue(PanelShows(panel, "Crops harvested"));
            Assert.IsTrue(PanelShows(panel, "Plots unlocked"));
            Assert.IsTrue(PanelShows(panel, "Next unlock"));
            panel.SelectTab(1); yield return null;
            Assert.IsTrue(PanelShows(panel, "First Harvest"), "Achievements tab");
            panel.SelectTab(2); yield return null;
            Assert.IsTrue(PanelShows(panel, "Today's objectives"), "Daily tab");
            panel.SelectTab(3); yield return null;
            Assert.IsTrue(PanelShows(panel, "Bigger Watering Can"), "Upgrades tab");

            var close = panel.GetComponentsInChildren<UnityEngine.UI.Button>().First(x => x.name == "CloseButton");
            close.onClick.Invoke();
            yield return new WaitForSeconds(0.3f);
            Assert.IsFalse(panel.IsOpen);
            Assert.IsFalse(GameplayInput.Blocked);
        }

        [UnityTest]
        public IEnumerator EachBuildingOpensItsOwnPanelAndOnlyOneIsOpen()
        {
            (string name, Type panel, Vector3 side)[] cases =
            {
                ("Barn", typeof(BarnPanel), Vector3.back), ("Market Stall", typeof(MarketPanel), Vector3.right),
                ("Greenhouse", typeof(GreenhousePanel), Vector3.left), ("Heart of Mossvale", typeof(HeartPanel), Vector3.right),
            };
            foreach (var c in cases)
            {
                var bld = UnityEngine.Object.FindObjectsByType<Building>(FindObjectsSortMode.None).First(b => b.DisplayName == c.name);
                StandBeside(bld, c.side);
                yield return null;
                Assert.IsTrue(interactor.InRange(bld), c.name + " should be in range");
                var prompt = bld.GetPrompt(tools);
                Assert.IsTrue(prompt.canPerform, c.name + " prompt should be available");
                Assert.IsFalse(string.IsNullOrEmpty(prompt.action), c.name + " needs its own prompt");
                Assert.IsTrue(interactor.TryInteract(bld), c.name + " interaction failed: " + FarmFeedback.LastMessage);
                yield return null;
                Assert.IsNotNull(UIPanel.Current);
                Assert.AreEqual(c.panel, UIPanel.Current.GetType(), c.name + " opened the wrong panel");
                Assert.AreEqual(1, UnityEngine.Object.FindObjectsByType<UIPanel>(FindObjectsSortMode.None).Count(p => p.IsOpen), "Only one panel may be open");
            }
            // Out of range: walking away and interacting fails.
            var barn = Find<BarnStorage>();
            Teleport(barn.WorldBounds.center + new Vector3(0, 0, -25f), barn.WorldBounds.center);
            UIPanel.Current.Close();
            Assert.IsFalse(interactor.TryInteract(barn));
            StringAssert.Contains("Too far", FarmFeedback.LastMessage);
        }

        // ------------------------------------------------------------------ barn
        [UnityTest]
        public IEnumerator BarnStoresAndRetrievesWithinCapacity()
        {
            var barn = Find<BarnStorage>();
            StandBeside(barn, Vector3.back);
            yield return null;
            Assert.IsTrue(interactor.TryInteract(barn));
            yield return null;
            var panel = Find<BarnPanel>();
            Assert.IsTrue(panel.IsOpen);
            Assert.IsTrue(PanelShows(panel, "In the barn"));
            Assert.IsTrue(PanelShows(panel, $"0 / {barn.Capacity}"), "Capacity should be shown");

            var seeds = Item("carrot_seeds");
            int s0 = inv.Count(seeds);
            Assert.IsTrue(barn.Store(seeds, 2));
            Assert.AreEqual(s0 - 2, inv.Count(seeds));
            Assert.AreEqual(2, barn.Storage.Count(seeds));
            Assert.AreEqual(2, barn.Used);
            Assert.IsFalse(barn.Store(seeds, 99), "Storing more than you own must fail");
            Assert.IsFalse(barn.Store(Item("hoe"), 1), "Tools can't be stored");

            var carrot = Item("carrot");
            inv.Add(carrot, 100);
            Assert.IsFalse(barn.Store(carrot, 100), "Over capacity must fail");
            Assert.AreEqual(100, inv.Count(carrot), "Failed store must not move items");
            int free = barn.Free;
            Assert.IsTrue(barn.Store(carrot, free));
            Assert.AreEqual(0, barn.Free);
            Assert.IsFalse(barn.Store(carrot, 1));
            StringAssert.Contains("full", FarmFeedback.LastMessage);
            yield return null;
            Assert.IsTrue(PanelShows(panel, $"{barn.Capacity} / {barn.Capacity}"), "Panel should refresh capacity");

            Assert.IsTrue(barn.Retrieve(carrot, 5));
            Assert.AreEqual(100 - free + 5, inv.Count(carrot));
            Assert.IsFalse(barn.Retrieve(carrot, 999), "Can't take more than stored");
            Assert.IsTrue(barn.Retrieve(seeds, 2));
            Assert.AreEqual(s0, inv.Count(seeds));
            Assert.AreEqual(2 + free, prog.Stat(Stats.ItemsStored));
        }

        // ------------------------------------------------------------------ market
        [UnityTest]
        public IEnumerator MarketSellingUpdatesInventoryCoinsAndStars()
        {
            var market = Find<MarketStall>();
            StandBeside(market, Vector3.right);
            yield return null;
            Assert.IsTrue(interactor.TryInteract(market));
            yield return null;
            var panel = Find<MarketPanel>();
            Assert.IsTrue(panel.IsOpen);
            var carrot = Item("carrot");
            Assert.IsTrue(PanelShows(panel, $"{carrot.sellPrice} "), "Prices should be displayed");

            inv.Add(carrot, 5);
            int c0 = wallet.Coins, st0 = prog.LifetimeStars;
            Assert.IsTrue(market.Sell(carrot, 3));
            Assert.AreEqual(2, inv.Count(carrot));
            Assert.AreEqual(c0 + 3 * carrot.sellPrice, wallet.Coins);
            Assert.AreEqual(3, prog.Stat(Stats.ItemsSold));
            Assert.IsTrue(prog.HasAchievement("market_day"));

            Assert.IsFalse(market.Sell(carrot, 5), "Selling more than you have must fail");
            Assert.AreEqual(2, inv.Count(carrot));
            Assert.AreEqual(c0 + 3 * carrot.sellPrice, wallet.Coins, "Failed sale must not pay");
            Assert.IsFalse(market.Sell(Item("carrot_seeds"), 1), "Seeds are not sellable");
            Assert.IsFalse(market.Sell(Item("hoe"), 1), "Tools are not sellable");
            Assert.IsFalse(market.Sell(Item("tomato"), 1), "Item not owned");

            Assert.IsTrue(market.Sell(carrot, 2));
            int coinsFromSales = 5 * carrot.sellPrice;
            int saleStars = coinsFromSales / prog.Config.coinsPerStar;
            int achStars = prog.Config.achievements.First(a => a.id == "market_day").stars;
            int expected = st0 + saleStars + achStars + DailyStarsEarned();
            // Crossing Level 2 unlocks the tomato bed, which pays the new-area stars too (intended cascade).
            if (prog.Config.LevelFor(expected) >= 2) expected += prog.Config.starsPerAreaUnlock;
            Assert.AreEqual(expected, prog.LifetimeStars, "Sales stars + one Market Day achievement (+ area unlock on level-up)");
            yield return null;
            Assert.IsTrue(PanelShows(panel, $"{wallet.Coins} coins"), "Coins shown after sale");
        }

        // ------------------------------------------------------------------ stars, achievements, milestones
        [UnityTest]
        public IEnumerator HarvestStarsAchievementOnceAndCropMilestone()
        {
            var plot = Plot("FarmBed_Carrot", "Plot_0_1");
            var b = plot.WorldBounds;
            Teleport(new Vector3(b.center.x, 0, plot.transform.parent.parent.GetComponentInChildren<BoxCollider>().bounds.min.z - 0.5f), b.center);
            yield return null;
            var cfg = prog.Config;
            int first = cfg.achievements.First(a => a.id == "first_harvest").stars;
            int area = cfg.starsPerAreaUnlock;

            yield return HarvestOnce(plot);
            Assert.AreEqual(1 + first + DailyStarsEarned(), prog.LifetimeStars, "1 star per harvest + First Harvest");
            Assert.IsTrue(prog.HasAchievement("first_harvest"));
            yield return HarvestOnce(plot);
            Assert.AreEqual(2 + first + DailyStarsEarned(), prog.LifetimeStars, "First Harvest must not be awarded twice");

            for (int i = 0; i < 3; i++) yield return HarvestOnce(plot);
            // 5 harvests: +5, First Harvest, Level 2 at 5 stars unlocks the tomato bed (+area stars), carrot milestone (5) +2.
            int milestone = cfg.milestones.First(m => m.id == "ms_carrot").stars[0];
            Assert.AreEqual(5, prog.Stat(Stats.HarvestOf("carrot")));
            Assert.AreEqual(5 + first + area + milestone + DailyStarsEarned(), prog.LifetimeStars);
            Assert.AreEqual(cfg.LevelFor(prog.LifetimeStars), prog.Level);
            Assert.AreEqual(prog.LifetimeStars, prog.StarBalance, "Nothing spent yet");
            var ms = prog.Milestones.First(m => m.track.id == "ms_carrot");
            Assert.AreEqual(1, ms.tier);
            Assert.AreEqual(15, ms.next);
        }

        [UnityTest]
        public IEnumerator LevelsUnlockAtThresholdsWithRewards()
        {
            var cfg = prog.Config;
            var tomatoPlot = Plot("FarmBed_Tomato", "Plot_0_0");
            Assert.IsTrue(tomatoPlot.IsLocked, "Tomato bed starts locked");
            Assert.AreEqual(9, prog.UnlockedPlotCount);
            tools.SelectTool(ToolType.Hoe);
            Assert.AreEqual(false, tomatoPlot.GetPrompt(tools).canPerform);
            StringAssert.Contains("Level 2", tomatoPlot.GetPrompt(tools).detail);

            int levelUps = 0;
            prog.LeveledUp += (l, d) => levelUps++;
            int coins0 = wallet.Coins;
            prog.DevAddStars(cfg.GetLevel(2).starsRequired - 1);
            Assert.AreEqual(1, prog.Level, "One star short of Level 2");
            prog.DevAddStars(1);
            Assert.AreEqual(2, prog.Level);
            Assert.AreEqual(1, levelUps);
            Assert.IsFalse(tomatoPlot.IsLocked, "Level 2 unlocks the tomato bed");
            Assert.AreEqual(18, prog.UnlockedPlotCount);
            Assert.AreEqual(coins0 + 20, wallet.Coins, "Level 2 coin reward");

            prog.DevAddStars(cfg.GetLevel(3).starsRequired - prog.LifetimeStars);
            Assert.AreEqual(3, prog.Level);
            Assert.IsFalse(Plot("FarmBed_Wheat", "Plot_0_0").IsLocked, "Level 3 unlocks the wheat bed");
            Assert.AreEqual(27, prog.UnlockedPlotCount);
            Assert.IsTrue(prog.HasAchievement("growing_fields"));

            int cap0 = tools.WaterCapacity;
            prog.DevAddStars(Mathf.Max(0, cfg.GetLevel(4).starsRequired - prog.LifetimeStars));
            Assert.AreEqual(cfg.LevelFor(prog.LifetimeStars), prog.Level);
            Assert.GreaterOrEqual(prog.Level, 4);
            Assert.AreEqual(cap0 + 3, tools.WaterCapacity, "Level 4 watering can reward");
            Assert.AreEqual(prog.Level - 1, levelUps, "One level-up event per level");

            yield return null;
            var hud = Find<ProgressionHUD>();
            Assert.GreaterOrEqual(hud.BannersShown, 1, "Level-up banner should play");
            Assert.AreEqual(prog.Level.ToString(), hud.LevelText);
        }

        // ------------------------------------------------------------------ greenhouse
        [UnityTest]
        public IEnumerator GreenhouseRequirementsUpdateAndUnlock()
        {
            var gh = Find<GreenhouseBuilding>();
            StandBeside(gh, Vector3.left);
            yield return null;
            Assert.IsTrue(interactor.TryInteract(gh));
            yield return null;
            var panel = Find<GreenhousePanel>();
            var cfg = prog.Config;
            Assert.IsFalse(gh.IsUnlocked);
            Assert.IsTrue(PanelShows(panel, $"Reach Farm Level {cfg.greenhouseRequiredLevel}"));
            Assert.IsTrue(PanelShows(panel, "You: Level 1"));
            Assert.IsFalse(gh.TryUnlock(), "Locked greenhouse must not unlock early");

            prog.DevAddStars(cfg.GetLevel(cfg.greenhouseRequiredLevel).starsRequired);
            yield return null;
            Assert.IsTrue(PanelShows(panel, $"You: Level {prog.Level}"), "Requirements should update live");
            if (prog.LifetimeStars < cfg.greenhouseRequiredStars)
            {
                Assert.IsFalse(gh.RequirementsMet);
                prog.DevAddStars(cfg.greenhouseRequiredStars - prog.LifetimeStars);
            }
            Assert.IsTrue(gh.RequirementsMet);
            float g0 = FarmManager.Instance.BonusGrowthMultiplier;
            Assert.IsTrue(gh.TryUnlock());
            Assert.IsTrue(gh.IsUnlocked);
            Assert.AreEqual(g0 * cfg.greenhouseGrowthBonus, FarmManager.Instance.BonusGrowthMultiplier, 0.001f);
            Assert.IsTrue(prog.HasAchievement("under_glass"));
            yield return null;
            Assert.IsTrue(PanelShows(panel, "Seed nursery"));

            var carrot = Crop("carrot");
            inv.Add(carrot.produceItem, 1);
            int s0 = inv.Count(carrot.seedItem);
            Assert.IsTrue(gh.Nurse(carrot));
            Assert.AreEqual(s0 + cfg.nurserySeedsPerProduce, inv.Count(carrot.seedItem));
            Assert.IsFalse(gh.Nurse(carrot), "Needs a carrot to nurse");
        }

        // ------------------------------------------------------------------ Heart of Mossvale
        [UnityTest]
        public IEnumerator HeartOfMossvaleRestoresBySpendingStars()
        {
            var heart = Find<HeartOfMossvale>();
            StandBeside(heart, Vector3.right);
            yield return null;
            Assert.IsTrue(interactor.TryInteract(heart));
            yield return null;
            var panel = Find<HeartPanel>();
            Assert.IsTrue(panel.IsOpen);
            Assert.AreEqual(0, heart.Stage);
            Assert.IsTrue(PanelShows(panel, heart.CurrentStage.title));
            Assert.IsTrue(PanelShows(panel, heart.NextStage.title), "Next visual upgrade shown");
            Assert.IsFalse(heart.TryRestore(), "No stars yet");
            StringAssert.Contains("Needs", FarmFeedback.LastMessage);

            int cost = heart.NextStage.starCost;
            prog.DevAddStars(cost);
            int lifetime = prog.LifetimeStars, balance = prog.StarBalance;
            Assert.IsTrue(heart.TryRestore());
            Assert.AreEqual(1, heart.Stage);
            int achStars = prog.Config.achievements.First(a => a.id == "heart_awakens").stars;
            Assert.AreEqual(balance - cost + achStars, prog.StarBalance, "Stars spent (plus Heart Awakens reward)");
            Assert.AreEqual(lifetime + achStars, prog.LifetimeStars, "Spending must not reduce lifetime stars / level");
            var visuals = heart.transform.Find("Visuals");
            Assert.IsTrue(visuals.Find("Stage_1").gameObject.activeSelf);
            Assert.IsFalse(visuals.Find("Stage_0").gameObject.activeSelf);

            // Next stage needs more stars than we have left.
            if (prog.StarBalance < heart.NextStage.starCost) Assert.IsFalse(heart.TryRestore());
            yield return null;
            Assert.IsTrue(PanelShows(panel, "Restoration 1 /"));
        }

        // ------------------------------------------------------------------ daily objectives
        [UnityTest]
        public IEnumerator DailyObjectivesCompleteOnceAndRollOver()
        {
            yield return null;
            var dailies = prog.DailyObjectives.ToList();
            Assert.AreEqual(prog.Config.dailyObjectivesPerDay, dailies.Count);
            var d = dailies[0];
            int paid = 0;
            void Count(string msg, Vector3 pos, FeedbackKind kind) { if (msg.Contains("Daily: " + d.def.title)) paid++; }
            FarmFeedback.Raised += Count;
            int stars0 = prog.LifetimeStars;
            prog.ReportStat(d.def.statKey, prog.Stat(d.def.statKey) + d.def.amount);
            Assert.IsTrue(prog.DailyObjectives.First(x => x.def.id == d.def.id).done, "Objective should complete");
            Assert.GreaterOrEqual(prog.LifetimeStars - stars0, d.def.rewardStars, "Reward stars paid");
            prog.ReportStat(d.def.statKey, prog.Stat(d.def.statKey) + d.def.amount);
            FarmFeedback.Raised -= Count;
            Assert.AreEqual(1, paid, "A completed objective must pay exactly once");
            prog.DevNewDay();
            Assert.IsTrue(prog.DailyObjectives.All(x => !x.done), "New day = fresh objectives");
        }

        // ------------------------------------------------------------------ save / load
        [UnityTest]
        public IEnumerator SaveAndLoadRestoresAllProgress()
        {
            var barn = Find<BarnStorage>();
            var heart = Find<HeartOfMossvale>();
            prog.DevAddStars(20);                       // reaches Level 3: tomato + wheat beds unlocked
            wallet.Add(100);
            Assert.IsTrue(prog.TryBuyUpgrade(prog.Config.upgrades.First(u => u.id == "bigger_can")));
            Assert.IsTrue(barn.Store(Item("carrot_seeds"), 2));
            inv.Add(Item("tomato"), 4);
            Assert.IsTrue(heart.TryRestore());
            var plot = Plot("FarmBed_Wheat", "Plot_1_2");
            plot.DevSetState(PlotState.Watered, Crop("wheat"), CropStage.Medium, 0.5f);
            tools.SetWater(3);
            tools.SelectTool(ToolType.WateringCan);
            yield return null;

            var expected = new
            {
                coins = wallet.Coins, earned = wallet.TotalEarned, lifetime = prog.LifetimeStars, balance = prog.StarBalance,
                level = prog.Level, ach = prog.CompletedAchievements, plots = prog.UnlockedPlotCount,
                seeds = inv.Count(Item("carrot_seeds")), tomato = inv.Count(Item("tomato")), stored = barn.Storage.Count(Item("carrot_seeds")),
                heart = heart.Stage, water = tools.Water, cap = tools.WaterCapacity, daily = string.Join(",", prog.DailyObjectives.Select(x => x.def.id)),
            };
            SaveManager.Instance.SaveNow();
            Assert.IsTrue(File.Exists(SaveManager.Instance.FilePath), "Save file written");

            yield return Load();                        // reload the scene: everything is rebuilt from the save
            yield return null;
            Assert.IsTrue(SaveManager.Instance.LoadedFromSave);
            barn = Find<BarnStorage>();
            heart = Find<HeartOfMossvale>();
            plot = Plot("FarmBed_Wheat", "Plot_1_2");
            Assert.AreEqual(expected.coins, wallet.Coins, "coins");
            Assert.AreEqual(expected.earned, wallet.TotalEarned, "total earned");
            Assert.AreEqual(expected.lifetime, prog.LifetimeStars, "lifetime stars");
            Assert.AreEqual(expected.balance, prog.StarBalance, "star balance");
            Assert.AreEqual(expected.level, prog.Level, "level");
            Assert.AreEqual(expected.ach, prog.CompletedAchievements, "achievements");
            Assert.AreEqual(expected.plots, prog.UnlockedPlotCount, "unlocked plots");
            Assert.IsTrue(prog.OwnsUpgrade("bigger_can"), "upgrade");
            Assert.AreEqual(expected.cap, tools.WaterCapacity, "can capacity");
            Assert.AreEqual(expected.water, tools.Water, "water level");
            Assert.AreEqual(ToolType.WateringCan, tools.SelectedTool, "selected tool");
            Assert.AreEqual(expected.seeds, inv.Count(Item("carrot_seeds")), "backpack seeds");
            Assert.AreEqual(expected.tomato, inv.Count(Item("tomato")), "backpack produce");
            Assert.AreEqual(expected.stored, barn.Storage.Count(Item("carrot_seeds")), "barn storage");
            Assert.AreEqual(expected.heart, heart.Stage, "heart stage");
            Assert.IsTrue(heart.transform.Find("Visuals/Stage_1").gameObject.activeSelf, "heart visual");
            Assert.AreEqual(PlotState.Watered, plot.State, "plot state");
            Assert.AreEqual("wheat", plot.Crop.cropId, "plot crop");
            Assert.AreEqual(CropStage.Medium, plot.Stage, "plot stage");
            Assert.AreEqual(Crop("wheat").stagePrefabs[2].name, plot.GetComponent<FarmPlotVisuals>().CurrentCropVisualName, "plot visual");
            Assert.AreEqual(expected.daily, string.Join(",", prog.DailyObjectives.Select(x => x.def.id)), "daily objectives");
        }

        // ------------------------------------------------------------------ screenshots (only with MOSSVALE_SHOTS)
        [UnityTest]
        public IEnumerator CaptureProgressionScreenshots()
        {
            string dir = Environment.GetEnvironmentVariable("MOSSVALE_SHOTS");
            if (string.IsNullOrEmpty(dir)) { Assert.Ignore("Set MOSSVALE_SHOTS to capture screenshots"); yield break; }
            Directory.CreateDirectory(dir);
            prog.DevAddStars(12);
            wallet.Add(85);
            inv.Add(Item("carrot"), 6); inv.Add(Item("tomato"), 3);
            var barn = Find<BarnStorage>();
            barn.Store(Item("wheat_seeds"), 3);
            prog.ReportStat(Stats.Harvests, 7);
            yield return new WaitForSeconds(9.5f);   // let the queued level-up banners finish

            foreach (var name in new[] { "Farmhouse", "Barn", "Market Stall", "Greenhouse", "Heart of Mossvale" })
            {
                var bld = UnityEngine.Object.FindObjectsByType<Building>(FindObjectsSortMode.None).First(b => b.DisplayName == name);
                bld.Panel.Open(bld);
                yield return new WaitForSeconds(0.4f);
                yield return Capture(Path.Combine(dir, "panel_" + name.Replace(" ", "_") + ".png"));
                if (bld.Panel is FarmhousePanel fp)
                    for (int t = 1; t < 4; t++)
                    {
                        fp.SelectTab(t);
                        yield return new WaitForSeconds(0.2f);
                        yield return Capture(Path.Combine(dir, $"panel_Farmhouse_tab{t}.png"));
                    }
                bld.Panel.Close();
                yield return new WaitForSeconds(0.3f);
            }
            var heart = Find<HeartOfMossvale>();
            for (int s = 0; s <= heart.MaxStage; s++)
            {
                heart.DevSetStage(s);
                Teleport(heart.transform.position + new Vector3(0, 0, -4.5f), heart.transform.position);
                yield return new WaitForSeconds(2.5f);
                yield return Capture(Path.Combine(dir, $"heart_stage{s}.png"));
            }
            var bed = Plot("FarmBed_Wheat", "Plot_0_0");
            Teleport(bed.transform.position + new Vector3(0, 0, -5f), bed.transform.position);
            yield return new WaitForSeconds(2.5f);
            yield return Capture(Path.Combine(dir, "world_beds_badges.png"));
        }

        IEnumerator Capture(string path)
        {
            var cam = Camera.main;
            var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            var canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay).ToList();
            foreach (var c in canvases)
            {
                c.renderMode = RenderMode.ScreenSpaceCamera;
                c.worldCamera = cam;
                c.planeDistance = c.sortingOrder >= 20 ? 0.4f : 0.6f;
            }
            yield return null;
            yield return null;
            var req = new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = rt };
            if (UnityEngine.Rendering.RenderPipeline.SupportsRenderRequest(cam, req)) UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(cam, req);
            else cam.Render();
            var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.Destroy(tex);
            cam.targetTexture = null;
            foreach (var c in canvases) c.renderMode = RenderMode.ScreenSpaceOverlay;
            rt.Release();
            Debug.Log("[MossvaleTest] captured " + path);
        }
    }
}
