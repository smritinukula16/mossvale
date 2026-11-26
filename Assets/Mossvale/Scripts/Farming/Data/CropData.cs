using UnityEngine;

namespace Mossvale.Farming
{
    /// <summary>Configurable crop definition: visuals per stage, growth timing and harvest yield.</summary>
    [CreateAssetMenu(menuName = "Mossvale/Farming/Crop", fileName = "Crop_")]
    public class CropData : ScriptableObject
    {
        public string cropId = "crop";
        public string displayName = "Crop";
        public ItemData seedItem;
        public ItemData produceItem;

        [Header("Growth")]
        [Tooltip("Seconds of watered growth needed to advance one stage (Seed->Small->Medium->Full). " +
                 "Scaled by FarmSettings.growthSpeedMultiplier and the dev speed.")]
        [Min(0.1f)] public float secondsPerStage = 15f;

        [Header("Visuals (index = CropStage: Seed, Small, Medium, Full)")]
        public GameObject[] stagePrefabs = new GameObject[4];

        [Header("Harvest")]
        [Min(1)] public int minYield = 2;
        [Min(1)] public int maxYield = 3;
        [Tooltip("Seeds returned on harvest so the loop never dead-ends.")]
        [Min(0)] public int minSeedsBack = 1;
        [Min(0)] public int maxSeedsBack = 2;

        public float TotalGrowSeconds => secondsPerStage * 3f;

        public GameObject PrefabFor(CropStage stage)
        {
            int i = (int)stage;
            return stagePrefabs != null && i < stagePrefabs.Length ? stagePrefabs[i] : null;
        }
    }
}
