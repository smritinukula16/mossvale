using UnityEngine;

namespace Mossvale.Farming
{
    /// <summary>
    /// A building the player can click or walk up to (E) to open its UI panel. Reuses the farming interaction
    /// flow (hover prompt, outline, walk-then-use, facing). Subclasses add building-specific actions.
    /// </summary>
    public class Building : Interactable
    {
        [SerializeField] protected string displayName = "Building";
        [SerializeField] protected string actionLabel = "Open";
        [Tooltip("Max distance (m) from the building's edge to interact.")]
        [SerializeField] float interactionRange = 3.5f;
        [SerializeField] protected UIPanel panel;
        [SerializeField] protected Inventory playerInventory;

        public override string DisplayName => displayName;
        public override float InteractionRange => interactionRange;
        public override bool PlaysToolAnimation => false;
        public UIPanel Panel => panel;
        public Inventory PlayerInventory => playerInventory;
        protected FarmProgressionManager Progression => FarmProgressionManager.Instance;

        /// <summary>Second line of the hover prompt.</summary>
        public virtual string PromptDetail => displayName;

        public override InteractionPrompt GetPrompt(PlayerToolController tools) =>
            new InteractionPrompt(actionLabel, PromptDetail, panel != null);

        public override bool Interact(PlayerToolController tools)
        {
            if (panel == null) return false;
            panel.Open(this);
            return true;
        }
    }
}
