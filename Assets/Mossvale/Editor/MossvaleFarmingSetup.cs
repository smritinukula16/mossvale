// Mossvale farming setup (Unity 6 / URP).
// Creates the farming assets and wires the gameplay + HUD into Mossvale_Main.unity IN PLACE (the environment is kept).
//
// Menu:  Mossvale > Farming > Setup Farming (assets + scene)
//        Mossvale > Farming > Re-apply To Scene Only
//        Mossvale > Dev > ...  (Play Mode helpers)
// Batch: -executeMethod Mossvale.EditorTools.MossvaleFarmingSetup.BatchImportTMP     (once, imports TMP Essentials)
//        -executeMethod Mossvale.EditorTools.MossvaleFarmingSetup.BatchSetupFarming
//
// Safe to re-run: everything it creates lives under Assets/Mossvale/{Data,Materials,Prefabs,UI}/Farming and the
// scene objects named "Farming", "Farming_HUD", "EventSystem", "Farm_Beds". Decorative crop beds that are turned into
// playable beds are only deactivated (never deleted) and are re-activated before a re-run.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mossvale.Farming;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Mossvale.EditorTools
{
    public static class MossvaleFarmingSetup
    {
        const string Root = "Assets/Mossvale";
        const string ScenePath = Root + "/Scenes/Mossvale_Main.unity";
        const string FarmModels = Root + "/Models/Farming";
        const string EnvPrefabs = Root + "/Prefabs";
        const string DataDir = Root + "/Data/Farming";
        const string MatDir = Root + "/Materials/Farming";
        const string PrefabDir = Root + "/Prefabs/Farming";
        const string IconDir = Root + "/UI/Farming/Icons";
        const string UiDir = Root + "/UI/Farming";
        const string Tag = "[MossvaleFarming] ";

        // Decorative beds converted into playable 3x3 plot beds (the other three stay as decoration).
        static readonly string[] PlayableBeds = { "CropBed_Carrot_01", "CropBed_Tomato_01", "CropBed_Wheat_01" };

        // ------------------------------------------------------------------ menu / batch
        [MenuItem("Mossvale/Farming/Setup Farming (assets + scene)", priority = 40)]
        public static void SetupFarmingMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            SetupFarming();
            EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Mossvale/Farming/Re-apply To Scene Only", priority = 41)]
        public static void ApplyMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            ApplyToScene();
        }

        public static void BatchImportTMP()
        {
            // ImportPackage completes asynchronously: exit from its completion callbacks, not right away.
            if (AssetDatabase.FindAssets("t:TMP_Settings").Length > 0) { Debug.Log(Tag + "TMP Essentials already present"); EditorApplication.Exit(0); return; }
            AssetDatabase.importPackageCompleted += n =>
            {
                AssetDatabase.Refresh();
                Debug.Log(Tag + "TMP Essentials imported: " + (AssetDatabase.FindAssets("t:TMP_Settings").Length > 0));
                EditorApplication.Exit(0);
            };
            AssetDatabase.importPackageFailed += (n, err) => { Debug.LogError(Tag + "TMP IMPORT FAILED: " + err); EditorApplication.Exit(1); };
            AssetDatabase.ImportPackage("Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage", false);
        }

        public static void BatchSetupFarming()
        {
            try { SetupFarming(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogError(Tag + "SETUP FAILED: " + e); EditorApplication.Exit(1); }
        }

        /// <summary>Imports TextMesh Pro Essential Resources non-interactively if they are missing.</summary>
        public static void ImportTmpEssentials()
        {
            if (AssetDatabase.FindAssets("t:TMP_Settings").Length > 0) { Debug.Log(Tag + "TMP Essentials already present"); return; }
            const string pkg = "Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage";
            AssetDatabase.ImportPackage(pkg, false);
            AssetDatabase.Refresh();
            Debug.Log(Tag + "TMP Essentials imported: " + (AssetDatabase.FindAssets("t:TMP_Settings").Length > 0));
        }

        public static void SetupFarming()
        {
            if (TMP_Settings.defaultFontAsset == null) ImportTmpEssentials();
            MossvaleSceneBuilder.SetupMaterialsAndModels();   // imports Models/Farming with the URP palette materials
            var a = CreateAssets();
            RenderIcons(a);
            ApplyToScene();
            Debug.Log(Tag + "Farming setup finished.");
        }

        // ------------------------------------------------------------------ assets
        class Assets
        {
            public FarmSettings settings;
            public CropData carrot, tomato, wheat;
            public ItemData hoe, seedBag, can, basket;
            public ItemData[] seeds, produce;
            public GameObject plotPrefab, bedPrefab;
            public Material fx;
            public readonly Dictionary<ItemData, GameObject> iconSource = new Dictionary<ItemData, GameObject>();
        }

        static Assets CreateAssets()
        {
            foreach (var d in new[] { DataDir + "/Items", DataDir + "/Crops", MatDir, PrefabDir + "/Crops", PrefabDir + "/FX", IconDir })
                EnsureFolder(d);
            var a = new Assets();

            // Materials ------------------------------------------------------
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var wet = LitMat("MV_SoilWet", lit, new Color32(0x45, 0x2A, 0x1C, 255), 0.3f);
            var depleted = LitMat("MV_SoilDepleted", lit, new Color32(0x8E, 0x6C, 0x4C, 255), 0.05f);
            a.fx = ParticleMat("FX_Particles");
            var palette = Root + "/Materials/";
            Material P(string n) => AssetDatabase.LoadAssetAtPath<Material>(palette + n + ".mat");

            // Models / env prefabs ------------------------------------------
            GameObject M(string n) => Load<GameObject>($"{FarmModels}/{n}.fbx");
            GameObject E(string path) => Load<GameObject>($"{EnvPrefabs}/{path}.prefab");

            // FX prefabs -----------------------------------------------------
            var tillFx = MakeFx("FX_Till", a.fx, new Color(0.45f, 0.29f, 0.19f), 16, 1.8f, 0.13f, 0.6f, 1.6f, ParticleSystemShapeType.Cone, 0.3f, false, 0);
            var waterFx = MakeFx("FX_Water", a.fx, new Color(0.45f, 0.75f, 1f), 26, 0.3f, 0.08f, 0.7f, 2.5f, ParticleSystemShapeType.Box, 0.5f, false, 0, emitFromAbove: true);
            var growFx = MakeFx("FX_Grow", a.fx, new Color(0.62f, 1f, 0.5f), 14, 1.0f, 0.1f, 0.9f, -0.2f, ParticleSystemShapeType.Cone, 0.35f, false, 0);
            var harvestFx = MakeFx("FX_Harvest", a.fx, new Color(1f, 0.85f, 0.3f), 28, 2.6f, 0.12f, 0.8f, 1.2f, ParticleSystemShapeType.Cone, 0.3f, false, 0);
            var readyFx = MakeFx("FX_ReadyLoop", a.fx, new Color(1f, 0.92f, 0.45f), 0, 0.35f, 0.07f, 1.3f, -0.1f, ParticleSystemShapeType.Box, 0.55f, true, 5f);

            // Items + crops (created first so stage prefabs can be assigned) -----
            a.settings = GetOrCreate<FarmSettings>($"{DataDir}/FarmSettings.asset");
            a.hoe = Item("Tool_Hoe", "hoe", "Hoe", ItemCategory.Tool, ToolType.Hoe, new Color(0.65f, 0.45f, 0.3f));
            a.seedBag = Item("Tool_SeedBag", "seed_bag", "Seed Bag", ItemCategory.Tool, ToolType.Seeds, new Color(0.85f, 0.75f, 0.5f));
            a.can = Item("Tool_WateringCan", "watering_can", "Watering Can", ItemCategory.Tool, ToolType.WateringCan, new Color(0.4f, 0.6f, 0.9f));
            a.basket = Item("Tool_Basket", "basket", "Basket", ItemCategory.Tool, ToolType.Basket, new Color(0.8f, 0.6f, 0.35f));

            a.carrot = GetOrCreate<CropData>($"{DataDir}/Crops/Crop_Carrot.asset");
            a.tomato = GetOrCreate<CropData>($"{DataDir}/Crops/Crop_Tomato.asset");
            a.wheat = GetOrCreate<CropData>($"{DataDir}/Crops/Crop_Wheat.asset");
            var crops = new[] { (a.carrot, "carrot", "Carrot", new Color(0.95f, 0.55f, 0.16f), 10f, 2, 4, 1, 2),
                                (a.tomato, "tomato", "Tomato", new Color(0.86f, 0.22f, 0.18f), 20f, 3, 5, 1, 2),
                                (a.wheat, "wheat", "Wheat", new Color(0.92f, 0.76f, 0.25f), 15f, 3, 4, 1, 3) };
            var seedList = new List<ItemData>();
            var produceList = new List<ItemData>();
            foreach (var (crop, id, name, tint, secs, ymin, ymax, smin, smax) in crops)
            {
                var seed = Item($"Seed_{name}", id + "_seeds", $"{name} Seeds", ItemCategory.Seed, ToolType.None, tint * 0.8f);
                var prod = Item($"Produce_{name}", id, name, ItemCategory.Produce, ToolType.None, tint);
                seed.plantsCrop = crop;
                crop.cropId = id; crop.displayName = name; crop.seedItem = seed; crop.produceItem = prod;
                crop.secondsPerStage = secs; crop.minYield = ymin; crop.maxYield = ymax; crop.minSeedsBack = smin; crop.maxSeedsBack = smax;
                seedList.Add(seed); produceList.Add(prod);
                EditorUtility.SetDirty(seed); EditorUtility.SetDirty(prod);
            }
            a.seeds = seedList.ToArray();
            a.produce = produceList.ToArray();

            // Crop stage prefabs ---------------------------------------------
            var mound = M("Seed_Mound");
            var three = new[] { new Vector3(-0.36f, 0, 0.02f), new Vector3(0, 0, -0.04f), new Vector3(0.36f, 0, 0.03f) };
            var one = new[] { Vector3.zero };
            a.carrot.stagePrefabs = new[]
            {
                StagePrefab("Carrot_0_Seed", mound, three, 0.9f, null, null),
                StagePrefab("Carrot_1_Small", M("Crop_Carrot"), three, 0.5f, new[] { "Carrot_Root" }, null),
                StagePrefab("Carrot_2_Medium", M("Crop_Carrot"), three, 0.8f, null, null),
                StagePrefab("Carrot_3_Full", M("Crop_Carrot"), three, 1.15f, null, null),
            };
            var unripe = new Dictionary<string, Material> { { "Tomato_Fruit", P("MV_LeafLight") } };
            a.tomato.stagePrefabs = new[]
            {
                StagePrefab("Tomato_0_Seed", mound, one, 1.3f, null, null),
                StagePrefab("Tomato_1_Small", M("Crop_Tomato"), one, 0.38f, new[] { "Tomato_Fruit" }, null),
                StagePrefab("Tomato_2_Medium", M("Crop_Tomato"), one, 0.7f, null, unripe),
                StagePrefab("Tomato_3_Full", M("Crop_Tomato"), one, 0.95f, null, null),
            };
            var greenStalks = new Dictionary<string, Material> { { "Wheat_Stalks", P("MV_PlantStem") } };
            var greenWheat = new Dictionary<string, Material> { { "Wheat_Stalks", P("MV_PlantStem") }, { "Wheat_Heads", P("MV_LeafLight") } };
            a.wheat.stagePrefabs = new[]
            {
                StagePrefab("Wheat_0_Seed", mound, three, 0.9f, null, null),
                StagePrefab("Wheat_1_Small", M("Crop_Wheat"), three, 0.35f, new[] { "Wheat_Heads" }, greenStalks),
                StagePrefab("Wheat_2_Medium", M("Crop_Wheat"), three, 0.7f, null, greenWheat),
                StagePrefab("Wheat_3_Full", M("Crop_Wheat"), three, 1.0f, null, null),
            };
            foreach (var c in new[] { a.carrot, a.tomato, a.wheat }) EditorUtility.SetDirty(c);

            // Tools: held models ------------------------------------------------
            SetHeld(a.hoe, M("Tool_Hoe"), new Vector3(0f, -0.55f, 0.05f), new Vector3(20f, 0f, -12f), 1f);
            SetHeld(a.seedBag, E("Props/Sack_Grain"), new Vector3(0f, -0.3f, 0f), Vector3.zero, 0.38f);
            SetHeld(a.can, E("Props/Watering_Can"), new Vector3(0f, -0.32f, 0.08f), new Vector3(0f, -90f, 0f), 0.7f);
            SetHeld(a.basket, E("Props/Basket_Veg"), new Vector3(0f, -0.4f, 0.05f), Vector3.zero, 0.55f);

            // Icon sources
            a.iconSource[a.hoe] = M("Tool_Hoe");
            a.iconSource[a.seedBag] = E("Props/Sack_Grain");
            a.iconSource[a.can] = E("Props/Watering_Can");
            a.iconSource[a.basket] = E("Props/Basket_Veg");
            a.iconSource[a.seeds[0]] = a.carrot.stagePrefabs[1];
            a.iconSource[a.seeds[1]] = a.tomato.stagePrefabs[1];
            a.iconSource[a.seeds[2]] = a.wheat.stagePrefabs[1];
            a.iconSource[a.produce[0]] = E("Props/Crate_Carrot");
            a.iconSource[a.produce[1]] = E("Props/Crate_Tomato");
            a.iconSource[a.produce[2]] = a.wheat.stagePrefabs[3];

            // Plot + bed prefabs -------------------------------------------------
            a.plotPrefab = BuildPlotPrefab(M, E, P, wet, depleted, tillFx, waterFx, growFx, harvestFx, readyFx);
            a.bedPrefab = BuildBedPrefab(M("CropBed_Frame"), a.plotPrefab);

            AssetDatabase.SaveAssets();
            Debug.Log(Tag + "Assets created.");
            return a;
        }

        static GameObject StagePrefab(string name, GameObject model, Vector3[] offsets, float scale, string[] hide, Dictionary<string, Material> recolor)
        {
            var root = new GameObject(name);
            for (int i = 0; i < offsets.Length; i++)
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
                inst.transform.SetParent(root.transform, false);
                inst.transform.localPosition = offsets[i];
                inst.transform.localRotation = Quaternion.Euler(0f, (i * 137f + 20f) % 360f, 0f);
                inst.transform.localScale = Vector3.one * scale;
                foreach (var t in inst.GetComponentsInChildren<Transform>(true))
                {
                    if (hide != null && hide.Contains(t.name)) t.gameObject.SetActive(false);
                    if (recolor != null && recolor.TryGetValue(t.name, out var m) && t.TryGetComponent<Renderer>(out var r))
                        r.sharedMaterials = Enumerable.Repeat(m, r.sharedMaterials.Length).ToArray();
                }
            }
            var path = $"{PrefabDir}/Crops/{name}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        static GameObject BuildPlotPrefab(Func<string, GameObject> M, Func<string, GameObject> E, Func<string, Material> P,
            Material wet, Material depleted, GameObject tillFx, GameObject waterFx, GameObject growFx, GameObject harvestFx, GameObject readyFx)
        {
            var root = new GameObject("FarmPlot");
            var box = root.AddComponent<BoxCollider>();
            box.isTrigger = true;               // clickable + hoverable, but walls come from the bed
            box.center = new Vector3(0f, 0.3f, 0f);
            box.size = new Vector3(1.15f, 0.6f, 0.8f);
            root.AddComponent<FarmPlot>();
            var vis = root.AddComponent<FarmPlotVisuals>();

            GameObject Child(string n, GameObject prefab, Vector3 pos, Vector3 scale, float yaw = 0f, Transform parent = null)
            {
                var g = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                g.name = n;
                g.transform.SetParent(parent != null ? parent : root.transform, false);
                g.transform.localPosition = pos;
                g.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                g.transform.localScale = scale;
                foreach (var c in g.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                return g;
            }
            GameObject Empty(string n, Vector3 pos)
            {
                var g = new GameObject(n);
                g.transform.SetParent(root.transform, false);
                g.transform.localPosition = pos;
                return g;
            }

            var weeds = Empty("Weeds", Vector3.zero);
            Child("Weed_A", E("Nature/Grass_Tuft_A"), new Vector3(-0.32f, 0f, 0.12f), Vector3.one * 0.55f, 30f, weeds.transform);
            Child("Weed_B", E("Nature/Grass_Tuft_C"), new Vector3(0.28f, 0f, -0.14f), Vector3.one * 0.5f, 140f, weeds.transform);
            Child("Weed_C", E("Nature/Grass_Tuft_B"), new Vector3(0.05f, 0f, 0.2f), Vector3.one * 0.4f, 260f, weeds.transform);
            Child("Pebbles", E("Nature/Pebbles"), new Vector3(-0.05f, 0f, -0.18f), Vector3.one * 0.45f, 75f, weeds.transform);

            var ridge = Child("Ridge", M("Plot_Ridge"), Vector3.zero, Vector3.one);
            var stubble = Empty("Stubble", new Vector3(0f, 0.12f, 0f));
            for (int i = 0; i < 3; i++)
            {
                var s = Child("Stubble_" + i, M("Crop_Wheat"), new Vector3(-0.36f + i * 0.36f, 0f, (i - 1) * 0.04f), new Vector3(0.75f, 0.14f, 0.75f), i * 97f, stubble.transform);
                foreach (var t in s.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "Wheat_Heads") t.gameObject.SetActive(false);
                    if (t.name == "Wheat_Stalks" && t.TryGetComponent<Renderer>(out var r))
                        r.sharedMaterials = Enumerable.Repeat(P("MV_HayDark"), r.sharedMaterials.Length).ToArray();
                }
            }
            var anchor = Empty("CropAnchor", new Vector3(0f, 0.13f, 0f));
            var drop = Child("NeedsWater", M("Water_Droplet"), new Vector3(0f, 1.05f, 0f), Vector3.one * 1.3f);
            foreach (var r in drop.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;

            ParticleSystem Fx(GameObject prefab, Vector3 pos) => Child(prefab.name, prefab, pos, Vector3.one).GetComponent<ParticleSystem>();
            var fxTill = Fx(tillFx, new Vector3(0f, 0.15f, 0f));
            var fxWater = Fx(waterFx, new Vector3(0f, 0.15f, 0f));
            var fxGrow = Fx(growFx, new Vector3(0f, 0.2f, 0f));
            var fxHarvest = Fx(harvestFx, new Vector3(0f, 0.3f, 0f));
            var fxReady = Fx(readyFx, new Vector3(0f, 0.25f, 0f));

            stubble.SetActive(false);
            ridge.SetActive(false);
            drop.SetActive(false);

            Set(vis, "weeds", weeds);
            Set(vis, "ridge", ridge.GetComponentInChildren<Renderer>());
            Set(vis, "stubble", stubble);
            Set(vis, "dryMaterial", P("MV_SoilLight"));
            Set(vis, "wetMaterial", wet);
            Set(vis, "depletedMaterial", depleted);
            Set(vis, "cropAnchor", anchor.transform);
            Set(vis, "needsWaterIndicator", drop.transform);
            Set(vis, "readyLoopFx", fxReady);
            Set(vis, "tillFx", fxTill);
            Set(vis, "waterFx", fxWater);
            Set(vis, "growFx", fxGrow);
            Set(vis, "harvestFx", fxHarvest);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabDir + "/FarmPlot.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        static GameObject BuildBedPrefab(GameObject frameModel, GameObject plotPrefab)
        {
            var bed = (GameObject)PrefabUtility.InstantiatePrefab(frameModel);
            bed.name = "FarmBed";
            var mf = bed.GetComponentInChildren<MeshFilter>();
            var b = mf.sharedMesh.bounds;
            mf.gameObject.AddComponent<BoxCollider>(); // solid frame: the player walks around the bed
            bool longX = b.size.x >= b.size.z;
            float soilY = b.min.y + 0.28f;
            var plots = new GameObject("Plots").transform;
            plots.SetParent(mf.transform, false);
            float[] cols = { -1.25f, 0f, 1.25f };
            float[] rows = { -0.85f, 0f, 0.85f };
            for (int r = 0; r < 3; r++)
                for (int c = 0; c < 3; c++)
                {
                    var p = (GameObject)PrefabUtility.InstantiatePrefab(plotPrefab);
                    p.name = $"Plot_{r}_{c}";
                    p.transform.SetParent(plots, false);
                    p.transform.localPosition = longX ? new Vector3(b.center.x + cols[c], soilY, b.center.z + rows[r])
                                                      : new Vector3(b.center.x + rows[r], soilY, b.center.z + cols[c]);
                    p.transform.localRotation = longX ? Quaternion.identity : Quaternion.Euler(0f, 90f, 0f);
                }
            var prefab = PrefabUtility.SaveAsPrefabAsset(bed, PrefabDir + "/FarmBed.prefab");
            Object.DestroyImmediate(bed);
            return prefab;
        }

        // ------------------------------------------------------------------ icons
        static void RenderIcons(Assets a)
        {
            // NewScene unloads unused assets, so keep paths and reload the items/prefabs afterwards.
            var entries = a.iconSource.Where(kv => kv.Key != null && kv.Value != null)
                .Select(kv => (item: AssetDatabase.GetAssetPath(kv.Key), source: AssetDatabase.GetAssetPath(kv.Value))).ToList();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var lightGo = new GameObject("IconLight");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.3f;
            light.color = new Color(1f, 0.95f, 0.85f);
            lightGo.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.62f, 0.62f, 0.66f);
            RenderSettings.fog = false;

            var camGo = new GameObject("IconCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            var key = new Color(1f, 0f, 1f, 1f);
            cam.backgroundColor = key;
            cam.fieldOfView = 22f;
            cam.allowMSAA = false;
            cam.allowHDR = false;
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false;
            data.antialiasing = AntialiasingMode.None;

            const int size = 256;
            var rt = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32);
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            foreach (var (itemPath, sourcePath) in entries)
            {
                var item = AssetDatabase.LoadAssetAtPath<ItemData>(itemPath);
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
                if (item == null || source == null) continue;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(source, scene);
                go.transform.rotation = Quaternion.Euler(0f, 25f, 0f);
                var rs = go.GetComponentsInChildren<Renderer>();
                var bnd = rs[0].bounds;
                foreach (var r in rs) bnd.Encapsulate(r.bounds);
                var dir = new Vector3(0.55f, 0.55f, -1f).normalized;
                float dist = bnd.extents.magnitude / Mathf.Sin(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.02f;
                cam.transform.position = bnd.center + dir * dist;
                cam.transform.LookAt(bnd.center);
                cam.nearClipPlane = 0.01f;
                cam.farClipPlane = dist * 3f;

                var req = new RenderPipeline.StandardRequest { destination = rt };
                if (RenderPipeline.SupportsRenderRequest(cam, req)) RenderPipeline.SubmitRenderRequest(cam, req);
                else { cam.targetTexture = rt; cam.Render(); cam.targetTexture = null; }
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
                RenderTexture.active = null;
                var px = tex.GetPixels32();
                for (int i = 0; i < px.Length; i++)   // chroma-key the magenta background to transparent
                    if (px[i].r > 200 && px[i].g < 60 && px[i].b > 200) px[i] = new Color32(0, 0, 0, 0);
                tex.SetPixels32(px);
                tex.Apply();
                string path = $"{IconDir}/Icon_{item.itemId}.png";
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(go);

                AssetDatabase.ImportAsset(path);
                var ti = (TextureImporter)AssetImporter.GetAtPath(path);
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single; // Multiple (no slices) would yield no Sprite
                ti.alphaIsTransparency = true;
                ti.mipmapEnabled = false;
                ti.SaveAndReimport();
                item = AssetDatabase.LoadAssetAtPath<ItemData>(itemPath); // reimport may unload it too
                item.icon = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                EditorUtility.SetDirty(item);
                AssetDatabase.SaveAssetIfDirty(item);
            }
            rt.Release();
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
            AssetDatabase.SaveAssets();
            Debug.Log(Tag + $"Icons rendered: {entries.Count}");
        }

        // ------------------------------------------------------------------ scene
        public static void ApplyToScene()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var a = LoadAssets();
            if (a.settings == null || a.bedPrefab == null) throw new Exception("Farming assets missing - run 'Setup Farming (assets + scene)' first.");

            var all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToList();
            Transform Find(string n) => all.FirstOrDefault(t => t != null && t.name == n);

            // --- clean previous run (only objects/components this setup creates)
            foreach (var n in new[] { "Farming", "Farming_HUD", "EventSystem", "Farm_Beds" })
            {
                var t = Find(n);
                if (t != null) Object.DestroyImmediate(t.gameObject);
            }
            all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToList();
            var player = Find("Player")?.gameObject ?? throw new Exception("Player not found in Mossvale_Main");
            foreach (var c in new Type[] { typeof(PlayerKeyboardMovement), typeof(PlayerInteractor), typeof(PlayerToolController), typeof(Inventory) })
                foreach (var comp in player.GetComponents(c)) Object.DestroyImmediate(comp);
            var oldSocket = player.transform.Find("ToolSocket");
            if (oldSocket) Object.DestroyImmediate(oldSocket.gameObject);
            foreach (var ws in Object.FindObjectsByType<WaterSource>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.DestroyImmediate(ws);

            // --- playable beds (decorative originals are deactivated, not deleted)
            Transform bedsParent = null;
            foreach (var bedName in PlayableBeds)
            {
                var original = Find(bedName);
                if (original == null) { Debug.LogWarning(Tag + "Bed not found: " + bedName); continue; }
                original.gameObject.SetActive(true);
                if (bedsParent == null)
                {
                    bedsParent = new GameObject("Farm_Beds").transform;
                    bedsParent.SetParent(original.parent, false);
                }
                var bed = (GameObject)PrefabUtility.InstantiatePrefab(a.bedPrefab, scene);
                bed.name = "FarmBed_" + bedName.Replace("CropBed_", "").Replace("_01", "");
                bed.transform.SetParent(bedsParent, true);
                bed.transform.SetPositionAndRotation(original.position, original.rotation);
                bed.transform.localScale = original.lossyScale;
                original.gameObject.SetActive(false);
            }

            // --- farming systems
            var farming = new GameObject("Farming");
            var manager = farming.AddComponent<FarmManager>();
            Set(manager, "settings", a.settings);

            // --- player
            var inv = player.AddComponent<Inventory>();
            var start = new List<(ItemData, int)> { (a.hoe, 1), (a.seedBag, 1), (a.can, 1), (a.basket, 1),
                (a.seeds[0], 6), (a.seeds[1], 4), (a.seeds[2], 6), (a.produce[0], 0), (a.produce[1], 0), (a.produce[2], 0) };
            var so = new SerializedObject(inv);
            var list = so.FindProperty("startingItems");
            list.arraySize = start.Count;
            for (int i = 0; i < start.Count; i++)
            {
                list.GetArrayElementAtIndex(i).FindPropertyRelative("item").objectReferenceValue = start[i].Item1;
                list.GetArrayElementAtIndex(i).FindPropertyRelative("count").intValue = start[i].Item2;
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            var socket = new GameObject("ToolSocket").transform;
            socket.SetParent(player.transform, false);
            socket.localPosition = new Vector3(0.42f, 0.85f, 0.15f);
            var tools = player.AddComponent<PlayerToolController>();
            Set(tools, "inventory", inv);
            Set(tools, "toolSocket", socket);
            var interactor = player.AddComponent<PlayerInteractor>();
            Set(interactor, "tools", tools);
            Set(interactor, "viewCamera", Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>());
            player.AddComponent<PlayerKeyboardMovement>();

            // --- water sources
            AddWaterSource(Find("Well_01"), "Well");
            AddWaterSource(Find("Pond_Water"), "Pond");

            // --- selection outline
            var hl = new GameObject("Interaction_Highlight");
            hl.transform.SetParent(farming.transform, false);
            var line = hl.AddComponent<LineRenderer>();
            line.sharedMaterial = a.fx;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.numCornerVertices = 2;
            line.alignment = LineAlignment.View;
            var highlighter = hl.AddComponent<InteractionHighlighter>();
            Set(highlighter, "interactor", interactor);

            // --- HUD
            var hud = BuildHud(tools, interactor, inv);
            var dev = farming.AddComponent<FarmDevTools>();
            Set(dev, "panel", hud.devPanel);
            Set(dev, "panelText", hud.devText);
            Set(dev, "inventory", inv);
            Set(dev, "tools", tools);
            Set(dev, "crops", new Object[] { a.carrot, a.tomato, a.wheat });

            // Progression (buildings, stars, save system) sits on top of farming: re-apply it if it has been set up.
            if (MossvaleProgressionSetup.AssetsExist) MossvaleProgressionSetup.ApplyToOpenScene();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log(Tag + $"Scene updated: {ScenePath} (beds: {PlayableBeds.Length}, plots: {Object.FindObjectsByType<FarmPlot>(FindObjectsSortMode.None).Length})");
        }

        static void AddWaterSource(Transform t, string name)
        {
            if (t == null) { Debug.LogWarning(Tag + "Water source not found: " + name); return; }
            var ws = t.gameObject.AddComponent<WaterSource>();
            Set(ws, "displayName", name);
        }

        static Assets LoadAssets()
        {
            var a = new Assets
            {
                settings = Load<FarmSettings>($"{DataDir}/FarmSettings.asset"),
                carrot = Load<CropData>($"{DataDir}/Crops/Crop_Carrot.asset"),
                tomato = Load<CropData>($"{DataDir}/Crops/Crop_Tomato.asset"),
                wheat = Load<CropData>($"{DataDir}/Crops/Crop_Wheat.asset"),
                hoe = Load<ItemData>($"{DataDir}/Items/Tool_Hoe.asset"),
                seedBag = Load<ItemData>($"{DataDir}/Items/Tool_SeedBag.asset"),
                can = Load<ItemData>($"{DataDir}/Items/Tool_WateringCan.asset"),
                basket = Load<ItemData>($"{DataDir}/Items/Tool_Basket.asset"),
                bedPrefab = Load<GameObject>($"{PrefabDir}/FarmBed.prefab"),
                fx = Load<Material>($"{MatDir}/FX_Particles.mat"),
            };
            a.seeds = new[] { "Carrot", "Tomato", "Wheat" }.Select(n => Load<ItemData>($"{DataDir}/Items/Seed_{n}.asset")).ToArray();
            a.produce = new[] { "Carrot", "Tomato", "Wheat" }.Select(n => Load<ItemData>($"{DataDir}/Items/Produce_{n}.asset")).ToArray();
            return a;
        }

        // ------------------------------------------------------------------ HUD
        class Hud { public GameObject devPanel; public TMP_Text devText; }

        static readonly Color Panel = new Color(0.20f, 0.14f, 0.10f, 0.86f);
        static readonly Color Cream = new Color(0.97f, 0.95f, 0.89f);
        static readonly Color Gold = new Color(0.97f, 0.78f, 0.18f);
        static Sprite uiSprite, knob;
        static TMP_FontAsset font;
        static Material outlineFontMat;

        static Hud BuildHud(PlayerToolController tools, PlayerInteractor interactor, Inventory inv)
        {
            uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            font = TMP_Settings.defaultFontAsset;
            outlineFontMat = OutlineFontMaterial(font);

            // EventSystem (UI clicks); the Input System UI module matches the project's active input handling.
            var es = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));

            var canvasGo = new GameObject("Farming_HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            var root = canvasGo.transform;
            var hudComp = canvasGo.AddComponent<FarmHUD>();

            // Floating feedback layer (full screen, behind panels)
            var floatLayer = Rect("FloatingFeedback", root);
            Stretch(floatLayer);
            var floatTemplate = Text(floatLayer, "FloatingTextTemplate", "+3 Carrot", 30, TextAlignmentOptions.Center, Cream, FontStyles.Bold);
            Place(floatTemplate.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(600, 50));
            floatTemplate.fontSharedMaterial = outlineFontMat;
            var floating = floatLayer.gameObject.AddComponent<FloatingFeedbackUI>();
            Set(floating, "template", floatTemplate);

            // Toolbar -------------------------------------------------------------
            var bar = Rect("Toolbar", root);
            Place(bar, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 24), new Vector2(4 * 132 + 3 * 14, 140));
            var hlg = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 14; hlg.childAlignment = TextAnchor.LowerCenter;
            hlg.childControlWidth = hlg.childControlHeight = false;
            hlg.childForceExpandWidth = hlg.childForceExpandHeight = false;
            var slots = new List<ToolbarSlotUI>();
            var names = new[] { (ToolType.Hoe, "1"), (ToolType.Seeds, "2"), (ToolType.WateringCan, "3"), (ToolType.Basket, "4") };
            foreach (var (tool, keyText) in names) slots.Add(Slot(bar, tool, keyText));

            // Prompt ----------------------------------------------------------------
            var prompt = Rect("InteractionPrompt", root);
            Place(prompt, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 182), new Vector2(860, 84));
            Image(prompt, Panel);
            var promptAction = Text(prompt, "Action", "[E] / Click  Till soil", 28, TextAlignmentOptions.Center, Cream, FontStyles.Bold);
            Place(promptAction.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -8), new Vector2(840, 38));
            var promptDetail = Text(prompt, "Detail", "Untilled soil", 21, TextAlignmentOptions.Center, new Color(0.86f, 0.82f, 0.72f));
            Place(promptDetail.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 8), new Vector2(840, 30));
            prompt.gameObject.SetActive(false);

            // Inventory -------------------------------------------------------------
            const float rowH = 38f, headerH = 28f, titleH = 40f;
            int toolsRows = 4, seedRows = 3, prodRows = 3;
            float h = 16 * 2 + titleH + 3 * headerH + (toolsRows + seedRows + prodRows) * (rowH + 2) + 8 * 6;
            var invPanel = Rect("InventoryPanel", root);
            Place(invPanel, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24, -24), new Vector2(330, h));
            Image(invPanel, Panel);
            var vlg = invPanel.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(16, 16, 16, 16);
            vlg.spacing = 6;
            vlg.childControlWidth = vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            Layout(Text(invPanel, "Title", "Inventory", 28, TextAlignmentOptions.Left, Gold, FontStyles.Bold).gameObject, titleH);
            Transform Section(string title, int rows)
            {
                Layout(Text(invPanel, title + "Header", title, 20, TextAlignmentOptions.Left, new Color(0.86f, 0.80f, 0.66f), FontStyles.Bold | FontStyles.UpperCase).gameObject, headerH);
                var l = Rect(title + "List", invPanel);
                var lv = l.gameObject.AddComponent<VerticalLayoutGroup>();
                lv.spacing = 2; lv.childControlWidth = lv.childControlHeight = true; lv.childForceExpandWidth = true; lv.childForceExpandHeight = false;
                Layout(l.gameObject, rows * (rowH + 2));
                return l;
            }
            var toolsList = Section("Tools", toolsRows);
            var seedsList = Section("Seeds", seedRows);
            var produceList = Section("Harvest", prodRows);
            var rowTemplate = Row(invPanel, rowH);

            // Toast ---------------------------------------------------------------
            var toast = Text(root, "Toast", "Watering can refilled", 30, TextAlignmentOptions.Center, Cream, FontStyles.Bold);
            Place(toast.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -36), new Vector2(1000, 48));
            toast.fontSharedMaterial = outlineFontMat;

            // Help --------------------------------------------------------------
            var help = Rect("HelpPanel", root);
            Place(help, Vector2.zero, Vector2.zero, new Vector2(24, 24), new Vector2(560, 236));
            Image(help, Panel);
            var helpText = Text(help, "HelpText",
                "<b><color=#F8C72D>Mossvale farming</color></b>  Till > Plant > Water > Grow > Harvest\n" +
                "<b>Left-click</b> walk / use tool on a plot\n" +
                "<b>WASD / Arrows</b> walk      <b>E / Space</b> use\n" +
                "<b>1</b> Hoe  <b>2</b> Seeds  <b>3</b> Watering Can  <b>4</b> Basket\n" +
                "<b>Wheel</b> change tool     <b>Tab</b> change seed\n" +
                "Refill the can at the <b>well</b> or <b>pond</b>\n" +
                "<b>H</b> hide help     <b>F1</b> dev tools", 19, TextAlignmentOptions.TopLeft, Cream);
            Stretch(helpText.rectTransform, 16);

            // Dev panel -----------------------------------------------------------
            var devPanel = Rect("DevPanel", root);
            Place(devPanel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24, -170), new Vector2(640, 230));
            Image(devPanel, new Color(0.08f, 0.10f, 0.14f, 0.88f));
            var devText = Text(devPanel, "DevText", "DEV TOOLS", 19, TextAlignmentOptions.TopLeft, new Color(0.8f, 0.95f, 1f));
            Stretch(devText.rectTransform, 14);
            devPanel.gameObject.SetActive(false);

            Set(hudComp, "tools", tools);
            Set(hudComp, "interactor", interactor);
            Set(hudComp, "inventory", inv);
            Set(hudComp, "slots", slots.Cast<Object>().ToArray());
            Set(hudComp, "rowTemplate", rowTemplate);
            Set(hudComp, "toolsList", toolsList);
            Set(hudComp, "seedsList", seedsList);
            Set(hudComp, "produceList", produceList);
            Set(hudComp, "promptRoot", prompt.gameObject);
            Set(hudComp, "promptAction", promptAction);
            Set(hudComp, "promptDetail", promptDetail);
            Set(hudComp, "toast", toast);
            Set(hudComp, "helpPanel", help.gameObject);
            return new Hud { devPanel = devPanel.gameObject, devText = devText };
        }

        static ToolbarSlotUI Slot(Transform parent, ToolType tool, string key)
        {
            var root = Rect("Slot_" + tool, parent);
            root.sizeDelta = new Vector2(132, 132);
            var hit = root.gameObject.AddComponent<UnityEngine.UI.Image>();
            hit.color = new Color(0, 0, 0, 0);
            hit.raycastTarget = true;
            var btn = root.gameObject.AddComponent<Button>();
            btn.targetGraphic = hit;
            var slot = root.gameObject.AddComponent<ToolbarSlotUI>();
            slot.tool = tool;
            slot.button = btn;

            var frame = Rect("SelectedFrame", root); Stretch(frame); slot.selectedFrame = Image(frame, Gold);
            var bg = Rect("Background", root); Stretch(bg, 5); slot.background = Image(bg, Panel);
            var icon = Rect("Icon", root);
            Place(icon, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -12), new Vector2(84, 84));
            slot.icon = icon.gameObject.AddComponent<UnityEngine.UI.Image>();
            slot.icon.preserveAspect = true; slot.icon.raycastTarget = false;
            slot.keyLabel = Text(root, "Key", key, 22, TextAlignmentOptions.TopLeft, Gold, FontStyles.Bold);
            Place(slot.keyLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12, -8), new Vector2(30, 30));
            slot.nameLabel = Text(root, "Name", tool.ToString(), 17, TextAlignmentOptions.Center, Cream);
            Place(slot.nameLabel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 10), new Vector2(124, 24));

            var badgeBg = Rect("Badge", root);
            Place(badgeBg, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-6, -6), new Vector2(40, 40));
            Image(badgeBg, Gold, knob);
            slot.badge = Text(badgeBg, "Count", "6", 20, TextAlignmentOptions.Center, new Color(0.2f, 0.13f, 0.08f), FontStyles.Bold);
            Stretch(slot.badge.rectTransform);
            badgeBg.gameObject.SetActive(tool == ToolType.Seeds);

            var barBg = Rect("WaterBar", root);
            Place(barBg, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 36), new Vector2(100, 10));
            Image(barBg, new Color(0, 0, 0, 0.55f));
            var fill = Rect("Fill", barBg); Stretch(fill, 2);
            slot.barFill = Image(fill, new Color(0.42f, 0.72f, 1f));
            slot.barFill.type = UnityEngine.UI.Image.Type.Filled;
            slot.barFill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            slot.barFill.fillAmount = 1f;
            barBg.gameObject.SetActive(tool == ToolType.WateringCan);
            return slot;
        }

        static InventoryRowUI Row(Transform parent, float height)
        {
            var row = Rect("RowTemplate", parent);
            Image(row, new Color(1f, 1f, 1f, 0.06f));
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(6, 10, 3, 3); h.spacing = 10; h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = h.childControlHeight = true; h.childForceExpandWidth = h.childForceExpandHeight = false;
            Layout(row.gameObject, height);
            var ui = row.gameObject.AddComponent<InventoryRowUI>();
            var icon = Rect("Icon", row);
            ui.icon = icon.gameObject.AddComponent<UnityEngine.UI.Image>();
            ui.icon.preserveAspect = true; ui.icon.raycastTarget = false;
            var le = icon.gameObject.AddComponent<LayoutElement>(); le.preferredWidth = le.preferredHeight = 32; le.minWidth = 32;
            ui.nameLabel = Text(row, "Name", "Item", 20, TextAlignmentOptions.Left, Cream);
            ui.nameLabel.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            ui.countLabel = Text(row, "Count", "x0", 20, TextAlignmentOptions.Right, Gold, FontStyles.Bold);
            var cle = ui.countLabel.gameObject.AddComponent<LayoutElement>(); cle.preferredWidth = 60; cle.minWidth = 60;
            row.gameObject.SetActive(false);
            return ui;
        }

        // ------------------------------------------------------------------ small helpers
        static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        static void Stretch(RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(inset, inset); rt.offsetMax = new Vector2(-inset, -inset);
        }

        static UnityEngine.UI.Image Image(RectTransform rt, Color c, Sprite sprite = null)
        {
            var img = rt.gameObject.AddComponent<UnityEngine.UI.Image>();
            img.sprite = sprite != null ? sprite : uiSprite;
            img.type = sprite != null ? UnityEngine.UI.Image.Type.Simple : UnityEngine.UI.Image.Type.Sliced;
            img.color = c;
            img.raycastTarget = false;
            return img;
        }

        static TMP_Text Text(Transform parent, string name, string text, float size, TextAlignmentOptions align, Color c, FontStyles style = FontStyles.Normal)
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = font;
            t.text = text;
            t.fontSize = size;
            t.alignment = align;
            t.color = c;
            t.fontStyle = style;
            t.raycastTarget = false;
            t.richText = true;
            return t;
        }

        static void Layout(GameObject go, float height)
        {
            var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            le.minHeight = height;
        }

        static Material OutlineFontMaterial(TMP_FontAsset f)
        {
            EnsureFolder(UiDir);
            string path = UiDir + "/Font_Outline.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(f.material) { name = "Font_Outline" };
                AssetDatabase.CreateAsset(m, path);
            }
            m.EnableKeyword("OUTLINE_ON");
            m.SetFloat("_OutlineWidth", 0.22f);
            m.SetColor("_OutlineColor", new Color(0.16f, 0.10f, 0.06f, 1f));
            m.SetFloat("_FaceDilate", 0.15f);
            EditorUtility.SetDirty(m);
            return m;
        }

        internal static Material LitMat(string name, Shader lit, Color c, float smooth)
        {
            string p = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (m == null) { m = new Material(lit) { name = name }; AssetDatabase.CreateAsset(m, p); }
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(m);
            return m;
        }

        internal static Material ParticleMat(string name)
        {
            string p = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(p);
            var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (m == null) { m = new Material(sh) { name = name }; AssetDatabase.CreateAsset(m, p); }
            m.shader = sh;
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            m.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(m);
            return m;
        }

        internal static GameObject MakeFx(string name, Material mat, Color color, int burst, float speed, float size, float life, float gravity,
            ParticleSystemShapeType shape, float radius, bool loop, float rate, bool emitFromAbove = false)
        {
            var go = new GameObject(name);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = loop;
            main.playOnAwake = false;
            main.duration = loop ? 1f : 0.4f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.65f, life);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.5f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size);
            main.startColor = new ParticleSystem.MinMaxGradient(color, Color.Lerp(color, Color.white, 0.35f));
            main.gravityModifier = gravity;
            main.maxParticles = 80;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission;
            em.rateOverTime = loop ? rate : 0f;
            if (!loop) em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)burst) });
            var sh = ps.shape;
            sh.shapeType = shape;
            sh.radius = radius;
            sh.angle = 28f;
            if (shape == ParticleSystemShapeType.Box) sh.scale = new Vector3(radius * 2f, radius * 1.4f, 0.05f);
            // Cones/boxes emit along local +Z: point them up (or down for water).
            go.transform.localRotation = Quaternion.Euler(emitFromAbove ? 90f : -90f, 0f, 0f);
            if (emitFromAbove) sh.position = new Vector3(0f, 0f, -0.9f);
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var sz = ps.sizeOverLifetime;
            sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.35f));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabDir}/FX/{name}.prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }

        static ItemData Item(string file, string id, string name, ItemCategory cat, ToolType tool, Color tint)
        {
            var it = GetOrCreate<ItemData>($"{DataDir}/Items/{file}.asset");
            it.itemId = id; it.displayName = name; it.category = cat; it.toolType = tool; it.tint = tint;
            EditorUtility.SetDirty(it);
            return it;
        }

        static void SetHeld(ItemData item, GameObject prefab, Vector3 offset, Vector3 euler, float scale)
        {
            item.heldPrefab = prefab; item.heldOffset = offset; item.heldEuler = euler; item.heldScale = scale;
            EditorUtility.SetDirty(item);
        }

        static T GetOrCreate<T>(string path) where T : ScriptableObject
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a != null) return a;
            a = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(a, path);
            return a;
        }

        static T Load<T>(string path) where T : Object
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a == null) Debug.LogWarning(Tag + "Missing asset: " + path);
            return a;
        }

        internal static void Set(Object o, string field, object value)
        {
            var so = new SerializedObject(o);
            var p = so.FindProperty(field);
            if (p == null) throw new Exception($"{o.GetType().Name}.{field} not found");
            switch (value)
            {
                case Object[] arr:
                    p.arraySize = arr.Length;
                    for (int i = 0; i < arr.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = arr[i];
                    break;
                case Object ov: p.objectReferenceValue = ov; break;
                case string s: p.stringValue = s; break;
                case bool b: p.boolValue = b; break;
                case float f: p.floatValue = f; break;
                case int i: p.intValue = i; break;
                case null: p.objectReferenceValue = null; break;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int i = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, i));
            AssetDatabase.CreateFolder(path.Substring(0, i), path.Substring(i + 1));
        }

        // ------------------------------------------------------------------ dev menu (Play Mode)
        [MenuItem("Mossvale/Dev/Grow All Crops One Stage %#g")] static void DevGrow() => FarmDevTools.Instance?.GrowAllOneStage();
        [MenuItem("Mossvale/Dev/Water All Plots")] static void DevWater() => FarmDevTools.Instance?.WaterAll();
        [MenuItem("Mossvale/Dev/Give 5 Of Every Seed")] static void DevSeeds() => FarmDevTools.Instance?.GiveSeeds(5);
        [MenuItem("Mossvale/Dev/Growth Speed x1")] static void DevSpeed1() => FarmDevTools.Instance?.SetSpeed(1f);
        [MenuItem("Mossvale/Dev/Growth Speed x10")] static void DevSpeed10() => FarmDevTools.Instance?.SetSpeed(10f);
        [MenuItem("Mossvale/Dev/Growth Speed x50")] static void DevSpeed50() => FarmDevTools.Instance?.SetSpeed(50f);
        [MenuItem("Mossvale/Dev/Showcase Every Crop Stage")] static void DevShowcase() => FarmDevTools.Instance?.Showcase();
        [MenuItem("Mossvale/Dev/Reset All Plots")] static void DevReset() => FarmDevTools.Instance?.ResetAllPlots();

        [MenuItem("Mossvale/Dev/Grow All Crops One Stage %#g", true)]
        [MenuItem("Mossvale/Dev/Water All Plots", true)]
        [MenuItem("Mossvale/Dev/Give 5 Of Every Seed", true)]
        [MenuItem("Mossvale/Dev/Growth Speed x1", true)]
        [MenuItem("Mossvale/Dev/Growth Speed x10", true)]
        [MenuItem("Mossvale/Dev/Growth Speed x50", true)]
        [MenuItem("Mossvale/Dev/Showcase Every Crop Stage", true)]
        [MenuItem("Mossvale/Dev/Reset All Plots", true)]
        static bool DevValidate() => EditorApplication.isPlaying && FarmDevTools.Instance != null;
    }
}
