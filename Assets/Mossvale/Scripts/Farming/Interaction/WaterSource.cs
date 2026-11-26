using UnityEngine;

namespace Mossvale.Farming
{
    /// <summary>Refills the watering can (the well, the pond...).</summary>
    public class WaterSource : Interactable
    {
        [SerializeField] string displayName = "Well";

        public override string DisplayName => displayName;

        public override InteractionPrompt GetPrompt(PlayerToolController tools)
        {
            if (tools == null) return new InteractionPrompt("Refill watering can", displayName, false);
            if (tools.SelectedTool != ToolType.WateringCan)
                return new InteractionPrompt("Refill watering can", "Select the Watering Can (3) first", false);
            if (tools.Water >= tools.WaterCapacity)
                return new InteractionPrompt("Refill watering can", "Watering can is already full", false);
            return new InteractionPrompt("Refill watering can", $"{displayName} - {tools.Water}/{tools.WaterCapacity}", true);
        }

        public override bool Interact(PlayerToolController tools)
        {
            var p = GetPrompt(tools);
            if (!p.canPerform)
            {
                FarmFeedback.Raise(p.detail, InteractionPoint, FeedbackKind.Failure);
                return false;
            }
            tools.RefillWater();
            FarmFeedback.Raise($"Watering can refilled ({tools.Water}/{tools.WaterCapacity})", InteractionPoint, FeedbackKind.Success);
            return true;
        }
    }
}
