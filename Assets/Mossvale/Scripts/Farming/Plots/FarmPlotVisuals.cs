using System.Collections;
using UnityEngine;

namespace Mossvale.Farming
{
    /// <summary>
    /// Presents a FarmPlot: weeds when untilled, a furrow ridge (dry / wet / depleted), the crop stage model,
    /// a "needs water" droplet, a ready-to-harvest sparkle and one-shot effects for actions and growth.
    /// All prefab/material references are assigned by the Mossvale farming setup and can be swapped in the Inspector.
    /// </summary>
    [RequireComponent(typeof(FarmPlot))]
    public class FarmPlotVisuals : MonoBehaviour
    {
        [Header("Soil")]
        [SerializeField] GameObject weeds;          // child shown while untilled
        [SerializeField] Renderer ridge;            // child furrow, shown once tilled
        [SerializeField] GameObject stubble;        // child shown after harvest
        [SerializeField] Material dryMaterial;
        [SerializeField] Material wetMaterial;
        [SerializeField] Material depletedMaterial;

        [Header("Crop")]
        [SerializeField] Transform cropAnchor;      // top of the ridge

        [Header("Indicators")]
        [SerializeField] Transform needsWaterIndicator;
        [SerializeField] ParticleSystem readyLoopFx;

        [Header("One-shot effects")]
        [SerializeField] ParticleSystem tillFx;
        [SerializeField] ParticleSystem waterFx;
        [SerializeField] ParticleSystem growFx;
        [SerializeField] ParticleSystem harvestFx;

        FarmPlot plot;
        GameObject cropInstance;
        GameObject cropInstancePrefab;
        Vector3 indicatorBase;
        Coroutine pop;

        /// <summary>Name of the stage prefab currently shown (tests use this).</summary>
        public string CurrentCropVisualName => cropInstance != null ? cropInstancePrefab.name : null;

        void Awake()
        {
            plot = GetComponent<FarmPlot>();
            if (needsWaterIndicator) indicatorBase = needsWaterIndicator.localPosition;
        }

        void OnEnable()
        {
            plot.Changed += OnChanged;
            plot.StageAdvanced += OnStageAdvanced;
            plot.ActionPerformed += OnAction;
            Refresh();
        }

        void OnDisable()
        {
            plot.Changed -= OnChanged;
            plot.StageAdvanced -= OnStageAdvanced;
            plot.ActionPerformed -= OnAction;
        }

        void Update()
        {
            if (needsWaterIndicator && needsWaterIndicator.gameObject.activeSelf)
            {
                float t = Time.time * 2.2f + transform.position.x;
                needsWaterIndicator.localPosition = indicatorBase + Vector3.up * (Mathf.Sin(t) * 0.07f);
                needsWaterIndicator.Rotate(Vector3.up, 60f * Time.deltaTime, Space.World);
            }
        }

        void OnChanged(FarmPlot p) => Refresh();

        void OnStageAdvanced(FarmPlot p, CropStage s)
        {
            Play(growFx);
            Pop();
        }

        void OnAction(FarmPlot p, ToolType tool, bool success)
        {
            if (!success) return;
            switch (tool)
            {
                case ToolType.Hoe: Play(tillFx); break;
                case ToolType.Seeds: Play(tillFx); Pop(); break;
                case ToolType.WateringCan: Play(waterFx); break;
                case ToolType.Basket: Play(harvestFx); break;
            }
        }

        /// <summary>Makes every visual match the plot's current state.</summary>
        public void Refresh()
        {
            if (plot == null) plot = GetComponent<FarmPlot>();
            var s = plot.State;

            if (weeds) weeds.SetActive(s == PlotState.Untilled);
            if (stubble) stubble.SetActive(s == PlotState.Harvested);
            if (ridge)
            {
                ridge.gameObject.SetActive(s != PlotState.Untilled);
                var m = s == PlotState.Watered ? wetMaterial : s == PlotState.Harvested ? depletedMaterial : dryMaterial;
                if (m != null && ridge.sharedMaterial != m) ridge.sharedMaterial = m;
            }

            var wanted = plot.Crop != null ? plot.Crop.PrefabFor(plot.Stage) : null;
            if (wanted != cropInstancePrefab)
            {
                if (cropInstance != null) Destroy(cropInstance);
                cropInstance = null;
                cropInstancePrefab = wanted;
                if (wanted != null)
                {
                    var parent = cropAnchor != null ? cropAnchor : transform;
                    cropInstance = Instantiate(wanted, parent);
                    cropInstance.transform.localPosition = Vector3.zero;
                    cropInstance.transform.localRotation = Quaternion.identity;
                    foreach (var c in cropInstance.GetComponentsInChildren<Collider>()) Destroy(c);
                }
            }

            if (needsWaterIndicator) needsWaterIndicator.gameObject.SetActive(plot.NeedsWater);
            if (readyLoopFx)
            {
                if (s == PlotState.Grown && !readyLoopFx.isPlaying) readyLoopFx.Play();
                else if (s != PlotState.Grown && readyLoopFx.isPlaying) readyLoopFx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        static void Play(ParticleSystem fx)
        {
            if (fx == null) return;
            fx.Clear(true);
            fx.Play(true);
        }

        void Pop()
        {
            if (!isActiveAndEnabled || cropAnchor == null) return;
            if (pop != null) StopCoroutine(pop);
            pop = StartCoroutine(PopRoutine());
        }

        IEnumerator PopRoutine()
        {
            const float duration = 0.35f;
            for (float t = 0; t < duration; t += Time.deltaTime)
            {
                float k = t / duration;
                float s = 1f + Mathf.Sin(k * Mathf.PI) * 0.25f * (1f - k) - (1f - k) * 0.35f * (1f - k);
                cropAnchor.localScale = Vector3.one * Mathf.Max(0.2f, s);
                yield return null;
            }
            cropAnchor.localScale = Vector3.one;
            pop = null;
        }
    }
}
