using System;
using UnityEngine;

namespace Mossvale.Farming
{
    /// <summary>
    /// One plantable plot. Owns the farming state machine and crop growth; visuals live in FarmPlotVisuals.
    ///   Untilled/Harvested --Hoe--> Tilled --Seeds--> Planted --Water--> Watered --(time)--> Growing (needs water)
    ///   ... --Water--> Watered --(time)--> Grown --Harvest--> Harvested
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class FarmPlot : Interactable, ISaveable
    {
        [SerializeField] PlotState state = PlotState.Untilled;
        [SerializeField] CropData crop;
        [SerializeField] CropStage stage = CropStage.Seed;
        [SerializeField] bool watered;
        [SerializeField] float stageTimer;
        [SerializeField] bool locked;
        [SerializeField] string lockedReason = "Locked";

        /// <summary>Raised after any state change.</summary>
        public event Action<FarmPlot> Changed;
        /// <summary>Raised when the crop advances a growth stage.</summary>
        public event Action<FarmPlot, CropStage> StageAdvanced;
        /// <summary>Raised after an attempted action: (plot, tool used, success).</summary>
        public event Action<FarmPlot, ToolType, bool> ActionPerformed;

        public PlotState State => state;
        public CropData Crop => crop;
        public CropStage Stage => stage;
        public bool IsWatered => watered;
        public bool IsLocked => locked;

        /// <summary>Locks/unlocks the plot (used by BedLock when farm areas unlock).</summary>
        public void SetLocked(bool value, string reason)
        {
            locked = value;
            lockedReason = string.IsNullOrEmpty(reason) ? "Locked" : reason;
            if (locked && state != PlotState.Untilled) DevSetState(PlotState.Untilled);
        }
        public bool NeedsWater => state == PlotState.Planted || state == PlotState.Growing;
        public float StageProgress01 => crop == null || stage == CropStage.Full ? 1f : Mathf.Clamp01(stageTimer / crop.secondsPerStage);
        /// <summary>Overall growth 0..1 from seed to fully grown.</summary>
        public float TotalProgress01 => crop == null ? 0f : Mathf.Clamp01(((int)stage + (stage == CropStage.Full ? 0f : StageProgress01)) / 3f);

        public override string DisplayName => "Farm plot";
        public override float HighlightHeight => transform.position.y + 0.16f;
        public override Vector3 InteractionPoint => transform.position + Vector3.up * 0.2f;

        protected override void OnEnable() { base.OnEnable(); FarmManager.Register(this); }
        protected override void OnDisable() { base.OnDisable(); FarmManager.Unregister(this); }

        void Update()
        {
            if (state != PlotState.Watered || crop == null) return;
            stageTimer += Time.deltaTime * FarmManager.GrowthMultiplier;
            if (stageTimer >= crop.secondsPerStage) AdvanceStage();
        }

        // ------------------------------------------------------------------ growth
        /// <summary>Moves the crop to its next stage (called by the growth timer, or by dev tools).</summary>
        public void AdvanceStage()
        {
            if (crop == null || stage == CropStage.Full) return;
            stage++;
            stageTimer = 0f;
            if (stage == CropStage.Full)
            {
                state = PlotState.Grown;
                watered = false;
                FarmFeedback.Raise($"{crop.displayName} is ready to harvest!", InteractionPoint, FeedbackKind.Growth);
            }
            else if (FarmManager.RequireWaterEachStage)
            {
                state = PlotState.Growing;
                watered = false;
                FarmFeedback.Raise($"{crop.displayName} grew ({stage}) - water it again", InteractionPoint, FeedbackKind.Growth);
            }
            else
            {
                FarmFeedback.Raise($"{crop.displayName} grew ({stage})", InteractionPoint, FeedbackKind.Growth);
            }
            StageAdvanced?.Invoke(this, stage);
            Changed?.Invoke(this);
            SaveManager.MarkDirty();
        }

        // ------------------------------------------------------------------ evaluation (shared by prompt + action)
        struct Evaluation
        {
            public ToolType tool; public string action; public bool ok; public string reason;
            public Evaluation(ToolType tool, string action, bool ok, string reason)
            { this.tool = tool; this.action = action; this.ok = ok; this.reason = reason; }
        }

        Evaluation Evaluate(PlayerToolController tools)
        {
            if (locked) return new Evaluation(tools != null ? tools.SelectedTool : ToolType.None, "Locked plot", false, lockedReason);
            // A fully grown crop is harvested whatever tool is selected (comfortable, contextual).
            if (state == PlotState.Grown)
                return new Evaluation(ToolType.Basket, $"Harvest {crop.displayName}", true, null);

            var tool = tools != null ? tools.SelectedTool : ToolType.None;
            switch (tool)
            {
                case ToolType.Hoe:
                    if (state == PlotState.Untilled || state == PlotState.Harvested)
                        return new Evaluation(tool, "Till soil", true, null);
                    return new Evaluation(tool, "Till soil", false, state == PlotState.Tilled ? "Already tilled - plant seeds (2)" : "Something is growing here");

                case ToolType.Seeds:
                {
                    var seed = tools.SelectedSeed;
                    var c = seed != null ? seed.plantsCrop : null;
                    string label = c != null ? $"Plant {c.displayName}" : "Plant seeds";
                    if (state == PlotState.Untilled || state == PlotState.Harvested) return new Evaluation(tool, label, false, "Till the soil first (Hoe, 1)");
                    if (state != PlotState.Tilled) return new Evaluation(tool, label, false, "Something is already planted here");
                    if (c == null) return new Evaluation(tool, label, false, "No seeds selected (Tab to choose)");
                    if (!tools.Inventory.Has(seed)) return new Evaluation(tool, label, false, $"No {c.displayName} seeds left (Tab to switch)");
                    return new Evaluation(tool, label, true, null);
                }

                case ToolType.WateringCan:
                    if (!NeedsWater)
                    {
                        string why = state == PlotState.Watered ? "Already watered" : "Nothing planted here to water";
                        return new Evaluation(tool, "Water", false, why);
                    }
                    if (!tools.HasWater) return new Evaluation(tool, "Water", false, "Watering can is empty - refill at the well or pond");
                    return new Evaluation(tool, "Water", true, null);

                case ToolType.Basket:
                    return new Evaluation(tool, "Harvest", false, crop != null ? $"Not ready yet ({Mathf.RoundToInt(TotalProgress01 * 100)}% grown)" : "Nothing to harvest");

                default:
                    return new Evaluation(tool, "Use", false, "Select a tool (1-4)");
            }
        }

        public override InteractionPrompt GetPrompt(PlayerToolController tools)
        {
            var e = Evaluate(tools);
            return new InteractionPrompt(e.action, e.ok ? StatusText : e.reason, e.ok);
        }

        public override bool Interact(PlayerToolController tools)
        {
            var e = Evaluate(tools);
            if (!e.ok)
            {
                FarmFeedback.Raise(e.reason, InteractionPoint, FeedbackKind.Failure);
                ActionPerformed?.Invoke(this, e.tool, false);
                return false;
            }
            switch (e.tool)
            {
                case ToolType.Hoe: DoTill(); break;
                case ToolType.Seeds:
                    tools.Inventory.TryRemove(tools.SelectedSeed);
                    DoPlant(tools.SelectedSeed.plantsCrop);
                    break;
                case ToolType.WateringCan:
                    tools.ConsumeWater();
                    DoWater();
                    break;
                case ToolType.Basket: DoHarvest(tools.Inventory); break;
            }
            ActionPerformed?.Invoke(this, e.tool, true);
            Changed?.Invoke(this);
            SaveManager.MarkDirty();
            return true;
        }

        // ------------------------------------------------------------------ actions
        void DoTill()
        {
            state = PlotState.Tilled;
            crop = null; stage = CropStage.Seed; watered = false; stageTimer = 0f;
            FarmFeedback.Raise("Soil tilled", InteractionPoint, FeedbackKind.Success);
            FarmEvents.RaiseTilled(this);
        }

        void DoPlant(CropData c)
        {
            crop = c; stage = CropStage.Seed; watered = false; stageTimer = 0f;
            state = PlotState.Planted;
            FarmFeedback.Raise($"Planted {c.displayName}", InteractionPoint, FeedbackKind.Success);
            FarmEvents.RaisePlanted(this, c);
        }

        void DoWater()
        {
            watered = true;
            state = PlotState.Watered;
            FarmFeedback.Raise("Watered", InteractionPoint, FeedbackKind.Success);
            FarmEvents.RaiseWatered(this);
        }

        void DoHarvest(Inventory inventory)
        {
            int yield = UnityEngine.Random.Range(crop.minYield, crop.maxYield + 1);
            int seeds = UnityEngine.Random.Range(crop.minSeedsBack, crop.maxSeedsBack + 1);
            inventory.Add(crop.produceItem, yield);
            if (seeds > 0) inventory.Add(crop.seedItem, seeds);
            string msg = $"+{yield} {crop.produceItem.displayName}" + (seeds > 0 ? $"  +{seeds} seeds" : "");
            LastHarvest = (crop, yield, seeds);
            var harvestedCrop = crop;
            crop = null; stage = CropStage.Seed; watered = false; stageTimer = 0f;
            state = PlotState.Harvested;
            FarmFeedback.Raise(msg, InteractionPoint, FeedbackKind.Harvest);
            FarmEvents.RaiseHarvested(this, harvestedCrop, yield);
        }

        /// <summary>Crop, produce and seeds from the most recent harvest on this plot.</summary>
        public (CropData crop, int produce, int seeds) LastHarvest { get; private set; }

        public string StatusText
        {
            get
            {
                switch (state)
                {
                    case PlotState.Untilled: return "Untilled soil - use the Hoe (1)";
                    case PlotState.Tilled: return "Tilled soil - ready for seeds";
                    case PlotState.Planted: return $"{crop.displayName} seeds - needs water";
                    case PlotState.Watered: return $"{crop.displayName} ({stage}) - growing {Mathf.RoundToInt(StageProgress01 * 100)}%";
                    case PlotState.Growing: return $"{crop.displayName} ({stage}) - needs water";
                    case PlotState.Grown: return $"{crop.displayName} - ready to harvest!";
                    case PlotState.Harvested: return "Harvested - till again to replant";
                }
                return "";
            }
        }

        // ------------------------------------------------------------------ dev / testing
        /// <summary>Forces a state directly (dev tools, tests, showcase). Skips inventory and feedback.</summary>
        public void DevSetState(PlotState newState, CropData newCrop = null, CropStage newStage = CropStage.Seed, float progress01 = 0f)
        {
            bool needsCrop = newState == PlotState.Planted || newState == PlotState.Watered ||
                             newState == PlotState.Growing || newState == PlotState.Grown;
            state = newState;
            crop = needsCrop ? newCrop : null;
            stage = newState == PlotState.Grown ? CropStage.Full : newStage;
            watered = newState == PlotState.Watered;
            stageTimer = crop != null ? Mathf.Clamp01(progress01) * crop.secondsPerStage : 0f;
            Changed?.Invoke(this);
        }

        /// <summary>Dev: water if needed, then jump to the next stage.</summary>
        public void DevGrowOneStage()
        {
            if (crop == null || stage == CropStage.Full) return;
            AdvanceStage();
        }

        // ------------------------------------------------------------------ save
        [Serializable] class PlotSave { public PlotState state; public string crop; public CropStage stage; public float timer; }

        public string SaveKey => "plot:" + (transform.parent != null && transform.parent.parent != null ? transform.parent.parent.name + "/" : "") + name;
        public int RestoreOrder => 30;

        public string CaptureState() =>
            JsonUtility.ToJson(new PlotSave { state = state, crop = crop != null ? crop.cropId : "", stage = stage, timer = stageTimer });

        public void RestoreState(string json)
        {
            var s = JsonUtility.FromJson<PlotSave>(json);
            var c = FarmManager.FindCrop(s.crop);
            bool needsCrop = s.state == PlotState.Planted || s.state == PlotState.Watered || s.state == PlotState.Growing || s.state == PlotState.Grown;
            if (locked || (needsCrop && c == null)) { DevSetState(locked ? PlotState.Untilled : PlotState.Tilled); return; }
            state = s.state;
            crop = needsCrop ? c : null;
            stage = s.state == PlotState.Grown ? CropStage.Full : s.stage;
            watered = s.state == PlotState.Watered;
            stageTimer = crop != null ? Mathf.Clamp(s.timer, 0f, crop.secondsPerStage) : 0f;
            Changed?.Invoke(this);
        }

        [ContextMenu("Dev/Advance One Stage")] void CtxAdvance() => DevGrowOneStage();
        [ContextMenu("Dev/Water")] void CtxWater() { if (NeedsWater) { DoWater(); Changed?.Invoke(this); } }
        [ContextMenu("Dev/Reset To Untilled")] void CtxReset() => DevSetState(PlotState.Untilled);
    }
}
