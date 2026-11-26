using System.Linq;
using UnityEngine;

namespace Mossvale.Farming
{
    /// <summary>Lookup of every item and crop by id (used by the save system).</summary>
    [CreateAssetMenu(menuName = "Mossvale/Farming/Item Database", fileName = "ItemDatabase")]
    public class ItemDatabase : ScriptableObject
    {
        public ItemData[] items;
        public CropData[] crops;

        public ItemData FindItem(string id) => string.IsNullOrEmpty(id) ? null : items.FirstOrDefault(i => i && i.itemId == id);
        public CropData FindCrop(string id) => string.IsNullOrEmpty(id) ? null : crops.FirstOrDefault(c => c && c.cropId == id);
    }
}
