using System;
using System.Collections;
using UnityEngine;

namespace Mossvale.Farming
{
    /// <summary>
    /// The Heart of Mossvale: a magical tree the player restores by spending blue stars. Each restoration stage
    /// swaps in a richer visual (stageVisuals[i]); add more stages by adding a child + a HeartStageDefinition.
    /// </summary>
    public class HeartOfMossvale : Building, ISaveable
    {
        [Tooltip("One root per stage; only the current stage is shown.")]
        [SerializeField] GameObject[] stageVisuals;
        [SerializeField] ParticleSystem restoreFx;
        [SerializeField] WorldBadge badge;

        int stage;
        Coroutine pop;

        public int Stage => stage;
        public int MaxStage => Mathf.Min(stageVisuals.Length, Config.heartStages.Count) - 1;
        public bool IsFullyRestored => stage >= MaxStage;
        ProgressionConfig Config => FarmProgressionManager.Instance.Config;
        public HeartStageDefinition CurrentStage => Config.heartStages[stage];
        public HeartStageDefinition NextStage => IsFullyRestored ? null : Config.heartStages[stage + 1];
        public event Action Restored;

        public override string PromptDetail => $"Restoration {stage}/{MaxStage}" + (NextStage != null ? $" - next costs {NextStage.starCost} {Glyphs.Star}" : " - fully restored");

        void Start()
        {
            ApplyVisual(false);
            if (Progression) Progression.Changed += RefreshBadge;
            RefreshBadge();
        }

        void OnDestroy() { if (Progression) Progression.Changed -= RefreshBadge; }

        public bool CanRestore(out string reason)
        {
            reason = null;
            var next = NextStage;
            if (next == null) { reason = "Fully restored"; return false; }
            if (Progression.Level < next.requiredLevel) { reason = $"Needs Farm Level {next.requiredLevel}"; return false; }
            if (Progression.StarBalance < next.starCost) { reason = $"Needs {next.starCost} {Glyphs.Star} (you have {Progression.StarBalance})"; return false; }
            return true;
        }

        /// <summary>Spends stars to restore the next stage.</summary>
        public bool TryRestore()
        {
            if (!CanRestore(out var reason))
            {
                FarmFeedback.Raise(reason, InteractionPoint, FeedbackKind.Failure);
                return false;
            }
            if (!Progression.TrySpendStars(NextStage.starCost)) return false;
            stage++;
            ApplyVisual(true);
            FarmFeedback.Raise($"The Heart of Mossvale grows: {CurrentStage.title}", InteractionPoint + Vector3.up, FeedbackKind.Star);
            Progression.ReportStat(Stats.HeartStage, stage);
            RefreshBadge();
            Restored?.Invoke();
            SaveManager.MarkDirty();
            return true;
        }

        void ApplyVisual(bool animate)
        {
            for (int i = 0; i < stageVisuals.Length; i++)
                if (stageVisuals[i]) stageVisuals[i].SetActive(i == stage);
            if (!animate) return;
            if (restoreFx) restoreFx.Play(true);
            var v = stage < stageVisuals.Length ? stageVisuals[stage] : null;
            if (v != null)
            {
                if (pop != null) StopCoroutine(pop);
                pop = StartCoroutine(Pop(v.transform));
            }
        }

        IEnumerator Pop(Transform t)
        {
            for (float a = 0; a < 0.6f; a += Time.deltaTime)
            {
                float k = a / 0.6f;
                t.localScale = Vector3.one * Mathf.LerpUnclamped(0.4f, 1f, 1f + 2.2f * Mathf.Pow(k - 1f, 3) + 1.2f * Mathf.Pow(k - 1f, 2));
                yield return null;
            }
            t.localScale = Vector3.one;
            pop = null;
        }

        void RefreshBadge()
        {
            if (badge == null) return;
            badge.SetText($"<b>Heart of Mossvale</b>\n{CurrentStage.title}  {stage}/{MaxStage}");
        }

        // ------------------------------------------------------------------ save
        [Serializable] class State { public int stage; }
        public string SaveKey => "heart_of_mossvale";
        public int RestoreOrder => 40;
        public string CaptureState() => JsonUtility.ToJson(new State { stage = stage });
        public void RestoreState(string json)
        {
            stage = Mathf.Clamp(JsonUtility.FromJson<State>(json).stage, 0, Mathf.Max(0, MaxStage));
            ApplyVisual(false);
            RefreshBadge();
        }

        /// <summary>Dev/test helper.</summary>
        public void DevSetStage(int s) { stage = Mathf.Clamp(s, 0, MaxStage); ApplyVisual(false); RefreshBadge(); }
    }
}
