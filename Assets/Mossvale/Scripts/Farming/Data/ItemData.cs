using UnityEngine;

namespace Mossvale.Farming
{
    /// <summary>Any inventory item: a tool, a seed packet or harvested produce.</summary>
    [CreateAssetMenu(menuName = "Mossvale/Farming/Item", fileName = "Item_")]
    public class ItemData : ScriptableObject
    {
        public string itemId = "item";
        public string displayName = "Item";
        public ItemCategory category = ItemCategory.Produce;
        [Tooltip("Only for tools.")] public ToolType toolType = ToolType.None;
        [Tooltip("Only for seeds: the crop this seed grows.")] public CropData plantsCrop;
        public Sprite icon;
        public Color tint = Color.white;
        [Tooltip("Coins paid per unit at the market. 0 = cannot be sold.")] [Min(0)] public int sellPrice = 0;
        [Tooltip("Model shown in the player's hand when this tool is selected.")] public GameObject heldPrefab;
        public Vector3 heldOffset = Vector3.zero;
        public Vector3 heldEuler = Vector3.zero;
        public float heldScale = 1f;
    }
}
