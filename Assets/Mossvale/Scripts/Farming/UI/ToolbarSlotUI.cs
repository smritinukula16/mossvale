using TMPro;
using UnityEngine;

namespace Mossvale.Farming
{
    /// <summary>One toolbar slot (tool icon, key hint, name, optional count badge / water bar).</summary>
    public class ToolbarSlotUI : MonoBehaviour
    {
        public ToolType tool;
        public UnityEngine.UI.Image background;
        public UnityEngine.UI.Image icon;
        public UnityEngine.UI.Image selectedFrame;
        public TMP_Text keyLabel;
        public TMP_Text nameLabel;
        public TMP_Text badge;
        public UnityEngine.UI.Image barFill;
        public UnityEngine.UI.Button button;
    }
}
