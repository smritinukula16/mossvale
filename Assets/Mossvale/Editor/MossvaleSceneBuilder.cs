// Mossvale environment builder (Unity 6 / URP 17).
// Builds Assets/Mossvale/Scenes/Mossvale_Main.unity from the Mossvale_Environment asset pack.
//
// Menu:  Mossvale > Build Everything            (runs steps 1-5 in order)
//        Mossvale > Steps > 1..5                (individual steps)
// Batch: Unity -batchmode -projectPath <proj> -executeMethod Mossvale.EditorTools.MossvaleSceneBuilder.BatchBuildAll
//
// Only writes inside Assets/Mossvale (plus GraphicsSettings default pipeline + build scene list).
// Farm.unity, Assets/Material, Assets/Scripts and the Gridness pack are never touched.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Mossvale.EditorTools
{
    public static class MossvaleSceneBuilder
    {
        const string Root = "Assets/Mossvale";
        const string ModelsDir = Root + "/Models";
        const string LevelModel = ModelsDir + "/Mossvale_Level.fbx";
        const string MatDir = Root + "/Materials";
        const string PrefabDir = Root + "/Prefabs";
        const string SettingsDir = Root + "/Settings";
        public const string ScenePath = Root + "/Scenes/Mossvale_Main.unity";
        const string PaletteCsv = Root + "/Data/Mossvale_Palette.csv";
        const string LayoutJson = Root + "/Data/Mossvale_Layout.json";
        const string Tag = "[Mossvale] ";

        // Spawn on the dirt road just inside the entrance arch, facing north (+Z).
        public static readonly Vector3 PlayerSpawn = new Vector3(2.6f, 0.02f, -20f);
        static readonly Vector3 CameraOffset = new Vector3(0f, 9f, -12f); // matches FarmClickMovement default

        // ------------------------------------------------------------------ layout data
        [Serializable] class LayoutObject { public string name, collection, prototype, parent; }
        [Serializable] class LayoutLight { public string name; public float[] dir, color; public float energy; }
        [Serializable] class LayoutCamera { public string name; public float[] pos, dir; }
        [Serializable] class Layout { public LayoutObject[] objects; public LayoutLight[] lights; public LayoutCamera[] cameras; }

        enum ColliderKind { None, Mesh, Box, Tree, Trigger, Pole }

        // ------------------------------------------------------------------ menu
        [MenuItem("Mossvale/Build Everything", priority = 0)]
        public static void BuildEverything()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            SetupURP();
            SetupMaterialsAndModels();
            CreatePrefabs();
            BuildScene();
            BakeEnvironmentLighting();
            // Rebuilding the environment recreates the scene; re-apply the farming gameplay if it has been set up.
            if (AssetDatabase.LoadAssetAtPath<Object>("Assets/Mossvale/Prefabs/Farming/FarmBed.prefab") != null)
                MossvaleFarmingSetup.ApplyToScene();
            EditorSceneManager.OpenScene(ScenePath);
            Debug.Log(Tag + "Build Everything finished. Scene: " + ScenePath);
        }

        [MenuItem("Mossvale/Steps/1. Configure URP Pipeline", priority = 20)]
        public static void SetupURP()
        {
            EnsureFolder(SettingsDir);
            const string rdPath = SettingsDir + "/Mossvale_URP_Renderer.asset";
            const string apPath = SettingsDir + "/Mossvale_URP.asset";

            var rd = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rdPath);
            if (rd == null)
            {
                rd = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(rd, rdPath);
            }
            if (rd.postProcessData == null)
                rd.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>(
                    "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
            EditorUtility.SetDirty(rd);

            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(apPath);
            if (asset == null)
            {
                asset = UniversalRenderPipelineAsset.Create(rd);
                AssetDatabase.CreateAsset(asset, apPath);
            }
            asset.supportsHDR = true;
            asset.msaaSampleCount = 4;
            asset.shadowDistance = 70f;
            asset.shadowCascadeCount = 2;
            asset.maxAdditionalLightsCount = 8;
            SetSerialized(asset, "m_SoftShadowsSupported", true);
            SetSerialized(asset, "m_MainLightShadowmapResolution", 2048);
            EditorUtility.SetDirty(asset);

            GraphicsSettings.defaultRenderPipeline = asset;
            // Clear any per-quality-level overrides so every level uses the Mossvale URP asset.
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                if (QualitySettings.renderPipeline != null && QualitySettings.renderPipeline != asset)
                    QualitySettings.renderPipeline = asset;
            }
            QualitySettings.SetQualityLevel(current, false);

            AssetDatabase.SaveAssets();
            Debug.Log(Tag + "URP asset assigned: " + apPath);
        }

        [MenuItem("Mossvale/Steps/2. Create URP Materials + Configure Models", priority = 21)]
        public static void SetupMaterialsAndModels()
        {
            EnsureFolder(MatDir);
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) throw new Exception("URP Lit shader not found - is the URP package installed?");

            var mats = new Dictionary<string, Material>();
            foreach (var line in File.ReadAllLines(PaletteCsv).Skip(1))
            {
                var f = line.Split(',');
                if (f.Length < 5) continue;
                ColorUtility.TryParseHtmlString(f[1].Trim(), out var c);
                mats[f[0].Trim()] = CreateOrUpdateMaterial(f[0].Trim(), c, F(f[2]), F(f[3]), F(f[4]), lit);
            }

            var missing = new HashSet<string>();
            foreach (var path in ModelPaths(includeLevel: true))
            {
                var imp = (ModelImporter)AssetImporter.GetAtPath(path);
                imp.globalScale = 1f;
                imp.useFileScale = true;
                imp.importCameras = false;
                imp.importLights = false;
                imp.importAnimation = false;
                imp.animationType = ModelImporterAnimationType.None;
                imp.importBlendShapes = false;
                imp.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                imp.materialLocation = ModelImporterMaterialLocation.InPrefab;

                var names = new Dictionary<string, Material>();
                foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (o is Material em) names[em.name] = em;
                foreach (var kv in imp.GetExternalObjectMap())
                    if (kv.Key.type == typeof(Material) && !names.ContainsKey(kv.Key.name)) names[kv.Key.name] = kv.Value as Material;

                foreach (var kv in names)
                {
                    string key = StripBlenderSuffix(kv.Key);
                    if (!mats.TryGetValue(key, out var mat))
                    {
                        // Not in the palette: build a URP material from the colour embedded in the FBX.
                        Color c = Color.gray;
                        var src = kv.Value;
                        if (src != null && src.HasProperty("_BaseColor")) c = src.GetColor("_BaseColor");
                        else if (src != null && src.HasProperty("_Color")) c = src.GetColor("_Color");
                        mat = CreateOrUpdateMaterial(key, c, 0.9f, 0f, 1f, lit);
                        mats[key] = mat;
                        missing.Add(key);
                    }
                    imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), kv.Key), mat);
                }
                imp.SaveAndReimport();
            }
            AssetDatabase.SaveAssets();
            Debug.Log(Tag + $"Materials ready ({mats.Count}). Not in palette (created from FBX colour): " +
                      (missing.Count == 0 ? "none" : string.Join(", ", missing)));
        }

        [MenuItem("Mossvale/Steps/3. Create Asset Prefabs (with colliders)", priority = 22)]
        public static void CreatePrefabs()
        {
            var preview = EditorSceneManager.NewPreviewScene();
            int n = 0;
            try
            {
                foreach (var path in ModelPaths(includeLevel: false))
                {
                    string category = Path.GetFileName(Path.GetDirectoryName(path));
                    string proto = Path.GetFileNameWithoutExtension(path);
                    var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    var inst = (GameObject)PrefabUtility.InstantiatePrefab(model, preview);
                    inst.name = proto;
                    ConfigureObject(inst, proto);
                    EnsureFolder(PrefabDir + "/" + category);
                    PrefabUtility.SaveAsPrefabAsset(inst, $"{PrefabDir}/{category}/{proto}.prefab");
                    Object.DestroyImmediate(inst);
                    n++;
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
            Debug.Log(Tag + $"Prefabs created: {n}");
        }

        [MenuItem("Mossvale/Steps/4. Build Mossvale_Main Scene", priority = 23)]
        public static void BuildScene()
        {
            if (!Application.isBatchMode)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                if (File.Exists(ScenePath) && !EditorUtility.DisplayDialog("Mossvale",
                        "Mossvale_Main.unity already exists. Rebuild it from scratch?", "Rebuild", "Cancel")) return;
            }

            EnsureFolder(Root + "/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var layout = JsonUtility.FromJson<Layout>(File.ReadAllText(LayoutJson));
            var mats = AssetDatabase.FindAssets("t:Material", new[] { MatDir })
                .Select(g => AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g)))
                .ToDictionary(m => m.name, m => m);
            var prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabDir })
                .Select(g => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g)))
                .ToDictionary(p => p.name, p => p);

            var env = new GameObject("Environment").transform;
            var groups = new Dictionary<string, Transform>();
            Transform Group(string path)
            {
                if (groups.TryGetValue(path, out var g)) return g;
                int i = path.LastIndexOf('/');
                var parent = i < 0 ? env : Group(path.Substring(0, i));
                g = new GameObject(i < 0 ? path : path.Substring(i + 1)).transform;
                g.SetParent(parent, false);
                groups[path] = g;
                return g;
            }
            // Create zone groups in a readable order.
            foreach (var z in new[] { "Terrain", "Paths", "Farmhouse_Yard", "Barn_and_Storage", "Crop_Garden", "Pond",
                         "Windmill_and_Water_Tower", "Market_and_Entrance", "Countryside", "Nature", "World_Bounds" })
                Group(z);

            // Instantiate the complete level FBX as the placement source.
            var level = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(LevelModel));
            level.name = "__LevelSource";
            // Unity imports this FBX as Blender (x,y,z) -> Unity (-x, z, -y). Rotating 180 deg about Y gives
            // (x, z, y): entrance at -Z, north = +Z, matching the pack previews, layout data and light/camera presets.
            level.transform.rotation = Quaternion.Euler(0f, 180f, 0f) * level.transform.rotation;
            var nodes = new Dictionary<string, Transform>();
            foreach (var t in level.GetComponentsInChildren<Transform>(true))
                if (!nodes.ContainsKey(t.name)) nodes[t.name] = t;

            int fromPrefab = 0, fromLevel = 0;
            var notFound = new List<string>();
            var mismatched = new List<string>();
            var handled = new HashSet<string>();
            var placedByProto = new List<(GameObject go, string proto)>();

            foreach (var o in layout.objects)
            {
                if (!string.IsNullOrEmpty(o.parent) && handled.Contains(o.parent)) continue;
                if (!nodes.TryGetValue(o.name, out var node)) { notFound.Add(o.name); continue; }

                string proto = string.IsNullOrEmpty(o.prototype) ? StripInstanceSuffix(o.name) : o.prototype;
                var group = Group(GroupPathFor(o.collection, proto, node.position));
                GameObject placed = null;

                if (prefabs.TryGetValue(proto, out var prefab))
                {
                    var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                    if (AlignToNode(inst, node.gameObject))
                    {
                        inst.name = o.name;
                        inst.transform.SetParent(group, true);
                        placed = inst;
                        fromPrefab++;
                    }
                    else
                    {
                        mismatched.Add(o.name);
                        Object.DestroyImmediate(inst);
                    }
                }
                if (placed == null)
                {
                    node.SetParent(group, true);
                    placed = node.gameObject;
                    ConfigureObject(placed, proto);
                    fromLevel++;
                }
                handled.Add(o.name);
                placedByProto.Add((placed, proto));
            }
            Object.DestroyImmediate(level);

            // Pond water: block the player from walking into the pond.
            foreach (var (go, proto) in placedByProto.Where(p => p.proto == "Pond_Water"))
            {
                var mf = go.GetComponentInChildren<MeshFilter>();
                if (mf == null) continue;
                var b = mf.sharedMesh.bounds;
                var box = mf.gameObject.AddComponent<BoxCollider>();
                box.center = new Vector3(b.center.x, b.min.y + 1.25f, b.center.z);
                box.size = new Vector3(b.size.x * 0.9f, 2.5f, b.size.z * 0.9f);
            }

            // Static flags for scenery that never moves (excludes gates, props, crop beds, windmill sails).
            foreach (var (go, proto) in placedByProto)
                if (IsStaticScenery(proto)) SetStaticRecursive(go);

            BuildWorldBounds(Group("World_Bounds"), placedByProto.FirstOrDefault(p => p.proto == "Ground").go);
            var lightingRoot = BuildLighting(layout, placedByProto);
            BuildCameras(layout);
            BuildPlayer(mats);

            // Build settings: Mossvale_Main first, keep the existing entries.
            var list = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log(Tag + $"Scene saved: {ScenePath}. Objects from prefabs: {fromPrefab}, from level mesh: {fromLevel}. " +
                      $"Not found: {(notFound.Count == 0 ? "none" : string.Join(",", notFound))}. " +
                      $"Prefab/level mismatch (kept level mesh): {(mismatched.Count == 0 ? "none" : string.Join(",", mismatched))}");
            ValidateMaterials();
        }

        [MenuItem("Mossvale/Steps/5. Generate Environment Lighting", priority = 24)]
        public static void BakeEnvironmentLighting()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath);
            // Realtime lighting only; the bake just generates the sky ambient + reflection probe data.
            Lightmapping.Bake();
            EditorSceneManager.SaveScene(scene);
            Debug.Log(Tag + "Environment lighting generated.");
        }

        // ------------------------------------------------------------------ batch entry points
        public static void BatchBuildAll()
        {
            try
            {
                BuildEverything();
                string shots = GetArg("-mossvaleShots");
                if (!string.IsNullOrEmpty(shots)) CaptureShots(shots);
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError(Tag + "BUILD FAILED: " + e);
                EditorApplication.Exit(1);
            }
        }

        public static void BatchCaptureShots()
        {
            try { EditorSceneManager.OpenScene(ScenePath); CaptureShots(GetArg("-mossvaleShots")); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogError(Tag + "CAPTURE FAILED: " + e); EditorApplication.Exit(1); }
        }

        // ------------------------------------------------------------------ classification
        static readonly HashSet<string> Buildings = new HashSet<string>
            { "Cottage", "Barn", "Silo", "Water_Tower", "Greenhouse", "Well", "Market_Stall", "Entrance_Arch", "Windmill", "Windmill_Tower" };

        static ColliderKind Classify(string proto, string partName)
        {
            if (partName.StartsWith("Windmill_Blades")) return ColliderKind.None;
            if (Buildings.Contains(proto) || proto == "Ground" || proto == "Dock") return ColliderKind.Mesh;
            if (proto.StartsWith("Tree_")) return ColliderKind.Tree;
            if (proto == "Lantern_Post" || proto.StartsWith("Sign_Post_")) return ColliderKind.Pole; // arms/boards must not block paths
            if (proto.StartsWith("Gate_")) return ColliderKind.Trigger; // passable now, interactable later
            if (proto.StartsWith("Fence_") || proto.StartsWith("Bush_") || proto.StartsWith("Hedge_") ||
                proto == "Rock_Large" || proto == "Rock_Medium" ||
                proto.StartsWith("CropBed_") || proto.StartsWith("FlowerBed_")) return ColliderKind.Box;
            if (IsProp(proto)) return ColliderKind.Box;
            return ColliderKind.None; // grass, flowers, mushrooms, pebbles, small/flat rocks, reeds, lilies, paths, patches
        }

        static bool IsProp(string proto) =>
            AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/Props/{proto}.prefab") != null ||
            File.Exists($"{ModelsDir}/Props/{proto}.fbx");

        static bool IsStaticScenery(string proto) =>
            !(proto.StartsWith("Gate_") || proto.StartsWith("CropBed_") || proto.StartsWith("FlowerBed_") ||
              proto == "Windmill" || proto == "Pond_Water" || IsProp(proto));

        static string GroupPathFor(string collection, string proto, Vector3 p)
        {
            if (collection == "Environment_Ground") return "Terrain";
            if (collection == "Trees_and_Nature")
            {
                if (proto.StartsWith("Tree_")) return "Nature/Trees";
                if (proto.StartsWith("Bush_") || proto.StartsWith("Hedge_")) return "Nature/Bushes_and_Hedges";
                if (proto.StartsWith("Rock_") || proto == "Pebbles") return "Nature/Rocks";
                if (proto.StartsWith("Mushrooms_")) return "Nature/Mushrooms";
                return "Nature/Flowers_and_Grass";
            }
            if (proto.StartsWith("Path_")) return "Paths";
            if (collection == "Water") return "Pond";

            string zone;
            if (p.x >= 2.5f && p.x <= 23.6f && p.z >= -13f && p.z <= 4.2f) zone = "Crop_Garden";
            else if (p.x >= 8f && p.z >= 6.5f) zone = "Barn_and_Storage";
            else if (p.x >= -15f && p.x <= 1.8f && p.z >= 2.5f && p.z <= 17f) zone = "Farmhouse_Yard";
            else if (p.z <= -14f) zone = "Market_and_Entrance";
            else if (p.z >= 16f) zone = "Windmill_and_Water_Tower";
            else zone = "Countryside";

            string type;
            if (collection == "Buildings") type = "Buildings";
            else if (proto.StartsWith("CropBed_")) type = "Crop_Plots";
            else if (proto.StartsWith("FlowerBed_")) type = "Flower_Beds";
            else if (proto.StartsWith("Fence_") || proto.StartsWith("Gate_")) type = "Fences_and_Gates";
            else if (proto.StartsWith("Hay_") || proto == "Cart_Hay") type = "Hay";
            else if (proto.StartsWith("Crate_") || proto.StartsWith("Barrel") || proto.StartsWith("Sack_") || proto.StartsWith("Basket_")) type = "Storage";
            else if (proto.StartsWith("Sign_")) type = "Signs";
            else if (proto == "Lantern_Post") type = "Lanterns";
            else type = "Props";
            return zone + "/" + type;
        }

        static void ConfigureObject(GameObject root, string proto)
        {
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var go = mf.gameObject;
                if (go.GetComponent<Collider>() != null) continue;
                switch (Classify(proto, go.name))
                {
                    case ColliderKind.Mesh:
                        go.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
                        break;
                    case ColliderKind.Box:
                        go.AddComponent<BoxCollider>();
                        break;
                    case ColliderKind.Trigger:
                        go.AddComponent<BoxCollider>().isTrigger = true;
                        break;
                    case ColliderKind.Pole:
                        var pb = mf.sharedMesh.bounds;
                        var pole = go.AddComponent<BoxCollider>();
                        pole.center = new Vector3(0f, pb.min.y + pb.size.y * 0.5f, 0f); // pivot = base of the pole
                        pole.size = new Vector3(0.22f, pb.size.y, 0.22f);
                        break;
                    case ColliderKind.Tree:
                        var b = mf.sharedMesh.bounds;
                        var cap = go.AddComponent<CapsuleCollider>();
                        cap.direction = 1;
                        cap.height = Mathf.Min(2.6f, b.size.y);
                        cap.radius = Mathf.Clamp(Mathf.Min(b.size.x, b.size.z) * 0.12f, 0.22f, 0.45f);
                        cap.center = new Vector3(b.center.x, b.min.y + cap.height * 0.5f, b.center.z);
                        break;
                }
                if (go.name.StartsWith("Windmill_Blades") && go.GetComponent<MossvaleSpin>() == null)
                    go.AddComponent<MossvaleSpin>();
            }
        }

        // Places a prefab instance so its main mesh coincides with the same mesh in the level FBX.
        static bool AlignToNode(GameObject inst, GameObject node)
        {
            var srcMf = LargestMesh(node);
            var dstMf = LargestMesh(inst);
            if (srcMf == null || dstMf == null) return false;
            inst.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            inst.transform.localScale = Vector3.one;
            Matrix4x4 rel = dstMf.transform.localToWorldMatrix; // root at identity => mesh relative to root
            Matrix4x4 w = srcMf.transform.localToWorldMatrix * rel.inverse;
            inst.transform.SetPositionAndRotation(w.GetPosition(), w.rotation);
            inst.transform.localScale = w.lossyScale;

            var a = CombinedBounds(inst);
            var b = CombinedBounds(node);
            float tol = 0.05f + 0.02f * b.size.magnitude;
            return Vector3.Distance(a.center, b.center) < tol && (a.size - b.size).magnitude < tol;
        }

        static MeshFilter LargestMesh(GameObject go) =>
            go.GetComponentsInChildren<MeshFilter>(true).Where(m => m.sharedMesh != null)
              .OrderByDescending(m => m.sharedMesh.vertexCount).FirstOrDefault();

        static Bounds CombinedBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        static void SetStaticRecursive(GameObject go)
        {
            if (go.GetComponent<MossvaleSpin>() != null) return;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic |
                                                       StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic);
            foreach (Transform c in go.transform) SetStaticRecursive(c.gameObject);
        }

        // ------------------------------------------------------------------ world pieces
        static void BuildWorldBounds(Transform parent, GameObject ground)
        {
            var b = ground != null ? CombinedBounds(ground) : new Bounds(Vector3.zero, new Vector3(50, 1, 50));
            float h = 4f, t = 1f;
            var c = new Vector3(b.center.x, h / 2f, b.center.z);
            void Wall(string n, Vector3 pos, Vector3 size)
            {
                var w = new GameObject(n);
                w.transform.SetParent(parent, false);
                w.transform.position = pos;
                w.layer = 2; // Ignore Raycast: blocks walking, but clicks pass through to the ground
                w.AddComponent<BoxCollider>().size = size;
                GameObjectUtility.SetStaticEditorFlags(w, StaticEditorFlags.BatchingStatic);
            }
            Wall("Bound_North", c + new Vector3(0, 0, b.extents.z + t / 2 - 0.3f), new Vector3(b.size.x, h, t));
            Wall("Bound_South", c - new Vector3(0, 0, b.extents.z + t / 2 - 0.3f), new Vector3(b.size.x, h, t));
            Wall("Bound_East", c + new Vector3(b.extents.x + t / 2 - 0.3f, 0, 0), new Vector3(t, h, b.size.z));
            Wall("Bound_West", c - new Vector3(b.extents.x + t / 2 - 0.3f, 0, 0), new Vector3(t, h, b.size.z));
        }

        static Transform BuildLighting(Layout layout, List<(GameObject go, string proto)> placed)
        {
            var root = new GameObject("Lighting").transform;
            root.SetSiblingIndex(0);

            foreach (var l in layout.lights)
            {
                var go = new GameObject(l.name);
                go.transform.SetParent(root, false);
                go.transform.rotation = Quaternion.LookRotation(V(l.dir));
                var lt = go.AddComponent<Light>();
                lt.type = LightType.Directional;
                lt.color = new Color(l.color[0], l.color[1], l.color[2]);
                bool main = l.name == "Sun_Main";
                lt.intensity = main ? 1.55f : 0.35f;
                lt.shadows = main ? LightShadows.Soft : LightShadows.None;
                lt.shadowStrength = 0.8f;
                if (main) RenderSettings.sun = lt;
            }

            // Warm lantern glow (lights are not exported from Blender).
            var lanternRoot = new GameObject("Lantern_Lights").transform;
            lanternRoot.SetParent(root, false);
            foreach (var (go, proto) in placed.Where(p => p.proto == "Lantern_Post"))
            {
                var b = CombinedBounds(go);
                var l = new GameObject("Light_" + go.name);
                l.transform.SetParent(lanternRoot, false);
                l.transform.position = new Vector3(b.center.x, b.max.y - 0.3f, b.center.z);
                var lt = l.AddComponent<Light>();
                lt.type = LightType.Point;
                lt.color = new Color(1f, 0.78f, 0.45f);
                lt.intensity = 1.2f;
                lt.range = 5f;
                lt.shadows = LightShadows.None;
            }

            // Sky, ambient and fog: soft storybook daylight.
            var sky = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/Sky_Mossvale.mat");
            if (sky == null)
            {
                sky = new Material(Shader.Find("Skybox/Procedural"));
                AssetDatabase.CreateAsset(sky, MatDir + "/Sky_Mossvale.mat");
            }
            sky.SetColor("_SkyTint", new Color(0.45f, 0.68f, 1f));
            sky.SetColor("_GroundColor", new Color(0.93f, 0.86f, 0.72f));
            sky.SetFloat("_AtmosphereThickness", 0.75f);
            sky.SetFloat("_Exposure", 1.25f);
            sky.SetFloat("_SunSize", 0.03f);
            EditorUtility.SetDirty(sky);
            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.66f, 0.76f, 0.92f);
            RenderSettings.ambientEquatorColor = new Color(0.78f, 0.80f, 0.68f);
            RenderSettings.ambientGroundColor = new Color(0.45f, 0.40f, 0.30f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.80f, 0.88f, 0.95f);
            RenderSettings.fogStartDistance = 110f;
            RenderSettings.fogEndDistance = 360f;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;

            // Realtime-only lighting settings (no lightmap baking needed).
            const string lsPath = SettingsDir + "/Mossvale_Lighting.lighting";
            var ls = AssetDatabase.LoadAssetAtPath<LightingSettings>(lsPath);
            if (ls == null)
            {
                ls = new LightingSettings { name = "Mossvale_Lighting" };
                AssetDatabase.CreateAsset(ls, lsPath);
            }
            ls.bakedGI = false;
            ls.realtimeGI = false;
            Lightmapping.lightingSettings = ls;

            // Post-processing volume.
            var volGo = new GameObject("Global_Volume");
            volGo.transform.SetParent(root, false);
            var vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.sharedProfile = GetOrCreatePostFx();
            return root;
        }

        static VolumeProfile GetOrCreatePostFx()
        {
            const string path = SettingsDir + "/Mossvale_PostFX.asset";
            var prof = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (prof != null) return prof;
            prof = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(prof, path);

            var tm = prof.Add<Tonemapping>(); tm.mode.Override(TonemappingMode.Neutral);
            var bl = prof.Add<Bloom>(); bl.threshold.Override(1.0f); bl.intensity.Override(0.35f); bl.scatter.Override(0.6f);
            var ca = prof.Add<ColorAdjustments>(); ca.postExposure.Override(0.05f); ca.contrast.Override(10f); ca.saturation.Override(8f);
            var wb = prof.Add<WhiteBalance>(); wb.temperature.Override(8f);
            var vg = prof.Add<Vignette>(); vg.intensity.Override(0.18f); vg.smoothness.Override(0.5f);
            foreach (var c in prof.components) AssetDatabase.AddObjectToAsset(c, prof);
            EditorUtility.SetDirty(prof);
            AssetDatabase.SaveAssets();
            return prof;
        }

        static void BuildCameras(Layout layout)
        {
            var root = new GameObject("Cameras").transform;
            root.SetSiblingIndex(1);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            camGo.transform.SetParent(root, false);
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 50f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 400f;
            cam.clearFlags = CameraClearFlags.Skybox;
            camGo.AddComponent<AudioListener>();
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            camGo.transform.position = PlayerSpawn + CameraOffset;
            camGo.transform.LookAt(PlayerSpawn);

            // Showcase cameras matching the pack's preview renders (disabled; enable one to frame the whole farm).
            var show = new GameObject("Showcase_Cameras (disabled)").transform;
            show.SetParent(root, false);
            foreach (var c in layout.cameras)
            {
                var go = new GameObject(c.name);
                go.transform.SetParent(show, false);
                go.transform.position = V(c.pos);
                go.transform.rotation = Quaternion.LookRotation(V(c.dir));
                var sc = go.AddComponent<Camera>();
                sc.fieldOfView = c.name.Contains("Isometric") ? 30f : c.name.Contains("TopDown") ? 36f : 42f;
                sc.farClipPlane = 400f;
                var d = sc.GetUniversalAdditionalCameraData();
                d.renderPostProcessing = true;
                d.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                sc.enabled = false;
            }
        }

        static void BuildPlayer(Dictionary<string, Material> mats)
        {
            var player = new GameObject("Player") { tag = "Player" };
            player.transform.position = PlayerSpawn;
            var cc = player.AddComponent<CharacterController>();
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.height = 1.8f;
            cc.radius = 0.35f;
            cc.stepOffset = 0.3f;
            cc.skinWidth = 0.04f;
            cc.slopeLimit = 45f;
            cc.minMoveDistance = 0f; // default 0.001 silently drops tiny per-frame moves at high frame rates
            player.AddComponent<FarmClickMovement>(); // existing script from Assets/Scripts (unchanged)
            player.AddComponent<MossvaleGravity>();   // gravity so the player comes back down off porches/steps

            // Simple low-poly farmer silhouette (no character mesh was found in Downloads).
            var vis = new GameObject("Farmer_Visual").transform;
            vis.SetParent(player.transform, false);
            Material M(string n) => mats.TryGetValue(n, out var m) ? m : null;
            void Part(string n, PrimitiveType type, Vector3 pos, Vector3 scale, string mat)
            {
                var p = GameObject.CreatePrimitive(type);
                p.name = n;
                Object.DestroyImmediate(p.GetComponent<Collider>());
                p.transform.SetParent(vis, false);
                p.transform.localPosition = pos;
                p.transform.localScale = scale;
                p.GetComponent<MeshRenderer>().sharedMaterial = M(mat);
            }
            Part("Overalls", PrimitiveType.Capsule, new Vector3(0, 0.62f, 0), new Vector3(0.62f, 0.6f, 0.55f), "MV_TankBlue");
            Part("Shirt", PrimitiveType.Capsule, new Vector3(0, 0.95f, 0), new Vector3(0.66f, 0.35f, 0.58f), "MV_ScarecrowShirt");
            Part("Head", PrimitiveType.Sphere, new Vector3(0, 1.45f, 0), new Vector3(0.42f, 0.42f, 0.42f), "MV_WoodLight");
            Part("Nose", PrimitiveType.Sphere, new Vector3(0, 1.43f, 0.2f), new Vector3(0.09f, 0.09f, 0.09f), "MV_WoodLight");
            Part("Hat_Brim", PrimitiveType.Cylinder, new Vector3(0, 1.62f, 0), new Vector3(0.78f, 0.025f, 0.78f), "MV_ScarecrowHat");
            Part("Hat_Crown", PrimitiveType.Cylinder, new Vector3(0, 1.72f, 0), new Vector3(0.4f, 0.1f, 0.4f), "MV_ScarecrowHat");
            Part("Hat_Band", PrimitiveType.Cylinder, new Vector3(0, 1.66f, 0), new Vector3(0.41f, 0.03f, 0.41f), "MV_BarnRed");
        }

        // ------------------------------------------------------------------ verification
        static void ValidateMaterials()
        {
            int bad = 0, total = 0;
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                foreach (var m in r.sharedMaterials)
                {
                    total++;
                    if (m == null || m.shader == null || m.shader.name == "Hidden/InternalErrorShader" ||
                        !m.shader.name.StartsWith("Universal Render Pipeline"))
                    {
                        bad++;
                        if (bad <= 20) Debug.LogWarning(Tag + $"MATERIAL ISSUE on {r.name}: {(m == null ? "null" : m.name + " / " + m.shader?.name)}");
                    }
                }
            Debug.Log(Tag + $"Material check: {total} material slots, {bad} non-URP/missing.");
        }

        static void CaptureShots(string dir)
        {
            Directory.CreateDirectory(dir);
            var cams = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var cam in cams)
            {
                var rt = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32);
                bool was = cam.enabled;
                cam.enabled = true;
                var req = new RenderPipeline.StandardRequest { destination = rt };
                if (RenderPipeline.SupportsRenderRequest(cam, req)) RenderPipeline.SubmitRenderRequest(cam, req);
                else { cam.targetTexture = rt; cam.Render(); cam.targetTexture = null; }
                cam.enabled = was;
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                File.WriteAllBytes(Path.Combine(dir, cam.name.Replace(' ', '_') + ".png"), tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                rt.Release();
                Object.DestroyImmediate(rt);
            }
            Debug.Log(Tag + $"Captured {cams.Length} camera shots to {dir}");
        }

        // ------------------------------------------------------------------ helpers
        static Material CreateOrUpdateMaterial(string name, Color c, float roughness, float emission, float alpha, Shader lit)
        {
            string p = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (m == null) { m = new Material(lit) { name = name }; AssetDatabase.CreateAsset(m, p); }
            else if (m.shader != lit) m.shader = lit;

            c.a = alpha;
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", Mathf.Clamp01(1f - roughness) * 0.75f);
            m.SetFloat("_Metallic", name.StartsWith("MV_Metal") ? 0.4f : 0f);

            if (emission > 0f)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", c * (emission * 0.6f));
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else
            {
                m.DisableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", Color.black);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            }

            bool transparent = alpha < 0.999f;
            m.SetFloat("_Surface", transparent ? 1f : 0f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_Cull", transparent ? 0f : 2f);
            m.SetFloat("_ZWrite", transparent ? 0f : 1f);
            m.SetFloat("_SrcBlend", transparent ? (float)BlendMode.SrcAlpha : (float)BlendMode.One);
            m.SetFloat("_DstBlend", transparent ? (float)BlendMode.OneMinusSrcAlpha : (float)BlendMode.Zero);
            m.SetFloat("_SrcBlendAlpha", 1f);
            m.SetFloat("_DstBlendAlpha", transparent ? (float)BlendMode.OneMinusSrcAlpha : (float)BlendMode.Zero);
            m.SetOverrideTag("RenderType", transparent ? "Transparent" : "Opaque");
            m.renderQueue = transparent ? (int)RenderQueue.Transparent : -1;
            if (transparent) m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); else m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetShaderPassEnabled("DepthOnly", !transparent);
            m.SetShaderPassEnabled("ShadowCaster", !transparent);

            // Let URP's own material GUI finalise keywords (resolved by reflection to stay version-tolerant).
            var gui = TypeCache.GetTypesDerivedFrom<ShaderGUI>().FirstOrDefault(t => t.Name == "BaseShaderGUI");
            var setKw = gui?.GetMethod("SetMaterialKeywords", BindingFlags.Public | BindingFlags.Static);
            try { setKw?.Invoke(null, new object[] { m, null, null }); } catch { /* manual setup above is sufficient */ }

            EditorUtility.SetDirty(m);
            return m;
        }

        static IEnumerable<string> ModelPaths(bool includeLevel) =>
            AssetDatabase.FindAssets("t:Model", new[] { ModelsDir })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                .Where(p => includeLevel || p != LevelModel)
                .Distinct().OrderBy(p => p);

        static string StripBlenderSuffix(string n)
        {
            int i = n.LastIndexOf('.');
            return i > 0 && n.Substring(i + 1).All(char.IsDigit) ? n.Substring(0, i) : n;
        }

        static string StripInstanceSuffix(string n)
        {
            int i = n.LastIndexOf('_');
            return i > 0 && n.Substring(i + 1).All(char.IsDigit) ? n.Substring(0, i) : n;
        }

        static void SetSerialized(Object o, string prop, object value)
        {
            var so = new SerializedObject(o);
            var p = so.FindProperty(prop);
            if (p == null) return;
            switch (value)
            {
                case bool b: p.boolValue = b; break;
                case int i: p.intValue = i; break; // for enums this sets the underlying value
                case float f: p.floatValue = f; break;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int i = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, i));
            AssetDatabase.CreateFolder(path.Substring(0, i), path.Substring(i + 1));
        }

        static float F(string s) => float.Parse(s.Trim(), CultureInfo.InvariantCulture);
        static Vector3 V(float[] a) => new Vector3(a[0], a[1], a[2]);

        static string GetArg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
    }
}
