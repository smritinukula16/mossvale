using TMPro;
using UnityEngine;

namespace Mossvale.Farming
{
    /// <summary>Shared look for all Mossvale building panels and progression HUD (sprites, colours, font).</summary>
    [CreateAssetMenu(menuName = "Mossvale/UI Style", fileName = "MossvaleUIStyle")]
    public class UIStyle : ScriptableObject
    {
        public TMP_FontAsset font;
        public Material outlineFont;

        [Header("Sprites (9-sliced where it makes sense)")]
        public Sprite panel;      // rounded cream panel
        public Sprite wood;       // rounded wooden frame with grain
        public Sprite card;       // small rounded card
        public Sprite button;     // rounded button (tinted)
        public Sprite bar;        // rounded bar (background + fill)
        public Sprite star, coin, lockIcon, check, close, glow, heart, circle;

        [Header("Colours")]
        public Color cream = new Color32(0xFB, 0xF3, 0xDE, 0xFF);
        public Color creamDark = new Color32(0xEE, 0xDD, 0xB8, 0xFF);
        public Color wood1 = new Color32(0x9A, 0x63, 0x3A, 0xFF);
        public Color woodDark = new Color32(0x5E, 0x3B, 0x22, 0xFF);
        public Color green = new Color32(0x6F, 0xAE, 0x48, 0xFF);
        public Color greenDark = new Color32(0x4D, 0x8A, 0x33, 0xFF);
        public Color blue = new Color32(0x4C, 0x9B, 0xE8, 0xFF);
        public Color blueLight = new Color32(0xA9, 0xD6, 0xFF, 0xFF);
        public Color gold = new Color32(0xF2, 0xB6, 0x32, 0xFF);
        public Color red = new Color32(0xC8, 0x55, 0x3D, 0xFF);
        public Color text = new Color32(0x4A, 0x30, 0x20, 0xFF);
        public Color textMuted = new Color32(0x8A, 0x70, 0x58, 0xFF);
        public Color textLight = new Color32(0xFF, 0xF8, 0xEA, 0xFF);
        public Color disabled = new Color32(0xC9, 0xBC, 0xA4, 0xFF);
    }
}
