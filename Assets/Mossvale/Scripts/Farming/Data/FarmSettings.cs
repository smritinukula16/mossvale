using UnityEngine;

namespace Mossvale.Farming
{
    /// <summary>Global farming tuning, shared by every plot and the player.</summary>
    [CreateAssetMenu(menuName = "Mossvale/Farming/Farm Settings", fileName = "FarmSettings")]
    public class FarmSettings : ScriptableObject
    {
        [Tooltip("Multiplies every crop's growth speed. 1 = normal, 10 = ten times faster.")]
        [Min(0.01f)] public float growthSpeedMultiplier = 1f;
        [Tooltip("If true, a crop must be watered again after every growth stage.")]
        public bool requireWaterEachStage = true;
        [Min(1)] public int wateringCanCapacity = 10;
        [Tooltip("Max distance (m) between the player and an object's edge to interact with it.")]
        [Min(0.5f)] public float interactionRange = 2.2f;
    }
}
