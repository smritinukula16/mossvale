using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace Mossvale.Farming
{
    /// <summary>
    /// Farming HUD: toolbar (selected tool, seed type, water level), inventory panel, interaction prompt,
    /// info toasts and a controls help panel (H). References are wired by the Mossvale farming setup.
    /// </summary>
    public class FarmHUD : MonoBehaviour
    {
        [Header("Player")]
        [SerializeField] PlayerToolController tools;
        [SerializeField] PlayerInteractor interactor;
        [SerializeField] Inventory inventory;

        [Header("Toolbar")]
        [SerializeField] ToolbarSlotUI[] slots;
        [SerializeField] Color slotColor = new Color(0.23f, 0.16f, 0.12f, 0.86f);
        [SerializeField] Color slotSelectedColor = new Color(0.45f, 0.30f, 0.18f, 0.95f);

        [Header("Inventory")]
        [SerializeField] InventoryRowUI rowTemplate;
        [SerializeField] Transform toolsList;
        [SerializeField] Transform seedsList;
        [SerializeField] Transform produceList;

        [Header("Prompt")]
        [SerializeField] GameObject promptRoot;
        [SerializeField] TMP_Text promptAction;
        [SerializeField] TMP_Text promptDetail;

        [Header("Toast + help")]
        [SerializeField] TMP_Text toast;
        [SerializeField] GameObject helpPanel;

        readonly Dictionary<ItemData, InventoryRowUI> rows = new Dictionary<ItemData, InventoryRowUI>();
        Coroutine toastRoutine;

        void Start()
        {
            if (rowTemplate) rowTemplate.gameObject.SetActive(false);
            foreach (var s in slots)
            {
                var slot = s;
                if (slot.button) slot.button.onClick.AddListener(() => OnSlotClicked(slot.tool));
            }
            if (inventory)
            {
                foreach (var item in inventory.Items) EnsureRow(item);
                inventory.ItemChanged += OnItemChanged;
            }
            if (tools) tools.Changed += RefreshToolbar;
            FarmFeedback.Raised += OnFeedback;
            RefreshToolbar();
            if (toast) toast.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (inventory) inventory.ItemChanged -= OnItemChanged;
            if (tools) tools.Changed -= RefreshToolbar;
            FarmFeedback.Raised -= OnFeedback;
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.H) && helpPanel) helpPanel.SetActive(!helpPanel.activeSelf);
            RefreshPrompt();
        }

        // ------------------------------------------------------------------ toolbar
        void OnSlotClicked(ToolType tool)
        {
            if (tools == null) return;
            if (tool == ToolType.Seeds && tools.SelectedTool == ToolType.Seeds) tools.CycleSeed(1); // click again = next seed
            else tools.SelectTool(tool);
        }

        void RefreshToolbar()
        {
            if (tools == null || inventory == null) return;
            foreach (var s in slots)
            {
                var item = inventory.FindTool(s.tool);
                s.gameObject.SetActive(item != null);
                if (item == null) continue;
                bool selected = tools.SelectedTool == s.tool;
                if (s.background) s.background.color = selected ? slotSelectedColor : slotColor;
                if (s.selectedFrame) s.selectedFrame.enabled = selected;
                s.transform.localScale = Vector3.one * (selected ? 1.08f : 1f);

                var iconItem = item;
                string name = item.displayName;
                string badge = "";
                if (s.tool == ToolType.Seeds && tools.SelectedSeed != null)
                {
                    iconItem = tools.SelectedSeed;
                    name = tools.SelectedSeed.displayName;
                    badge = inventory.Count(tools.SelectedSeed).ToString();
                }
                if (s.icon) { s.icon.sprite = iconItem.icon; s.icon.color = iconItem.icon ? Color.white : iconItem.tint; }
                if (s.nameLabel) s.nameLabel.text = name;
                if (s.badge) { s.badge.text = badge; s.badge.transform.parent.gameObject.SetActive(badge != ""); }
                if (s.barFill)
                {
                    bool can = s.tool == ToolType.WateringCan;
                    s.barFill.transform.parent.gameObject.SetActive(can);
                    if (can) s.barFill.fillAmount = tools.WaterCapacity > 0 ? (float)tools.Water / tools.WaterCapacity : 0f;
                }
            }
        }

        // ------------------------------------------------------------------ inventory
        void OnItemChanged(ItemData item, int count, int delta)
        {
            var row = EnsureRow(item);
            if (row) row.SetCount(count, true);
        }

        InventoryRowUI EnsureRow(ItemData item)
        {
            if (item == null || rowTemplate == null) return null;
            if (rows.TryGetValue(item, out var row)) return row;
            var parent = item.category == ItemCategory.Tool ? toolsList : item.category == ItemCategory.Seed ? seedsList : produceList;
            row = Instantiate(rowTemplate, parent);
            row.gameObject.SetActive(true);
            row.name = "Row_" + item.itemId;
            row.Bind(item, inventory.Count(item));
            rows[item] = row;
            return row;
        }

        /// <summary>Text currently shown for an item's count (tests / debugging).</summary>
        public string CountTextFor(ItemData item) => rows.TryGetValue(item, out var r) && r.countLabel ? r.countLabel.text : null;
        public string PromptText => promptRoot && promptRoot.activeSelf ? promptAction.text + " | " + promptDetail.text : "";

        // ------------------------------------------------------------------ prompt
        void RefreshPrompt()
        {
            if (promptRoot == null || interactor == null) return;
            var target = interactor.Current;
            if (target == null) { promptRoot.SetActive(false); return; }
            promptRoot.SetActive(true);
            var p = interactor.CurrentPrompt;
            string keys = interactor.CurrentInRange ? "<color=#F8C72D>[E]</color> / Click" : "Click to walk there";
            promptAction.text = $"{keys}  {p.action}";
            promptAction.color = p.canPerform ? new Color(0.97f, 0.95f, 0.89f) : new Color(1f, 0.72f, 0.62f);
            promptDetail.text = p.detail;
        }

        // ------------------------------------------------------------------ toast
        void OnFeedback(string message, Vector3 pos, FeedbackKind kind)
        {
            if (kind != FeedbackKind.Info || toast == null) return;
            if (toastRoutine != null) StopCoroutine(toastRoutine);
            toastRoutine = StartCoroutine(ShowToast(message));
        }

        IEnumerator ShowToast(string message)
        {
            toast.gameObject.SetActive(true);
            toast.text = message;
            yield return new WaitForSecondsRealtime(2.2f);
            toast.gameObject.SetActive(false);
            toastRoutine = null;
        }
    }
}
