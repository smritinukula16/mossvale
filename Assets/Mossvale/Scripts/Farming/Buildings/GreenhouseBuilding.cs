using UnityEngine;

namespace Mossvale.Farming
{
    /// <summary>
    /// Greenhouse: locked until the farm reaches the configured level and lifetime stars. Once unlocked it speeds up
    /// all crop growth and runs a seed nursery (1 produce -> several seeds).
    /// </summary>
    public class GreenhouseBuilding : Building
    {
        [SerializeField] WorldBadge badge;
        [SerializeField] ParticleSystem unlockFx;

        public bool IsUnlocked => Progression && Progression.GreenhouseUnlocked;
        public bool RequirementsMet => Progression && Progression.GreenhouseRequirementsMet;
        public override string PromptDetail => IsUnlocked ? "Greenhouse - seed nursery" : "Locked - view requirements";

        void Start()
        {
            if (Progression) Progression.Changed += RefreshBadge;
            RefreshBadge();
        }

        void OnDestroy() { if (Progression) Progression.Changed -= RefreshBadge; }

        void RefreshBadge()
        {
            if (badge == null || Progression == null) return;
            var c = Progression.Config;
            badge.gameObject.SetActive(!IsUnlocked);
            badge.SetText(RequirementsMet ? "<b>Greenhouse</b>\nReady to unlock!" :
                $"<b>Greenhouse</b>\nLevel {c.greenhouseRequiredLevel} + {c.greenhouseRequiredStars} {Glyphs.Star}");
        }

        public bool TryUnlock()
        {
            if (Progression == null || !Progression.TryUnlockGreenhouse()) return false;
            if (unlockFx) unlockFx.Play();
            RefreshBadge();
            return true;
        }

        /// <summary>Seed nursery: turns one produce into seeds of the same crop.</summary>
        public bool Nurse(CropData crop)
        {
            if (!IsUnlocked) { FarmFeedback.Raise("Unlock the greenhouse first", InteractionPoint, FeedbackKind.Failure); return false; }
            if (crop == null || !playerInventory.TryRemove(crop.produceItem, 1))
            {
                FarmFeedback.Raise($"You need a {crop?.produceItem?.displayName ?? "crop"}", InteractionPoint, FeedbackKind.Failure);
                return false;
            }
            int n = Progression.Config.nurserySeedsPerProduce;
            playerInventory.Add(crop.seedItem, n);
            FarmFeedback.Raise($"+{n} {crop.seedItem.displayName}", InteractionPoint, FeedbackKind.Success);
            return true;
        }
    }
}
