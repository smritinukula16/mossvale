using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Mossvale.Farming
{
    /// <summary>
    /// Selected tool + selected seed + watering-can water level, and the tool model held by the player.
    /// Keys: 1-4 select a tool, mouse wheel cycles tools, Tab cycles seed type.
    /// </summary>
    public class PlayerToolController : MonoBehaviour, ISaveable
    {
        [SerializeField] Inventory inventory;
        [SerializeField] Transform toolSocket;
        [SerializeField] ToolType startingTool = ToolType.Hoe;
        [SerializeField] bool readKeyboardInput = true;

        public Inventory Inventory => inventory;
        public ToolType SelectedTool { get; private set; } = ToolType.None;
        public ItemData SelectedToolItem { get; private set; }
        public ItemData SelectedSeed { get; private set; }
        public int Water { get; private set; }
        public int WaterCapacity => FarmManager.WateringCanCapacity;
        public bool HasWater => Water > 0;

        /// <summary>Raised whenever the tool, seed or water level changes.</summary>
        public event Action Changed;

        GameObject held;
        Coroutine swing;

        void Awake()
        {
            if (inventory == null) inventory = GetComponent<Inventory>();
        }

        void OnEnable() { if (inventory) inventory.ItemChanged += OnInventoryChanged; }
        void OnDisable() { if (inventory) inventory.ItemChanged -= OnInventoryChanged; }

        void Start()
        {
            Water = WaterCapacity;
            SelectedSeed = Seeds().FirstOrDefault(s => inventory.Count(s) > 0) ?? Seeds().FirstOrDefault();
            SelectTool(startingTool, silent: true);
        }

        void Update()
        {
            if (!readKeyboardInput || GameplayInput.Blocked) return;
            if (Input.GetKeyDown(KeyCode.Alpha1)) SelectTool(ToolType.Hoe);
            if (Input.GetKeyDown(KeyCode.Alpha2)) SelectTool(ToolType.Seeds);
            if (Input.GetKeyDown(KeyCode.Alpha3)) SelectTool(ToolType.WateringCan);
            if (Input.GetKeyDown(KeyCode.Alpha4)) SelectTool(ToolType.Basket);
            float wheel = Input.mouseScrollDelta.y;
            if (wheel > 0.1f) CycleTool(-1); else if (wheel < -0.1f) CycleTool(1);
            if (Input.GetKeyDown(KeyCode.Tab)) CycleSeed(1);
        }

        public IEnumerable<ItemData> Seeds() => inventory.ItemsOf(ItemCategory.Seed);
        public IEnumerable<ItemData> OwnedTools() =>
            inventory.ItemsOf(ItemCategory.Tool).Where(t => inventory.Count(t) > 0).OrderBy(t => (int)t.toolType);

        public bool SelectTool(ToolType type, bool silent = false)
        {
            var item = inventory.FindTool(type);
            if (item == null)
            {
                if (!silent) FarmFeedback.Raise($"You don't have a {type}", transform.position + Vector3.up * 2f, FeedbackKind.Failure);
                return false;
            }
            SelectedTool = type;
            SelectedToolItem = item;
            RefreshHeldModel();
            Changed?.Invoke();
            return true;
        }

        public void CycleTool(int dir)
        {
            var tools = OwnedTools().ToList();
            if (tools.Count == 0) return;
            int i = Mathf.Max(0, tools.FindIndex(t => t.toolType == SelectedTool));
            SelectTool(tools[(i + dir + tools.Count) % tools.Count].toolType);
        }

        public void SelectSeed(ItemData seed)
        {
            if (seed == null || seed.category != ItemCategory.Seed) return;
            SelectedSeed = seed;
            Changed?.Invoke();
        }

        public void CycleSeed(int dir)
        {
            var seeds = Seeds().ToList();
            if (seeds.Count == 0) return;
            int i = Mathf.Max(0, seeds.IndexOf(SelectedSeed));
            SelectSeed(seeds[(i + dir + seeds.Count) % seeds.Count]);
            FarmFeedback.Raise($"Seeds: {SelectedSeed.displayName} ({inventory.Count(SelectedSeed)})", transform.position + Vector3.up * 2.2f, FeedbackKind.Info);
        }

        public bool ConsumeWater()
        {
            if (Water <= 0) return false;
            Water--;
            Changed?.Invoke();
            SaveManager.MarkDirty();
            return true;
        }

        public void RefillWater()
        {
            Water = WaterCapacity;
            Changed?.Invoke();
        }

        // ------------------------------------------------------------------ save
        [Serializable] class State { public int water; public ToolType tool; public string seed; }
        public string SaveKey => "player_tools";
        public int RestoreOrder => 25;
        public string CaptureState() => JsonUtility.ToJson(new State { water = Water, tool = SelectedTool, seed = SelectedSeed ? SelectedSeed.itemId : "" });
        public void RestoreState(string json)
        {
            var s = JsonUtility.FromJson<State>(json);
            Water = Mathf.Clamp(s.water, 0, WaterCapacity);
            var seed = FarmManager.FindItem(s.seed);
            if (seed != null) SelectedSeed = seed;
            if (!SelectTool(s.tool, silent: true)) Changed?.Invoke();
        }

        /// <summary>Dev/test helper.</summary>
        public void SetWater(int amount) { Water = Mathf.Clamp(amount, 0, WaterCapacity); Changed?.Invoke(); }

        void OnInventoryChanged(ItemData item, int count, int delta)
        {
            // Out of the selected seed? Switch to another seed that is still in stock.
            if (item == SelectedSeed && count == 0)
            {
                var next = Seeds().FirstOrDefault(s => inventory.Count(s) > 0);
                if (next != null && next != SelectedSeed)
                {
                    SelectedSeed = next;
                    FarmFeedback.Raise($"Out of {item.displayName} - switched to {next.displayName}", transform.position + Vector3.up * 2.2f, FeedbackKind.Info);
                }
            }
            Changed?.Invoke();
        }

        void RefreshHeldModel()
        {
            if (held != null) Destroy(held);
            held = null;
            if (toolSocket == null || SelectedToolItem == null || SelectedToolItem.heldPrefab == null) return;
            held = Instantiate(SelectedToolItem.heldPrefab, toolSocket);
            held.name = "Held_" + SelectedToolItem.displayName;
            held.transform.localPosition = SelectedToolItem.heldOffset;
            held.transform.localRotation = Quaternion.Euler(SelectedToolItem.heldEuler);
            held.transform.localScale = Vector3.one * SelectedToolItem.heldScale;
            // A held prop must never block movement or clicks.
            foreach (var c in held.GetComponentsInChildren<Collider>()) Destroy(c);
        }

        /// <summary>Short swing/tilt of the held tool when an action succeeds.</summary>
        public void PlayUseAnimation()
        {
            if (toolSocket == null || !isActiveAndEnabled) return;
            if (swing != null) StopCoroutine(swing);
            swing = StartCoroutine(Swing());
        }

        IEnumerator Swing()
        {
            var baseRot = Quaternion.identity;
            float angle = SelectedTool == ToolType.WateringCan ? 35f : 55f;
            Vector3 axis = SelectedTool == ToolType.WateringCan ? Vector3.forward : Vector3.right;
            const float duration = 0.35f;
            for (float t = 0; t < duration; t += Time.deltaTime)
            {
                float k = Mathf.Sin(t / duration * Mathf.PI);
                toolSocket.localRotation = baseRot * Quaternion.AngleAxis(angle * k, axis);
                yield return null;
            }
            toolSocket.localRotation = baseRot;
            swing = null;
        }
    }
}
