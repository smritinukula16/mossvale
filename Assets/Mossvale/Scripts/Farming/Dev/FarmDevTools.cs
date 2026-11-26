using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;

namespace Mossvale.Farming
{
    /// <summary>
    /// Development / testing helpers (Play Mode). Hotkeys:
    ///   F1 toggle dev panel      F2 grow every planted crop one stage   F3 water every plot that needs it
    ///   F4 +5 of every seed      F5 cycle growth speed (x1, x5, x20, x100)
    ///   F6 reset all plots       F7 refill watering can                 F8 showcase every crop state/stage
    ///   F9 +10 blue stars        F10 +100 coins                         F11 new day (daily objectives)
    ///   F12 delete the save and restart the scene
    /// Also available from the Unity menu: Mossvale > Dev (Play Mode).
    /// </summary>
    public class FarmDevTools : MonoBehaviour
    {
        public static FarmDevTools Instance { get; private set; }

        [SerializeField] bool enableHotkeys = true;
        [SerializeField] GameObject panel;
        [SerializeField] TMP_Text panelText;
        [SerializeField] Inventory inventory;
        [SerializeField] PlayerToolController tools;
        [SerializeField] CropData[] crops;

        static readonly float[] Speeds = { 1f, 5f, 20f, 100f };
        int speedIndex;

        void Awake() => Instance = this;
        void OnDestroy() { if (Instance == this) Instance = null; }

        void Start()
        {
            if (panel) panel.SetActive(false);
        }

        void Update()
        {
            if (enableHotkeys)
            {
                if (Input.GetKeyDown(KeyCode.F1) && panel) panel.SetActive(!panel.activeSelf);
                if (Input.GetKeyDown(KeyCode.F2)) GrowAllOneStage();
                if (Input.GetKeyDown(KeyCode.F3)) WaterAll();
                if (Input.GetKeyDown(KeyCode.F4)) GiveSeeds(5);
                if (Input.GetKeyDown(KeyCode.F5)) CycleSpeed();
                if (Input.GetKeyDown(KeyCode.F6)) ResetAllPlots();
                if (Input.GetKeyDown(KeyCode.F7)) RefillCan();
                if (Input.GetKeyDown(KeyCode.F8)) Showcase();
                if (Input.GetKeyDown(KeyCode.F9)) AddStars(10);
                if (Input.GetKeyDown(KeyCode.F10)) AddCoins(100);
                if (Input.GetKeyDown(KeyCode.F11)) NewDay();
                if (Input.GetKeyDown(KeyCode.F12)) ResetSaveAndReload();
            }
            if (panel && panel.activeSelf && panelText) panelText.text = BuildPanelText();
        }

        public void GrowAllOneStage()
        {
            int n = 0;
            foreach (var p in FarmManager.Plots.ToList())
                if (p.Crop != null && p.Stage != CropStage.Full) { p.DevGrowOneStage(); n++; }
            Info($"Dev: grew {n} crop(s) one stage");
        }

        public void WaterAll()
        {
            int n = 0;
            foreach (var p in FarmManager.Plots.ToList())
                if (p.NeedsWater) { p.DevSetState(PlotState.Watered, p.Crop, p.Stage, p.StageProgress01); n++; }
            Info($"Dev: watered {n} plot(s)");
        }

        public void GiveSeeds(int amount)
        {
            foreach (var c in crops.Where(c => c && c.seedItem)) inventory.Add(c.seedItem, amount);
            Info($"Dev: +{amount} of every seed");
        }

        public void SetSpeed(float multiplier)
        {
            if (FarmManager.Instance) FarmManager.Instance.DevSpeedMultiplier = multiplier;
            Info($"Dev: growth speed x{multiplier:0.#}");
        }

        public void CycleSpeed()
        {
            speedIndex = (speedIndex + 1) % Speeds.Length;
            SetSpeed(Speeds[speedIndex]);
        }

        public void ResetAllPlots()
        {
            foreach (var p in FarmManager.Plots) p.DevSetState(PlotState.Untilled);
            Info("Dev: all plots reset");
        }

        public void RefillCan()
        {
            if (tools) tools.RefillWater();
            Info("Dev: watering can refilled");
        }

        /// <summary>
        /// Fills each crop's bed with one plot per state so every visual can be checked at a glance:
        /// Untilled, Tilled, Planted(seed, dry), Watered(seed), Growing(small, dry), Watered(medium), Grown, Harvested, Watered(small).
        /// </summary>
        public void Showcase()
        {
            // Plots live under <FarmBed_X>/Plots; group by bed and pick the crop whose name the bed carries.
            string BedName(FarmPlot p) => p.transform.parent && p.transform.parent.parent ? p.transform.parent.parent.name : p.name;
            var beds = FarmManager.Plots.GroupBy(BedName).OrderBy(g => g.Key).ToList();
            for (int b = 0; b < beds.Count; b++)
            {
                var crop = crops.FirstOrDefault(c => c && beds[b].Key.Contains(c.displayName)) ?? crops[b % crops.Length];
                var plots = beds[b].OrderBy(p => p.name).ToList();
                (PlotState s, CropStage st)[] layout =
                {
                    (PlotState.Untilled, CropStage.Seed), (PlotState.Tilled, CropStage.Seed), (PlotState.Planted, CropStage.Seed),
                    (PlotState.Watered, CropStage.Seed), (PlotState.Growing, CropStage.Small), (PlotState.Watered, CropStage.Medium),
                    (PlotState.Grown, CropStage.Full), (PlotState.Harvested, CropStage.Seed), (PlotState.Watered, CropStage.Small),
                };
                for (int i = 0; i < plots.Count && i < layout.Length; i++)
                    plots[i].DevSetState(layout[i].s, crop, layout[i].st, 0.3f);
            }
            Info("Dev: showcase - every state and stage laid out");
        }

        public void AddStars(int n) { if (FarmProgressionManager.Instance) FarmProgressionManager.Instance.DevAddStars(n); }
        public void AddCoins(int n) { if (Wallet.Instance) Wallet.Instance.Add(n); Info($"Dev: +{n} coins"); }
        public void NewDay() { if (FarmProgressionManager.Instance) FarmProgressionManager.Instance.DevNewDay(); Info("Dev: new day - fresh daily objectives"); }

        /// <summary>Deletes the save file and reloads the scene for a fresh start.</summary>
        public void ResetSaveAndReload()
        {
            var sm = SaveManager.Instance;
            if (sm) { sm.enabled = false; sm.DeleteSave(); }
            UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
        }

        void Info(string msg)
        {
            Debug.Log("[MossvaleDev] " + msg);
            var pos = tools ? tools.transform.position + Vector3.up * 2.2f : Vector3.zero;
            FarmFeedback.Raise(msg, pos, FeedbackKind.Info);
        }

        string BuildPanelText()
        {
            var sb = new StringBuilder();
            sb.AppendLine("<b>DEV TOOLS</b>  (F1 to hide)");
            sb.AppendLine($"Growth speed: x{FarmManager.GrowthMultiplier:0.#}   (F5)");
            sb.AppendLine("F2 grow all one stage   F3 water all");
            sb.AppendLine("F4 +5 seeds   F6 reset plots   F7 refill can");
            sb.AppendLine("F8 showcase every crop stage");
            sb.AppendLine("F9 +10 stars   F10 +100 coins   F11 new day   F12 reset save");
            var plots = FarmManager.Plots;
            sb.AppendLine($"Plots: {plots.Count}  |  " + string.Join("  ",
                plots.GroupBy(p => p.State).OrderBy(g => g.Key).Select(g => $"{g.Key}:{g.Count()}")));
            return sb.ToString();
        }
    }
}
