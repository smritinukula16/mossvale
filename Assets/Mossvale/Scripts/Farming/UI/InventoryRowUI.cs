using System.Collections;
using TMPro;
using UnityEngine;

namespace Mossvale.Farming
{
    /// <summary>One inventory line: icon, name, count. Pulses when the count changes.</summary>
    public class InventoryRowUI : MonoBehaviour
    {
        public UnityEngine.UI.Image icon;
        public TMP_Text nameLabel;
        public TMP_Text countLabel;
        public ItemData Item { get; private set; }

        Coroutine pulse;

        public void Bind(ItemData item, int count)
        {
            Item = item;
            if (icon) { icon.sprite = item.icon; icon.color = item.icon ? Color.white : item.tint; }
            if (nameLabel) nameLabel.text = item.displayName;
            SetCount(count, false);
        }

        public void SetCount(int count, bool animate)
        {
            if (countLabel) countLabel.text = Item.category == ItemCategory.Tool ? "" : "x" + count;
            if (animate && isActiveAndEnabled)
            {
                if (pulse != null) StopCoroutine(pulse);
                pulse = StartCoroutine(Pulse());
            }
        }

        IEnumerator Pulse()
        {
            for (float t = 0; t < 0.3f; t += Time.unscaledDeltaTime)
            {
                transform.localScale = Vector3.one * (1f + 0.15f * Mathf.Sin(t / 0.3f * Mathf.PI));
                yield return null;
            }
            transform.localScale = Vector3.one;
        }
    }
}
