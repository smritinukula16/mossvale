using UnityEngine;

namespace Mossvale.Farming
{
    /// <summary>Locks a raised bed's plots until FarmProgressionManager unlocks its bed id (as a level reward).</summary>
    public class BedLock : MonoBehaviour
    {
        [SerializeField] string bedId = "carrot";
        [SerializeField] string displayName = "Carrot";
        [SerializeField] WorldBadge badge;
        [SerializeField] ParticleSystem unlockFx;

        public string BedId => bedId;
        public string DisplayName => displayName;
        public bool IsLocked { get; private set; }

        public void SetLocked(bool locked, string reason)
        {
            IsLocked = locked;
            foreach (var p in GetComponentsInChildren<FarmPlot>(true)) p.SetLocked(locked, reason);
            if (badge)
            {
                badge.gameObject.SetActive(locked);
                badge.SetText($"<b>{displayName} bed</b>\n{reason}");
            }
        }

        public void PlayUnlockEffect() { if (unlockFx) unlockFx.Play(true); }
    }
}
