using System;
using System.Collections;
using System.IO;
using System.Linq;
using Mossvale.Farming;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Mossvale.Tests
{
    /// Play Mode tests for the farming loop in Mossvale_Main. They drive the same public entry points the
    /// input code uses (PlayerToolController.SelectTool, PlayerInteractor.TryInteract / HandleClick,
    /// PlayerKeyboardMovement.Move) because a real mouse/keyboard can't be simulated in batch mode.
    public class FarmingLoopTests
    {
        GameObject player;
        PlayerToolController tools;
        PlayerInteractor interactor;
        Inventory inv;
        FarmHUD hud;
        IPointClickMover mover;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Application.targetFrameRate = 60;
            // Isolated save slot so tests never load or overwrite the player's real save.
            Mossvale.Farming.SaveManager.SlotOverride = "tests";
            Mossvale.Farming.SaveManager.DeleteSlot("tests");
            Mossvale.Farming.GameplayInput.Blocked = false;
            SceneManager.LoadScene("Mossvale_Main");
            yield return null;
            yield return null;
            player = GameObject.FindWithTag("Player");
            tools = player.GetComponent<PlayerToolController>();
            interactor = player.GetComponent<PlayerInteractor>();
            inv = player.GetComponent<Inventory>();
            mover = player.GetComponent<IPointClickMover>();
            hud = UnityEngine.Object.FindFirstObjectByType<FarmHUD>();
            Assert.IsNotNull(tools, "PlayerToolController missing");
            Assert.IsNotNull(interactor, "PlayerInteractor missing");
            Assert.IsNotNull(inv, "Inventory missing");
            Assert.IsNotNull(mover, "FarmClickMovement does not implement IPointClickMover");
            Assert.IsNotNull(hud, "FarmHUD missing");
            Assert.IsNotNull(FarmManager.Instance, "FarmManager missing");
            Assert.AreEqual(27, FarmManager.Plots.Count, "Expected 3 beds x 9 plots");
            // Tomato and Wheat beds are progression unlocks; these farming tests need every bed open.
            FarmProgressionManager.Instance.DevUnlockAllBeds();
        }

        // ------------------------------------------------------------------ helpers
        CropData Crop(string name) => inv.ItemsOf(ItemCategory.Seed).Select(s => s.plantsCrop).First(c => c.displayName == name);

        static FarmPlot Plot(string bed, string plot = "Plot_1_1") =>
            FarmManager.Plots.First(p => p.name == plot && p.transform.parent.parent.name == bed);

        static Bounds BedBounds(FarmPlot p) => p.transform.parent.parent.GetComponentInChildren<BoxCollider>().bounds;

        void Teleport(Vector3 pos, Vector3 lookAt)
        {
            var cc = player.GetComponent<CharacterController>();
            cc.enabled = false;
            player.transform.position = pos;
            var d = lookAt - pos; d.y = 0f;
            if (d.sqrMagnitude > 0.001f) player.transform.rotation = Quaternion.LookRotation(d);
            cc.enabled = true;
            mover.StopMoving();
        }

        /// Stands the player just outside the bed on the side nearest the plot.
        void StandNextTo(FarmPlot p)
        {
            var bb = BedBounds(p);
            var pp = p.transform.position;
            float z = Mathf.Abs(pp.z - bb.min.z) < Mathf.Abs(pp.z - bb.max.z) ? bb.min.z - 0.5f : bb.max.z + 0.5f;
            Teleport(new Vector3(pp.x, 0.02f, z), pp);
        }

        static IEnumerator WaitFor(Func<bool> cond, float timeout, string what)
        {
            float t = 0f;
            while (!cond() && t < timeout) { t += Time.deltaTime; yield return null; }
            Assert.IsTrue(cond(), "Timed out waiting for: " + what);
        }

        bool Use(FarmPlot p, ToolType tool)
        {
            Assert.IsTrue(tools.SelectTool(tool), "Could not select " + tool);
            return interactor.TryInteract(p);
        }

        static string VisualName(FarmPlot p) => p.GetComponent<FarmPlotVisuals>().CurrentCropVisualName;

        // ------------------------------------------------------------------ the loop, per crop
        [UnityTest]
        public IEnumerator FullLoop_TillPlantWaterGrowHarvest([Values("Carrot", "Tomato", "Wheat")] string cropName)
        {
            var crop = Crop(cropName);
            var plot = Plot("FarmBed_" + cropName, "Plot_0_1");
            StandNextTo(plot);
            yield return null;
            FarmManager.Instance.DevSpeedMultiplier = crop.secondsPerStage / 0.4f; // each stage takes ~0.4 s
            int seeds0 = inv.Count(crop.seedItem), produce0 = inv.Count(crop.produceItem);
            Assert.AreEqual(PlotState.Untilled, plot.State);

            Assert.IsTrue(Use(plot, ToolType.Hoe), "Till failed: " + FarmFeedback.LastMessage);
            Assert.AreEqual(PlotState.Tilled, plot.State);

            Assert.IsTrue(tools.SelectTool(ToolType.Seeds));
            tools.SelectSeed(crop.seedItem);
            Assert.IsTrue(interactor.TryInteract(plot), "Plant failed: " + FarmFeedback.LastMessage);
            Assert.AreEqual(PlotState.Planted, plot.State);
            Assert.AreEqual(seeds0 - 1, inv.Count(crop.seedItem), "Seed not consumed");
            Assert.AreEqual(crop.stagePrefabs[0].name, VisualName(plot), "Seed visual");
            Assert.IsTrue(plot.NeedsWater);

            // Not watered -> no growth.
            yield return new WaitForSeconds(1.0f);
            Assert.AreEqual(CropStage.Seed, plot.Stage, "Crop grew without water");

            int water0 = tools.Water;
            Assert.IsTrue(Use(plot, ToolType.WateringCan), "Water failed: " + FarmFeedback.LastMessage);
            Assert.AreEqual(PlotState.Watered, plot.State);
            Assert.AreEqual(water0 - 1, tools.Water, "Water not consumed");

            foreach (var stage in new[] { CropStage.Small, CropStage.Medium })
            {
                yield return WaitFor(() => plot.Stage == stage, 5f, $"{cropName} {stage}");
                Assert.AreEqual(PlotState.Growing, plot.State, "Should need water again after a stage");
                Assert.IsFalse(plot.IsWatered);
                Assert.AreEqual(crop.stagePrefabs[(int)stage].name, VisualName(plot), $"{stage} visual");
                Assert.IsTrue(interactor.TryInteract(plot), $"Water at {stage} failed: " + FarmFeedback.LastMessage);
            }
            yield return WaitFor(() => plot.State == PlotState.Grown, 5f, cropName + " grown");
            Assert.AreEqual(CropStage.Full, plot.Stage);
            Assert.AreEqual(crop.stagePrefabs[3].name, VisualName(plot), "Full visual");

            Assert.IsTrue(Use(plot, ToolType.Basket), "Harvest failed: " + FarmFeedback.LastMessage);
            Assert.AreEqual(PlotState.Harvested, plot.State);
            var h = plot.LastHarvest;
            Assert.AreEqual(crop, h.crop);
            Assert.That(h.produce, Is.InRange(crop.minYield, crop.maxYield));
            Assert.AreEqual(produce0 + h.produce, inv.Count(crop.produceItem), "Produce not added");
            Assert.AreEqual(seeds0 - 1 + h.seeds, inv.Count(crop.seedItem), "Seeds not returned");
            Assert.IsNull(VisualName(plot), "Crop visual should be gone after harvest");
            yield return null;
            Assert.AreEqual("x" + inv.Count(crop.produceItem), hud.CountTextFor(crop.produceItem), "HUD inventory count");

            Assert.IsTrue(Use(plot, ToolType.Hoe), "Re-till after harvest failed");
            Assert.AreEqual(PlotState.Tilled, plot.State);
            Debug.Log($"[MossvaleTest] {cropName} loop OK: +{h.produce} {crop.produceItem.displayName}, +{h.seeds} seeds");
        }

        [UnityTest]
        public IEnumerator HarvestWorksWithAnyToolWhenGrown()
        {
            var plot = Plot("FarmBed_Carrot", "Plot_0_0");
            StandNextTo(plot);
            yield return null;
            plot.DevSetState(PlotState.Grown, Crop("Carrot"), CropStage.Full);
            int before = inv.Count(Crop("Carrot").produceItem);
            Assert.IsTrue(Use(plot, ToolType.WateringCan));
            Assert.AreEqual(PlotState.Harvested, plot.State);
            Assert.Greater(inv.Count(Crop("Carrot").produceItem), before);
        }

        // ------------------------------------------------------------------ failures
        [UnityTest]
        public IEnumerator InvalidActionsFailWithFeedbackAndChangeNothing()
        {
            var crop = Crop("Wheat");
            var plot = Plot("FarmBed_Wheat", "Plot_0_0");
            StandNextTo(plot);
            yield return null;
            int seeds0 = inv.Count(crop.seedItem);

            void ExpectFail(ToolType tool, string contains, PlotState state)
            {
                Assert.IsTrue(tools.SelectTool(tool));
                if (tool == ToolType.Seeds) tools.SelectSeed(crop.seedItem);
                Assert.IsFalse(interactor.TryInteract(plot), $"{tool} should fail on {plot.State}");
                Assert.AreEqual(FeedbackKind.Failure, FarmFeedback.LastKind);
                StringAssert.Contains(contains, FarmFeedback.LastMessage);
                Assert.AreEqual(state, plot.State, "State changed after a failed action");
            }

            ExpectFail(ToolType.Seeds, "Till the soil first", PlotState.Untilled);
            Assert.AreEqual(seeds0, inv.Count(crop.seedItem), "Seed consumed by failed plant");
            ExpectFail(ToolType.WateringCan, "Nothing planted", PlotState.Untilled);
            ExpectFail(ToolType.Basket, "Nothing to harvest", PlotState.Untilled);

            Assert.IsTrue(Use(plot, ToolType.Hoe));
            ExpectFail(ToolType.Hoe, "Already tilled", PlotState.Tilled);
            ExpectFail(ToolType.WateringCan, "Nothing planted", PlotState.Tilled);

            tools.SelectTool(ToolType.Seeds); tools.SelectSeed(crop.seedItem);
            Assert.IsTrue(interactor.TryInteract(plot));
            ExpectFail(ToolType.Hoe, "Something is growing", PlotState.Planted);
            ExpectFail(ToolType.Seeds, "already planted", PlotState.Planted);
            ExpectFail(ToolType.Basket, "Not ready yet", PlotState.Planted);

            tools.SetWater(0);
            ExpectFail(ToolType.WateringCan, "empty", PlotState.Planted);
            Assert.AreEqual(0, tools.Water);

            Assert.IsTrue(Use(plot, ToolType.WateringCan) == false);
            tools.RefillWater();
            Assert.IsTrue(Use(plot, ToolType.WateringCan));
            ExpectFail(ToolType.WateringCan, "Already watered", PlotState.Watered);

            // Out of seeds.
            var other = Plot("FarmBed_Wheat", "Plot_0_2");
            StandNextTo(other);
            Assert.IsTrue(Use(other, ToolType.Hoe));
            foreach (var s in inv.ItemsOf(ItemCategory.Seed).ToList()) inv.SetCount(s, 0);
            tools.SelectTool(ToolType.Seeds); tools.SelectSeed(crop.seedItem);
            Assert.IsFalse(interactor.TryInteract(other));
            StringAssert.Contains("seeds left", FarmFeedback.LastMessage);
            Assert.AreEqual(PlotState.Tilled, other.State);

            // Too far away.
            Teleport(other.transform.position + new Vector3(0f, 0f, 9f), other.transform.position);
            Assert.IsTrue(Use(other, ToolType.Hoe) == false);
            StringAssert.Contains("Too far", FarmFeedback.LastMessage);
        }

        [UnityTest]
        public IEnumerator WateringCanRefillsAtWellAndPond()
        {
            foreach (var name in new[] { "Well", "Pond" })
            {
                var src = UnityEngine.Object.FindObjectsByType<WaterSource>(FindObjectsSortMode.None).First(w => w.DisplayName == name);
                var b = src.WorldBounds;
                // Stand on the side of the source facing the garden/centre, just outside its bounds.
                Vector3 dir = (new Vector3(0f, 0f, 0f) - b.center); dir.y = 0f; dir.Normalize();
                var pos = b.center + dir * (Mathf.Max(b.extents.x, b.extents.z) + 0.6f);
                // Walk inwards until within range (bounds are axis-aligned; the source may be round).
                Teleport(new Vector3(pos.x, 0.02f, pos.z), b.center);
                yield return null;
                tools.SetWater(0);
                Assert.IsTrue(tools.SelectTool(ToolType.WateringCan));
                Assert.IsTrue(interactor.TryInteract(src), $"Refill at {name} failed: {FarmFeedback.LastMessage}");
                Assert.AreEqual(tools.WaterCapacity, tools.Water);
                tools.SelectTool(ToolType.Hoe);
                Assert.IsFalse(interactor.TryInteract(src), "Refill should need the watering can");
            }
        }

        // ------------------------------------------------------------------ movement + targeting
        [UnityTest]
        public IEnumerator ClickingAPlotWalksThereThenUsesTheTool()
        {
            var plot = Plot("FarmBed_Wheat", "Plot_1_1");
            var bb = BedBounds(plot);
            Teleport(new Vector3(plot.transform.position.x + 0.5f, 0.02f, bb.max.z + 4f), plot.transform.position);
            yield return null;
            tools.SelectTool(ToolType.Hoe);
            Assert.IsFalse(interactor.InRange(plot), "Test should start out of range");

            var ray = new Ray(plot.InteractionPoint + Vector3.up * 6f, Vector3.down);
            Assert.IsTrue(Physics.Raycast(ray, out var hit, 20f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide));
            Assert.AreEqual(plot, hit.collider.GetComponentInParent<FarmPlot>(), "Click ray should hit the plot");
            mover.MoveTo(hit.point);
            interactor.HandleClick(hit);
            yield return WaitFor(() => plot.State == PlotState.Tilled, 5f, "walk + till");
            Assert.IsFalse(mover.IsMoving, "Player should stop once the action is done");
            var p = player.transform.position;
            Assert.IsFalse(bb.Contains(new Vector3(p.x, bb.center.y, p.z)), "Player walked into the bed");
            var toPlot = plot.transform.position - p; toPlot.y = 0f;
            Assert.Less(Vector3.Angle(player.transform.forward, toPlot), 10f, "Player should face the plot");
        }

        [UnityTest]
        public IEnumerator KeyboardMovesPlayerCancelsClickAndIsBlockedByBeds()
        {
            var km = player.GetComponent<PlayerKeyboardMovement>();
            var plot = Plot("FarmBed_Tomato", "Plot_1_1");
            var bb = BedBounds(plot);
            Teleport(new Vector3(bb.center.x, 0.02f, bb.max.z + 3f), bb.center);
            mover.MoveTo(player.transform.position + Vector3.right * 5f);
            var start = player.transform.position;
            // "S" pushes towards -Z (camera looks +Z): walk straight into the bed for 2 s.
            for (float t = 0; t < 2f; t += Time.deltaTime) { km.Move(new Vector2(0f, -1f), Time.deltaTime); yield return null; }
            Assert.IsFalse(mover.IsMoving, "Keyboard input should cancel the click destination");
            var p = player.transform.position;
            Assert.Greater(start.z - p.z, 1.5f, "Keyboard did not move the player");
            Assert.Greater(p.z, bb.max.z, "Player walked through the raised bed");
            Debug.Log($"[MossvaleTest] keyboard walk stopped at {p}, bed edge z={bb.max.z:F2}");
        }

        [UnityTest]
        public IEnumerator KeyboardTargetIsThePlotInFrontAndEUsesIt()
        {
            var plot = Plot("FarmBed_Carrot", "Plot_0_1");
            StandNextTo(plot);
            tools.SelectTool(ToolType.Hoe);
            yield return null;
            yield return null;
            Assert.IsNotNull(interactor.KeyboardTarget, "No keyboard target next to a bed");
            var target = interactor.KeyboardTarget as FarmPlot;
            Assert.IsNotNull(target);
            Assert.IsTrue(interactor.InRange(target));
            interactor.InteractWithKeyboardTarget();
            Assert.AreEqual(PlotState.Tilled, target.State);
            yield return null;
            StringAssert.Contains("Plant", Prompt(ToolType.Seeds));
        }

        string Prompt(ToolType tool)
        {
            tools.SelectTool(tool);
            interactor.SendMessage("Update");
            hud.SendMessage("Update");
            return hud.PromptText;
        }

        // ------------------------------------------------------------------ HUD + dev tools
        [UnityTest]
        public IEnumerator HudShowsToolsSeedsWaterAndPrompts()
        {
            yield return null;
            var slots = hud.GetComponentsInChildren<ToolbarSlotUI>(true);
            Assert.AreEqual(4, slots.Length);
            Assert.IsTrue(slots.All(s => s.gameObject.activeSelf), "All four tools should be in the toolbar");
            tools.SelectTool(ToolType.Seeds);
            tools.SelectSeed(Crop("Tomato").seedItem);
            yield return null;
            var seedSlot = slots.First(s => s.tool == ToolType.Seeds);
            Assert.AreEqual("Tomato Seeds", seedSlot.nameLabel.text);
            Assert.AreEqual(inv.Count(Crop("Tomato").seedItem).ToString(), seedSlot.badge.text);
            Assert.IsTrue(seedSlot.selectedFrame.enabled);
            tools.SetWater(5);
            yield return null;
            var canSlot = slots.First(s => s.tool == ToolType.WateringCan);
            Assert.AreEqual(5f / tools.WaterCapacity, canSlot.barFill.fillAmount, 0.001f);
            foreach (var item in inv.Items.Where(i => i.category != ItemCategory.Tool))
                Assert.AreEqual("x" + inv.Count(item), hud.CountTextFor(item), item.displayName);

            var plot = Plot("FarmBed_Tomato", "Plot_0_0");
            StandNextTo(plot);
            yield return null;
            StringAssert.Contains("Till soil", Prompt(ToolType.Hoe));
            StringAssert.Contains("Till the soil first", Prompt(ToolType.Seeds));
            var floating = hud.GetComponentInChildren<FloatingFeedbackUI>();
            int before = floating.ActiveCount;
            Use(plot, ToolType.Hoe);
            Assert.Greater(floating.ActiveCount, before, "No floating feedback for an action");
        }

        [UnityTest]
        public IEnumerator DevToolsSpeedGrowWaterSeedsReset()
        {
            var dev = FarmDevTools.Instance;
            Assert.IsNotNull(dev);
            var crop = Crop("Carrot");
            var plot = Plot("FarmBed_Carrot", "Plot_2_2");
            plot.DevSetState(PlotState.Planted, crop, CropStage.Seed);
            dev.WaterAll();
            Assert.AreEqual(PlotState.Watered, plot.State);
            dev.GrowAllOneStage();
            Assert.AreEqual(CropStage.Small, plot.Stage);
            dev.SetSpeed(1000f);
            dev.WaterAll();
            yield return WaitFor(() => plot.Stage == CropStage.Medium, 2f, "dev speed growth");
            int s0 = inv.Count(crop.seedItem);
            dev.GiveSeeds(5);
            Assert.AreEqual(s0 + 5, inv.Count(crop.seedItem));
            dev.ResetAllPlots();
            Assert.IsTrue(FarmManager.Plots.All(p => p.State == PlotState.Untilled));
            dev.SetSpeed(1f); // otherwise the showcase's watered plots grow on the very next frame
            dev.Showcase();
            yield return null;
            foreach (var bed in new[] { "FarmBed_Carrot", "FarmBed_Tomato", "FarmBed_Wheat" })
            {
                var states = FarmManager.Plots.Where(p => p.transform.parent.parent.name == bed).Select(p => p.State).Distinct().ToList();
                Assert.AreEqual(7, states.Count, bed + " showcase should contain all 7 plot states");
            }
        }

        // ------------------------------------------------------------------ screenshots (only when MOSSVALE_SHOTS is set)
        [UnityTest]
        public IEnumerator CaptureVerificationScreenshots()
        {
            string dir = Environment.GetEnvironmentVariable("MOSSVALE_SHOTS");
            if (string.IsNullOrEmpty(dir)) { Assert.Ignore("Set MOSSVALE_SHOTS to capture screenshots"); yield break; }
            Directory.CreateDirectory(dir);
            FarmDevTools.Instance.Showcase();
            yield return new WaitForSeconds(0.5f);

            var camGo = new GameObject("ShotCam");
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 45f;
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            var garden = FarmManager.Plots.Aggregate(new Bounds(FarmManager.Plots[0].transform.position, Vector3.zero), (b, p) => { b.Encapsulate(p.transform.position); return b; });
            camGo.transform.position = garden.center + new Vector3(0f, 11f, -13f);
            camGo.transform.LookAt(garden.center);
            yield return Capture(cam, Path.Combine(dir, "farm_showcase_overview.png"), null);
            foreach (var bed in new[] { "FarmBed_Carrot", "FarmBed_Tomato", "FarmBed_Wheat" })
            {
                var c = BedBounds(Plot(bed)).center;
                camGo.transform.position = c + new Vector3(0f, 3.2f, -5.2f);
                camGo.transform.LookAt(c + Vector3.up * 0.4f);
                yield return Capture(cam, Path.Combine(dir, $"close_{bed}.png"), null);
            }
            UnityEngine.Object.Destroy(camGo);

            // Gameplay view with HUD: player next to a plot holding the watering can.
            var plot = Plot("FarmBed_Wheat", "Plot_0_2");
            StandNextTo(plot);
            tools.SelectTool(ToolType.WateringCan);
            yield return new WaitForSeconds(2.5f); // let the follow camera settle
            Use(plot, ToolType.WateringCan);
            yield return new WaitForSeconds(0.2f);
            yield return Capture(Camera.main, Path.Combine(dir, "gameplay_hud.png"), hud.GetComponent<Canvas>());
        }

        static IEnumerator Capture(Camera cam, string path, Canvas canvas)
        {
            var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
            var prevTarget = cam.targetTexture;
            cam.targetTexture = rt;
            RenderMode prevMode = RenderMode.ScreenSpaceOverlay;
            if (canvas != null)
            {
                prevMode = canvas.renderMode;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = 1f;
            }
            yield return null;
            yield return null;   // let the canvas lay out at the render-texture size
            // WaitForEndOfFrame never fires in batch mode, so render explicitly.
            var req = new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = rt };
            if (UnityEngine.Rendering.RenderPipeline.SupportsRenderRequest(cam, req)) UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(cam, req);
            else cam.Render();
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.Destroy(tex);
            cam.targetTexture = prevTarget;
            if (canvas != null) canvas.renderMode = prevMode;
            rt.Release();
            Debug.Log("[MossvaleTest] captured " + path);
        }
    }
}
