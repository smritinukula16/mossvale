// Mossvale progression + buildings setup (Unity 6 / URP).
// Creates the UI art (procedural sprites), TMP icon sprites, UIStyle, ProgressionConfig, ItemDatabase, Heart of Mossvale,
// and wires FarmProgressionManager, Wallet, SaveManager, building interactions, bed locks and panels into Mossvale_Main.
//
// Menu:  Mossvale > Progression > Setup Progression (assets + scene)
// Batch: -executeMethod Mossvale.EditorTools.MossvaleProgressionSetup.BatchSetup
//
// Re-runnable: removes only what it created ("Progression", "Progression_HUD", "Heart_of_Mossvale", building components,
// BedLocks, badges) before re-adding. Existing ProgressionConfig / UIStyle values are kept if the assets already exist.

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
using UnityEngine.SceneManagement;
using UnityEngine.TextCore;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Mossvale.EditorTools
{
    public static class MossvaleProgressionSetup
    {
        const string Root = "Assets/Mossvale";
        const string ScenePath = Root + "/Scenes/Mossvale_Main.unity";
        const string SpriteDir = Root + "/UI/Progression/Sprites";
        const string UiDir = Root + "/UI/Progression";
        const string DataDir = Root + "/Data/Farming";
        const string PrefabDir = Root + "/Prefabs/Progression";
        const string MatDir = Root + "/Materials/Farming";
        const string ConfigPath = DataDir + "/ProgressionConfig.asset";
        const string StylePath = UiDir + "/MossvaleUIStyle.asset";
        const string DbPath = DataDir + "/ItemDatabase.asset";
        const string Tag = "[MossvaleProgression] ";

        [MenuItem("Mossvale/Progression/Setup Progression (assets + scene)", priority = 60)]
        public static void SetupMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            CreateAssets();
            var scene = EditorSceneManager.OpenScene(ScenePath);
            ApplyToOpenScene();
            EditorSceneManager.SaveScene(scene);
        }

        public static void BatchSetup()
        {
            try
            {
                CreateAssets();
                var scene = EditorSceneManager.OpenScene(ScenePath);
                ApplyToOpenScene();
                EditorSceneManager.SaveScene(scene);
                EditorApplication.Exit(0);
            }
            catch (Exception e) { Debug.LogError(Tag + "SETUP FAILED: " + e); EditorApplication.Exit(1); }
        }

        public static bool AssetsExist => AssetDatabase.LoadAssetAtPath<ProgressionConfig>(ConfigPath) != null;

        // ================================================================== assets
        public static void CreateAssets()
        {
            foreach (var d in new[] { SpriteDir, UiDir, DataDir, PrefabDir }) MossvaleFarmingSetup.EnsureFolder(d);
            var sprites = CreateSprites();
            var spriteAsset = CreateTmpIcons();
            CreateStyle(sprites);
            CreateDatabase();
            CreateConfig();
            AssetDatabase.SaveAssets();
            Debug.Log(Tag + "Assets ready");
        }

        // ------------------------------------------------------------------ procedural UI sprites
        delegate Color Painter(float x, float y, int w, int h);   // returns premultiplied-free RGBA

        static Dictionary<string, Sprite> CreateSprites()
        {
            var s = new Dictionary<string, Sprite>();
            s["panel"] = Make("ui_panel", 64, 64, 22, RoundRect(64, 64, 20, (x, y) => BorderShade(x, y, 64, 64, 20, 3, 1f, 0.86f)));
            s["card"] = Make("ui_card", 48, 48, 16, RoundRect(48, 48, 14, (x, y) => BorderShade(x, y, 48, 48, 14, 2, 1f, 0.88f)));
            s["button"] = Make("ui_button", 64, 64, 20, RoundRect(64, 64, 18, (x, y) =>
            {
                float v = y < 7 ? 0.74f : 1f;                   // darker bottom lip = soft 3D
                if (y > 52) v = Mathf.Lerp(1f, 1.0f, 1);         // keep top bright
                float b = BorderShade(x, y, 64, 64, 18, 2, 1f, 0.8f).r;
                return Grey(Mathf.Min(v, b));
            }));
            s["bar"] = Make("ui_bar", 32, 32, 14, RoundRect(32, 32, 14, (x, y) => Grey(1f)));
            s["circle"] = Make("ui_circle", 64, 64, 0, Shape(64, 64, (x, y) => Dist(x, y, 32, 32) < 30, (x, y) => Grey(1f)));
            s["wood"] = Make("ui_wood", 96, 96, 30, RoundRect(96, 96, 26, Wood));
            s["star"] = Make("ui_star", 128, 128, 0, Star());
            s["coin"] = Make("ui_coin", 128, 128, 0, Coin());
            s["lock"] = Make("ui_lock", 128, 128, 0, Lock());
            s["check"] = Make("ui_check", 128, 128, 0, Check());
            s["close"] = Make("ui_close", 64, 64, 0, Shape(64, 64, (x, y) => SegDist(x, y, 12, 12, 52, 52) < 6.5f || SegDist(x, y, 12, 52, 52, 12) < 6.5f, (x, y) => Grey(1f)));
            s["glow"] = Make("ui_glow", 128, 128, 0, Glow());
            s["heart"] = Make("ui_heart", 128, 128, 0, Heart());
            return s;
        }

        static Color Grey(float v, float a = 1f) => new Color(v, v, v, a);
        static float Dist(float x, float y, float cx, float cy) => Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));

        static float SegDist(float px, float py, float ax, float ay, float bx, float by)
        {
            float dx = bx - ax, dy = by - ay;
            float t = Mathf.Clamp01(((px - ax) * dx + (py - ay) * dy) / (dx * dx + dy * dy));
            return Dist(px, py, ax + t * dx, ay + t * dy);
        }

        static float RoundRectSdf(float x, float y, float w, float h, float r)
        {
            float qx = Mathf.Abs(x - w / 2f) - (w / 2f - r), qy = Mathf.Abs(y - h / 2f) - (h / 2f - r);
            return Mathf.Min(Mathf.Max(qx, qy), 0f) + new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude - r;
        }

        static Color BorderShade(float x, float y, int w, int h, float r, float border, float inside, float edge)
        {
            float d = RoundRectSdf(x, y, w, h, r);
            return Grey(d > -border ? edge : inside);
        }

        static Painter RoundRect(int w, int h, float r, Func<float, float, Color> color) => (x, y, W, H) =>
        {
            float d = RoundRectSdf(x, y, w, h, r);
            var c = color(x, y);
            c.a *= Mathf.Clamp01(0.5f - d);
            return c;
        };

        static Painter Shape(int w, int h, Func<float, float, bool> inside, Func<float, float, Color> color) => (x, y, W, H) =>
        {
            int hits = 0;   // 4x4 supersampling for smooth edges
            for (int i = 0; i < 4; i++) for (int j = 0; j < 4; j++) if (inside(x - 0.5f + (i + 0.5f) / 4f, y - 0.5f + (j + 0.5f) / 4f)) hits++;
            if (hits == 0) return new Color(0, 0, 0, 0);
            var c = color(x, y);
            c.a *= hits / 16f;
            return c;
        };

        static Color Wood(float x, float y)
        {
            var a = new Color32(0xA8, 0x6E, 0x40, 255); var b = new Color32(0x8A, 0x56, 0x30, 255);
            float n = Mathf.PerlinNoise(x * 0.04f, y * 0.25f) * 5f;
            float g = 0.5f + 0.5f * Mathf.Sin(y * 0.55f + n);
            Color c = Color.Lerp(a, b, g * g);
            float d = RoundRectSdf(x, y, 96, 96, 26);
            if (d > -4f) c = new Color32(0x4E, 0x30, 0x1B, 255);                 // dark outline
            else if (d > -7f && y > 48) c = Color.Lerp(c, Color.white, 0.18f);  // top bevel highlight
            else if (d > -7f) c = Color.Lerp(c, Color.black, 0.15f);            // bottom bevel shade
            foreach (var (nx, ny) in new[] { (17f, 17f), (79f, 17f), (17f, 79f), (79f, 79f) })   // little nails
                if (Dist(x, y, nx, ny) < 3.2f) c = new Color32(0xD9, 0xC2, 0x8F, 255);
            return c;
        }

        static Painter Star()
        {
            var pts = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float ang = Mathf.PI / 2f + i * Mathf.PI / 5f;
                float r = i % 2 == 0 ? 58f : 25f;
                pts[i] = new Vector2(64 + Mathf.Cos(ang) * r, 62 + Mathf.Sin(ang) * r);
            }
            return (x, y, W, H) =>
            {
                float d = PolySdf(x, y, pts);
                if (d > 1f) return new Color(0, 0, 0, 0);
                float a = Mathf.Clamp01(0.5f - d);
                Color top = new Color32(0xA6, 0xDA, 0xFF, 255), bottom = new Color32(0x2F, 0x7A, 0xDE, 255);
                Color c = Color.Lerp(bottom, top, y / 128f);
                if (d > -6f) c = Color.white;                                          // white rim
                else if (d > -9f) c = Color.Lerp(c, new Color32(0x1E, 0x5C, 0xB8, 255), 0.6f);
                if (y > 70 && x < 62 && d < -12f) c = Color.Lerp(c, Color.white, 0.35f); // shine
                c.a = a;
                return c;
            };
        }

        static float PolySdf(float x, float y, Vector2[] p)
        {
            float d = float.MaxValue; bool inside = false;
            for (int i = 0, j = p.Length - 1; i < p.Length; j = i++)
            {
                d = Mathf.Min(d, SegDist(x, y, p[i].x, p[i].y, p[j].x, p[j].y));
                if ((p[i].y > y) != (p[j].y > y) && x < (p[j].x - p[i].x) * (y - p[i].y) / (p[j].y - p[i].y) + p[i].x) inside = !inside;
            }
            return inside ? -d : d;
        }

        static Painter Coin() => (x, y, W, H) =>
        {
            float r = Dist(x, y, 64, 64);
            if (r > 58f) return new Color(0, 0, 0, 0);
            Color c = Color.Lerp(new Color32(0xE0, 0x9A, 0x16, 255), new Color32(0xFF, 0xDC, 0x5E, 255), y / 128f);
            if (r > 51f) c = new Color32(0xB0, 0x72, 0x12, 255);
            else if (Mathf.Abs(r - 38f) < 2.5f) c = new Color32(0xC9, 0x88, 0x1A, 255);
            if (r < 30f && x < 60 && y > 64) c = Color.Lerp(c, Color.white, 0.25f);
            c.a = Mathf.Clamp01(58.5f - r);
            return c;
        };

        static Painter Lock() => Shape(128, 128, (x, y) =>
        {
            bool body = RoundRectSdf(x - 24, y - 14, 80, 62, 12) < 0;
            float r = Dist(x, y, 64, 74);
            bool shackle = y > 70 && r > 18 && r < 30;
            bool side = (Mathf.Abs(x - 40) < 6 || Mathf.Abs(x - 88) < 6) && y > 60 && y <= 75;
            return body || shackle || side;
        }, (x, y) =>
        {
            if (y > 74) return new Color32(0x9A, 0x9C, 0xA6, 255);
            bool key = Dist(x, y, 64, 50) < 8 || (Mathf.Abs(x - 64) < 3.5f && y > 30 && y < 50);
            return key ? (Color)new Color32(0x5E, 0x3B, 0x22, 255) : Color.Lerp(new Color32(0xC0, 0x8A, 0x2E, 255), new Color32(0xF0, 0xC0, 0x50, 255), (y - 14) / 62f);
        });

        static Painter Check() => (x, y, W, H) =>
        {
            float r = Dist(x, y, 64, 64);
            if (r > 58f) return new Color(0, 0, 0, 0);
            float d = Mathf.Min(SegDist(x, y, 36, 66, 56, 44), SegDist(x, y, 56, 44, 94, 84));
            Color c = d < 8f ? Color.white : (Color)Color.Lerp(new Color32(0x4D, 0x8A, 0x33, 255), new Color32(0x86, 0xC8, 0x5A, 255), y / 128f);
            if (d >= 8f && d < 9.5f) c = Color.Lerp(c, Color.white, 0.5f);
            c.a = Mathf.Clamp01(58.5f - r);
            return c;
        };

        static Painter Glow() => (x, y, W, H) =>
        {
            float dx = x - 64, dy = y - 64, r = Mathf.Sqrt(dx * dx + dy * dy) / 64f;
            if (r >= 1f) return new Color(1, 1, 1, 0);
            float ang = Mathf.Atan2(dy, dx);
            float rays = 0.45f + 0.55f * Mathf.Pow(Mathf.Abs(Mathf.Cos(ang * 6f)), 8f);
            float a = Mathf.Pow(1f - r, 1.6f) * rays + Mathf.Pow(1f - r, 4f) * 0.6f;
            return new Color(1, 1, 1, Mathf.Clamp01(a));
        };

        static Painter Heart() => Shape(128, 128, (x, y) =>
        {
            float u = (x - 64) / 50f, v = (y - 58) / 50f;   // classic implicit heart
            float k = u * u + v * v - 1f;
            return k * k * k - u * u * v * v * v <= 0f;
        }, (x, y) =>
        {
            float shine = (x < 58 && y > 70) ? 0.35f : 0f;
            return Color.Lerp(Grey(0.82f), Color.white, shine + y / 256f);   // tinted in UI (grey->blue as it is restored)
        });

        static Sprite Make(string name, int w, int h, int border, Painter paint)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color[w * h];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) px[y * w + x] = paint(x + 0.5f, y + 0.5f, w, h);
            tex.SetPixels(px);
            tex.Apply();
            string path = $"{SpriteDir}/{name}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.filterMode = FilterMode.Bilinear;
            ti.spriteBorder = new Vector4(border, border, border, border);
            ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // ------------------------------------------------------------------ TMP inline icons (<sprite name="star">)
        static TMP_SpriteAsset CreateTmpIcons()
        {
            const int S = 128;
            var star = Star(); var coin = Coin();
            var tex = new Texture2D(S * 2, S, TextureFormat.RGBA32, false);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    tex.SetPixel(x, y, star(x + 0.5f, y + 0.5f, S, S));
                    tex.SetPixel(S + x, y, coin(x + 0.5f, y + 0.5f, S, S));
                }
            tex.Apply();
            string texPath = UiDir + "/TMP_MossvaleIcons.png";
            File.WriteAllBytes(texPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(texPath);
            var ti = (TextureImporter)AssetImporter.GetAtPath(texPath);
            ti.textureType = TextureImporterType.Default;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.SaveAndReimport();
            var sheet = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);

            string path = UiDir + "/TMP_MossvaleIcons.asset";
            var sa = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(path);
            if (sa == null)
            {
                sa = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
                AssetDatabase.CreateAsset(sa, path);
                var mat = new Material(Shader.Find("TextMeshPro/Sprite")) { name = "TMP_MossvaleIcons Material" };
                AssetDatabase.AddObjectToAsset(mat, sa);
                sa.material = mat;
            }
            // A new asset has no version, which makes TMP run its legacy upgrade (and crash on the null legacy list).
            if (sa.spriteInfoList == null) sa.spriteInfoList = new List<TMP_Sprite>();
            var vso = new SerializedObject(sa);
            var ver = vso.FindProperty("m_Version");
            if (ver != null) { ver.stringValue = "1.1.0"; vso.ApplyModifiedPropertiesWithoutUndo(); }
            sa.spriteSheet = sheet;
            sa.material.SetTexture("_MainTex", sheet);
            sa.spriteGlyphTable.Clear();
            sa.spriteCharacterTable.Clear();
            string[] names = { "star", "coin" };
            uint[] unicode = { 0x2605, 0xE100 };
            for (int i = 0; i < names.Length; i++)
            {
                var g = new TMP_SpriteGlyph((uint)i, new GlyphMetrics(S, S, 0, S * 0.86f, S * 1.06f), new GlyphRect(i * S, 0, S, S), 1f, 0);
                sa.spriteGlyphTable.Add(g);
                sa.spriteCharacterTable.Add(new TMP_SpriteCharacter(unicode[i], g) { name = names[i] });
            }
            sa.UpdateLookupTables();
            EditorUtility.SetDirty(sa);
            EditorUtility.SetDirty(sa.material);

            // Make it TMP's default sprite asset so <sprite name="star"> works in every text (farming HUD included).
            var settings = AssetDatabase.FindAssets("t:TMP_Settings").Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<TMP_Settings>).FirstOrDefault();
            if (settings != null)
            {
                var so = new SerializedObject(settings);
                so.FindProperty("m_defaultSpriteAsset").objectReferenceValue = sa;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(settings);
            }
            return sa;
        }

        static void CreateStyle(Dictionary<string, Sprite> s)
        {
            var st = AssetDatabase.LoadAssetAtPath<UIStyle>(StylePath);
            if (st == null) { st = ScriptableObject.CreateInstance<UIStyle>(); AssetDatabase.CreateAsset(st, StylePath); }
            st.font = TMP_Settings.defaultFontAsset;
            st.outlineFont = AssetDatabase.LoadAssetAtPath<Material>(Root + "/UI/Farming/Font_Outline.mat");
            st.panel = s["panel"]; st.wood = s["wood"]; st.card = s["card"]; st.button = s["button"]; st.bar = s["bar"];
            st.star = s["star"]; st.coin = s["coin"]; st.lockIcon = s["lock"]; st.check = s["check"]; st.close = s["close"];
            st.glow = s["glow"]; st.heart = s["heart"]; st.circle = s["circle"];
            EditorUtility.SetDirty(st);
        }

        static void CreateDatabase()
        {
            var db = AssetDatabase.LoadAssetAtPath<ItemDatabase>(DbPath);
            if (db == null) { db = ScriptableObject.CreateInstance<ItemDatabase>(); AssetDatabase.CreateAsset(db, DbPath); }
            db.items = AssetDatabase.FindAssets("t:ItemData", new[] { DataDir }).Select(g => AssetDatabase.LoadAssetAtPath<ItemData>(AssetDatabase.GUIDToAssetPath(g))).ToArray();
            db.crops = new[] { "Carrot", "Tomato", "Wheat" }.Select(n => AssetDatabase.LoadAssetAtPath<CropData>($"{DataDir}/Crops/Crop_{n}.asset")).ToArray();
            // Market prices (only set when unset, so Inspector edits survive re-runs).
            var prices = new Dictionary<string, int> { { "carrot", 8 }, { "tomato", 14 }, { "wheat", 10 } };
            foreach (var it in db.items)
                if (it.category == ItemCategory.Produce && it.sellPrice == 0 && prices.TryGetValue(it.itemId, out var p)) { it.sellPrice = p; EditorUtility.SetDirty(it); }
            EditorUtility.SetDirty(db);
        }

        static ItemData Item(string file) => AssetDatabase.LoadAssetAtPath<ItemData>($"{DataDir}/Items/{file}.asset");
        static CropData Crop(string n) => AssetDatabase.LoadAssetAtPath<CropData>($"{DataDir}/Crops/Crop_{n}.asset");

        static void CreateConfig()
        {
            if (AssetDatabase.LoadAssetAtPath<ProgressionConfig>(ConfigPath) != null) return;   // keep designer edits
            var c = ScriptableObject.CreateInstance<ProgressionConfig>();
            RewardDefinition R(RewardType t, int n, string id = null, ItemData item = null, string desc = null) =>
                new RewardDefinition { type = t, amount = n, targetId = id, item = item, description = desc };
            c.levels = new List<LevelDefinition>
            {
                new LevelDefinition { level = 1, starsRequired = 0, title = "Sprouting Farm" },
                new LevelDefinition { level = 2, starsRequired = 5, title = "Budding Farm", rewards = { R(RewardType.UnlockBed, 0, "tomato"), R(RewardType.Coins, 20) } },
                new LevelDefinition { level = 3, starsRequired = 15, title = "Growing Farm", rewards = { R(RewardType.UnlockBed, 0, "wheat"), R(RewardType.Coins, 40) } },
                new LevelDefinition { level = 4, starsRequired = 30, title = "Thriving Farm", rewards = { R(RewardType.WateringCanCapacity, 3), R(RewardType.Coins, 60), R(RewardType.Message, 0, desc: "The greenhouse can be restored") } },
                new LevelDefinition { level = 5, starsRequired = 50, title = "Flourishing Farm", rewards = { R(RewardType.BarnCapacity, 20), R(RewardType.Coins, 100), R(RewardType.GiveItem, 5, item: Item("Seed_Tomato")) } },
                new LevelDefinition { level = 6, starsRequired = 80, title = "Bountiful Farm", rewards = { R(RewardType.Coins, 150), R(RewardType.WateringCanCapacity, 5) } },
                new LevelDefinition { level = 7, starsRequired = 120, title = "Heart of the Valley", rewards = { R(RewardType.Coins, 250), R(RewardType.BarnCapacity, 30) } },
            };
            AchievementDefinition A(string id, string t, string d, string stat, int th, int stars, int coins = 0) =>
                new AchievementDefinition { id = id, title = t, description = d, statKey = stat, threshold = th, stars = stars, coins = coins };
            c.achievements = new List<AchievementDefinition>
            {
                A("first_harvest", "First Harvest", "Harvest your very first crop", Stats.Harvests, 1, 2),
                A("green_thumb", "Green Thumb", "Harvest 25 crops", Stats.Harvests, 25, 3, 20),
                A("variety", "Variety Grower", "Harvest carrots, tomatoes and wheat", Stats.KindsHarvested, 3, 3),
                A("water_bearer", "Water Bearer", "Water crops 30 times", Stats.Waterings, 30, 2),
                A("market_day", "Market Day", "Sell your first produce", Stats.ItemsSold, 1, 2),
                A("merchant", "Merchant", "Earn 200 coins at the market", Stats.CoinsFromSales, 200, 4, 25),
                A("well_stocked", "Well Stocked", "Store 20 items in the barn", Stats.ItemsStored, 20, 2),
                A("growing_fields", "Growing Fields", "Unlock all three crop beds", Stats.BedsUnlocked, 3, 3),
                A("under_glass", "Under Glass", "Restore the greenhouse", Stats.GreenhouseUnlocked, 1, 3),
                A("heart_awakens", "Heart Awakens", "Begin restoring the Heart of Mossvale", Stats.HeartStage, 1, 3),
            };
            c.milestones = new List<MilestoneTrack>
            {
                new MilestoneTrack { id = "ms_carrot", title = "Carrot harvests", crop = Crop("Carrot"), statKey = Stats.HarvestOf("carrot") },
                new MilestoneTrack { id = "ms_tomato", title = "Tomato harvests", crop = Crop("Tomato"), statKey = Stats.HarvestOf("tomato") },
                new MilestoneTrack { id = "ms_wheat", title = "Wheat harvests", crop = Crop("Wheat"), statKey = Stats.HarvestOf("wheat") },
            };
            DailyObjectiveDefinition D(string id, string t, string stat, int n, int stars = 2, int coins = 15) =>
                new DailyObjectiveDefinition { id = id, title = t, statKey = stat, amount = n, rewardStars = stars, rewardCoins = coins };
            c.dailyPool = new List<DailyObjectiveDefinition>
            {
                D("d_harvest5", "Harvest 5 crops", Stats.Harvests, 5),
                D("d_water8", "Water crops 8 times", Stats.Waterings, 8),
                D("d_till6", "Till 6 plots", Stats.Tills, 6, 1, 10),
                D("d_plant6", "Plant 6 seeds", Stats.Plantings, 6, 1, 10),
                D("d_sell5", "Sell 5 crops at the market", Stats.ItemsSold, 5, 2, 20),
                D("d_carrots3", "Harvest 3 carrots", Stats.HarvestOf("carrot"), 3),
                D("d_store5", "Store 5 items in the barn", Stats.ItemsStored, 5, 1, 10),
            };
            c.upgrades = new List<UpgradeDefinition>
            {
                new UpgradeDefinition { id = "bigger_can", title = "Bigger Watering Can", description = "Carry 5 more waterings before refilling", cost = 40, requiredLevel = 2, effect = UpgradeEffect.WateringCanCapacity, amount = 5 },
                new UpgradeDefinition { id = "barn_expansion", title = "Barn Expansion", description = "Add 30 storage spaces to the barn", cost = 60, requiredLevel = 3, effect = UpgradeEffect.BarnCapacity, amount = 30 },
                new UpgradeDefinition { id = "fertile_soil", title = "Fertile Soil", description = "All crops grow 20% faster", cost = 120, requiredLevel = 4, effect = UpgradeEffect.GrowthSpeedPercent, amount = 20 },
            };
            c.heartStages = new List<HeartStageDefinition>
            {
                new HeartStageDefinition { title = "Withered Sapling", description = "An ancient tree sleeps at the heart of the valley. Its leaves are grey and dry - but something still glows deep inside.", starCost = 0, requiredLevel = 1 },
                new HeartStageDefinition { title = "Awakening", description = "Fresh pink blossoms open and the first wildflowers return around its roots.", starCost = 5, requiredLevel = 1 },
                new HeartStageDefinition { title = "Blooming", description = "The tree grows tall and full. Mushrooms and flowering bushes gather around it.", starCost = 12, requiredLevel = 2 },
                new HeartStageDefinition { title = "Radiant", description = "A blue heart-crystal rises from the roots and fireflies drift through the branches.", starCost = 25, requiredLevel = 3 },
                new HeartStageDefinition { title = "Heart of Mossvale", description = "Standing stones circle the glowing tree. The valley is whole again.", starCost = 40, requiredLevel = 5 },
            };
            AssetDatabase.CreateAsset(c, ConfigPath);
        }

        // ================================================================== scene
        static readonly string[] Roots = { "Progression", "Progression_HUD", "Heart_of_Mossvale" };
        static readonly (string obj, string bedId, string name)[] Beds = { ("FarmBed_Carrot", "carrot", "Carrot"), ("FarmBed_Tomato", "tomato", "Tomato"), ("FarmBed_Wheat", "wheat", "Wheat") };

        /// <summary>Wires progression into the currently open Mossvale_Main scene (does not save).</summary>
        public static void ApplyToOpenScene()
        {
            var scene = SceneManager.GetActiveScene();
            var style = AssetDatabase.LoadAssetAtPath<UIStyle>(StylePath);
            var config = AssetDatabase.LoadAssetAtPath<ProgressionConfig>(ConfigPath);
            var db = AssetDatabase.LoadAssetAtPath<ItemDatabase>(DbPath);
            if (style == null || config == null) throw new Exception("Progression assets missing - run CreateAssets first");
            var fxMat = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/FX_Particles.mat");

            Transform Find(string n) => scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == n);

            // --- clean previous run
            foreach (var n in Roots) { var t = Find(n); if (t) Object.DestroyImmediate(t.gameObject); }
            foreach (var t in new[] { typeof(BedLock), typeof(Building) })
                foreach (var c in Object.FindObjectsByType(t, FindObjectsInactive.Include, FindObjectsSortMode.None)) Object.DestroyImmediate(c);
            foreach (var b in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<WorldBadge>(true)).ToList()) Object.DestroyImmediate(b.gameObject);
            foreach (var fxName in new[] { "FX_Unlock", "FX_HeartRestore" })
                foreach (var t in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).Where(t => t.name == fxName).ToList()) Object.DestroyImmediate(t.gameObject);
            var barnGo = Find("Barn_01");
            if (barnGo) foreach (var inv in barnGo.GetComponents<Inventory>()) Object.DestroyImmediate(inv);

            var player = Find("Player");
            var playerInv = player.GetComponent<Inventory>();
            var farming = Find("Farming");
            if (farming == null) throw new Exception("Run the farming setup first (no 'Farming' object)");
            MossvaleFarmingSetup.Set(farming.GetComponent<FarmManager>(), "database", db);

            // --- progression systems
            var prog = new GameObject("Progression");
            var pm = prog.AddComponent<FarmProgressionManager>();
            MossvaleFarmingSetup.Set(pm, "config", config);
            MossvaleFarmingSetup.Set(pm, "playerInventory", playerInv);
            prog.AddComponent<Wallet>();
            prog.AddComponent<SaveManager>();

            // --- FX prefabs
            var unlockFx = MossvaleFarmingSetup.MakeFx("FX_Unlock", fxMat, new Color(1f, 0.85f, 0.35f), 50, 3.2f, 0.16f, 1.2f, 0.6f, ParticleSystemShapeType.Box, 1.8f, false, 0);
            var heartFx = MossvaleFarmingSetup.MakeFx("FX_HeartRestore", fxMat, new Color(0.55f, 0.82f, 1f), 70, 4.5f, 0.2f, 1.5f, -0.25f, ParticleSystemShapeType.Cone, 1.0f, false, 0);
            var motesFx = MossvaleFarmingSetup.MakeFx("FX_HeartMotes", fxMat, new Color(0.7f, 0.9f, 1f), 0, 0.25f, 0.11f, 3.2f, -0.04f, ParticleSystemShapeType.Box, 1.8f, true, 7f);

            // --- HUD canvas with panels
            var canvasGo = new GameObject("Progression_HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            var hud = canvasGo.AddComponent<ProgressionHUD>();
            MossvaleFarmingSetup.Set(hud, "style", style);
            T Panel<T>(string name, string title, Sprite icon) where T : UIPanel
            {
                var go = new GameObject(name, typeof(RectTransform));
                go.transform.SetParent(canvasGo.transform, false);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
                var p = go.AddComponent<T>();
                MossvaleFarmingSetup.Set(p, "style", style);
                MossvaleFarmingSetup.Set(p, "title", title);
                MossvaleFarmingSetup.Set(p, "titleIcon", icon);
                return p;
            }
            var farmhousePanel = Panel<FarmhousePanel>("FarmhousePanel", "Farmhouse", style.star);
            var barnPanel = Panel<BarnPanel>("BarnPanel", "Barn", Item("Tool_Basket")?.icon);
            var marketPanel = Panel<MarketPanel>("MarketPanel", "Market Stall", style.coin);
            var greenhousePanel = Panel<GreenhousePanel>("GreenhousePanel", "Greenhouse", Item("Seed_Tomato")?.icon);
            var heartPanel = Panel<HeartPanel>("HeartPanel", "Heart of Mossvale", style.heart);

            // --- buildings
            void Wire(Building b, string display, string action, float range, UIPanel panel)
            {
                MossvaleFarmingSetup.Set(b, "displayName", display);
                MossvaleFarmingSetup.Set(b, "actionLabel", action);
                MossvaleFarmingSetup.Set(b, "interactionRange", range);
                MossvaleFarmingSetup.Set(b, "panel", panel);
                MossvaleFarmingSetup.Set(b, "playerInventory", playerInv);
            }
            var cottage = Require(Find("Cottage_01"));
            Wire(cottage.gameObject.AddComponent<Building>(), "Farmhouse", "Enter the farmhouse", 4.0f, farmhousePanel);

            var barn = Require(barnGo);
            var storage = barn.gameObject.AddComponent<Inventory>();
            MossvaleFarmingSetup.Set(storage, "saveId", "barn_storage");
            var barnComp = barn.gameObject.AddComponent<BarnStorage>();
            Wire(barnComp, "Barn", "Open barn storage", 4.0f, barnPanel);
            MossvaleFarmingSetup.Set(barnComp, "storage", storage);

            var market = Require(Find("Market_Stall_01"));
            Wire(market.gameObject.AddComponent<MarketStall>(), "Market Stall", "Visit the market", 3.2f, marketPanel);

            var gh = Require(Find("Greenhouse_01"));
            var ghComp = gh.gameObject.AddComponent<GreenhouseBuilding>();
            Wire(ghComp, "Greenhouse", "Visit the greenhouse", 3.5f, greenhousePanel);
            var ghBounds = Bounds(gh.gameObject);
            MossvaleFarmingSetup.Set(ghComp, "badge", Badge(style, gh, new Vector3(ghBounds.center.x, ghBounds.max.y + 0.9f, ghBounds.center.z), style.lockIcon, "Greenhouse"));
            MossvaleFarmingSetup.Set(ghComp, "unlockFx", Fx(unlockFx, gh, ghBounds.center));

            // --- bed locks
            foreach (var (obj, id, name) in Beds)
            {
                var bed = Find(obj);
                if (bed == null) { Debug.LogWarning(Tag + "Missing bed " + obj); continue; }
                var bl = bed.gameObject.AddComponent<BedLock>();
                MossvaleFarmingSetup.Set(bl, "bedId", id);
                MossvaleFarmingSetup.Set(bl, "displayName", name);
                var bb = Bounds(bed.gameObject);
                MossvaleFarmingSetup.Set(bl, "badge", Badge(style, bed, new Vector3(bb.center.x, bb.max.y + 1.4f, bb.center.z), style.lockIcon, name + " bed"));
                MossvaleFarmingSetup.Set(bl, "unlockFx", Fx(unlockFx, bed, bb.center + Vector3.up * 0.4f));
            }

            // --- Heart of Mossvale
            BuildHeart(scene, style, heartPanel, playerInv, heartFx, motesFx);

            Debug.Log(Tag + "Scene wired: progression, 4 buildings, 3 bed locks, Heart of Mossvale");
        }

        static Transform Require(Transform t) => t != null ? t : throw new Exception("Required scene object missing");
        static Transform Require(GameObject g) => g != null ? g.transform : throw new Exception("Required scene object missing");

        static Bounds Bounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>().Where(r => !(r is ParticleSystemRenderer)).ToArray();
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        static ParticleSystem Fx(GameObject prefab, Transform parent, Vector3 worldPos)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.name = prefab.name;
            go.transform.SetParent(parent, true);
            go.transform.position = worldPos;
            return go.GetComponent<ParticleSystem>();
        }

        /// <summary>World-space wooden sign with icon + text that faces the camera.</summary>
        static WorldBadge Badge(UIStyle style, Transform parent, Vector3 worldPos, Sprite icon, string text)
        {
            var go = new GameObject("Badge_" + parent.name, typeof(RectTransform), typeof(Canvas));
            go.transform.SetParent(parent, true);
            go.transform.position = worldPos;
            go.transform.localScale = Vector3.one * 0.0075f / Mathf.Max(0.001f, parent.lossyScale.x);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(460, 120);
            UIBuilder.Image(rt, style.wood, Color.white);
            var inner = UIBuilder.Fill(rt, "Inner", 10, 10, 10, 10);
            UIBuilder.Image(inner, style.panel, style.cream);
            UIBuilder.Icon(inner, icon, 8, 10, 80);
            var label = UIBuilder.Label(inner, style, text, 24, style.text, 96, 0, 338, 100, TMPro.TextAlignmentOptions.Center);
            var badge = go.AddComponent<WorldBadge>();
            MossvaleFarmingSetup.Set(badge, "label", label);
            return badge;
        }

        // ------------------------------------------------------------------ Heart of Mossvale
        static void BuildHeart(Scene scene, UIStyle style, UIPanel panel, Inventory playerInv, GameObject restoreFx, GameObject motesFx)
        {
            var pos = FindHeartSpot(new Vector3(-9.5f, 0f, -0.8f));
            var root = new GameObject("Heart_of_Mossvale");
            root.transform.position = pos;
            var cap = root.AddComponent<CapsuleCollider>();
            cap.radius = 1.0f; cap.height = 4f; cap.center = new Vector3(0, 2f, 0);

            GameObject P(string path) => AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/Prefabs/{path}.prefab");
            GameObject M(string n) => AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/Models/Farming/{n}.fbx");
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var withered = MossvaleFarmingSetup.LitMat("MV_HeartWithered", lit, new Color32(0x8E, 0x82, 0x70, 255), 0.05f);
            var crystalMat = MossvaleFarmingSetup.LitMat("MV_HeartCrystal", lit, new Color32(0x3E, 0x92, 0xEA, 255), 0.85f);
            crystalMat.EnableKeyword("_EMISSION");
            crystalMat.SetColor("_EmissionColor", new Color(0.18f, 0.48f, 1f) * 0.75f);   // below the bloom threshold so it stays blue
            crystalMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(crystalMat);

            GameObject Put(Transform parent, GameObject prefab, Vector3 local, float scale, float yaw = 0f, Vector3? scale3 = null)
            {
                var g = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                g.transform.SetParent(parent, false);
                g.transform.localPosition = local;
                g.transform.localRotation = Quaternion.Euler(0, yaw, 0);
                g.transform.localScale = scale3 ?? Vector3.one * scale;
                foreach (var c in g.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                return g;
            }
            Vector3 Ring(float r, int i, int n, float phase = 0f) => new Vector3(Mathf.Cos(i * Mathf.PI * 2 / n + phase) * r, 0, Mathf.Sin(i * Mathf.PI * 2 / n + phase) * r);

            var visuals = new GameObject("Visuals").transform;
            visuals.SetParent(root.transform, false);
            var baseRing = new GameObject("Base").transform;
            baseRing.SetParent(visuals, false);
            for (int i = 0; i < 6; i++) Put(baseRing, P(i % 2 == 0 ? "Nature/Pebbles" : "Nature/Rock_Small"), Ring(1.25f, i, 6, 0.3f), 0.7f, i * 60);

            var flowers = new[] { "Nature/Flowers_Daisy", "Nature/Flowers_Tulip", "Nature/Flowers_Poppy", "Nature/Flowers_Lavender", "Nature/Flowers_Blue" };
            var stages = new List<GameObject>();
            for (int s = 0; s < 5; s++)
            {
                var st = new GameObject($"Stage_{s}").transform;
                st.SetParent(visuals, false);
                stages.Add(st.gameObject);
                if (s == 0)
                {
                    var tree = Put(st, P("Nature/Tree_Autumn"), Vector3.zero, 0.75f);
                    foreach (var r in tree.GetComponentsInChildren<Renderer>())
                        r.sharedMaterials = r.sharedMaterials.Select(m => m != null && m.name.Contains("Trunk") ? m : withered).ToArray();
                    for (int i = 0; i < 3; i++) Put(st, P("Nature/Grass_Tuft_B"), Ring(1.6f, i, 3), 0.6f, i * 90);
                    continue;
                }
                Put(st, P("Nature/Tree_Blossom"), Vector3.zero, s == 1 ? 0.6f : s == 2 ? 0.95f : 1.1f);
                int nf = s == 1 ? 4 : 8;
                for (int i = 0; i < nf; i++) Put(st, P(flowers[i % flowers.Length]), Ring(s == 1 ? 1.3f : 1.7f, i, nf), 0.8f, i * 45);
                if (s >= 2)
                {
                    Put(st, P("Nature/Mushrooms_Red"), Ring(1.1f, 0, 2, 0.8f), 0.9f);
                    Put(st, P("Nature/Mushrooms_Brown"), Ring(1.1f, 1, 2, 0.8f), 0.9f);
                    Put(st, P("Nature/Bush_Flowering"), Ring(2.3f, 0, 2, 1.9f), 0.7f);
                    Put(st, P("Nature/Bush_Flowering"), Ring(2.3f, 1, 2, 1.9f), 0.7f);
                }
                if (s >= 3)
                {
                    var front = new Vector3(0f, 0f, -1.9f);   // the follow camera looks north (+Z): put the crystal on the south side
                    Put(st, P("Nature/Rock_Medium"), front, 0.45f);
                    var crystal = Put(st, M("Water_Droplet"), front + Vector3.up * 1.25f, s == 3 ? 2.2f : 2.8f);
                    crystal.name = "Heart_Crystal";
                    foreach (var r in crystal.GetComponentsInChildren<Renderer>()) { r.sharedMaterial = crystalMat; r.shadowCastingMode = ShadowCastingMode.Off; }
                    var fl = crystal.AddComponent<MossvaleFloat>(); fl.amplitude = 0.12f; fl.speed = 1.3f; fl.spin = 40f;
                    var lightGo = new GameObject("Heart_Light");
                    lightGo.transform.SetParent(st, false);
                    lightGo.transform.localPosition = front + Vector3.up * 1.6f;
                    var l = lightGo.AddComponent<Light>();
                    l.type = LightType.Point; l.color = new Color(0.5f, 0.78f, 1f); l.range = s == 3 ? 6f : 9f; l.intensity = s == 3 ? 2.2f : 3.5f; l.shadows = LightShadows.None;
                    var motes = (GameObject)PrefabUtility.InstantiatePrefab(motesFx);
                    motes.transform.SetParent(st, false);
                    motes.transform.localPosition = Vector3.up * 2.2f;
                    var main = motes.GetComponent<ParticleSystem>().main;
                    main.playOnAwake = true;
                }
                if (s >= 4)
                    for (int i = 0; i < 6; i++) Put(st, P("Nature/Rock_Medium"), Ring(3.0f, i, 6, 0.25f), 1f, i * 50, new Vector3(0.45f, 1.35f, 0.45f));
                st.gameObject.SetActive(false);
            }
            stages[0].SetActive(true);

            var fx = (GameObject)PrefabUtility.InstantiatePrefab(restoreFx);
            fx.transform.SetParent(root.transform, false);
            fx.transform.localPosition = Vector3.up * 0.3f;
            var badge = Badge(style, root.transform, pos + Vector3.up * 5.4f, style.heart, "Heart of Mossvale");

            var heart = root.AddComponent<HeartOfMossvale>();
            MossvaleFarmingSetup.Set(heart, "displayName", "Heart of Mossvale");
            MossvaleFarmingSetup.Set(heart, "actionLabel", "Visit the Heart of Mossvale");
            MossvaleFarmingSetup.Set(heart, "interactionRange", 3.0f);
            MossvaleFarmingSetup.Set(heart, "panel", panel);
            MossvaleFarmingSetup.Set(heart, "playerInventory", playerInv);
            MossvaleFarmingSetup.Set(heart, "stageVisuals", stages.Cast<Object>().ToArray());
            MossvaleFarmingSetup.Set(heart, "restoreFx", fx.GetComponent<ParticleSystem>());
            MossvaleFarmingSetup.Set(heart, "badge", badge);
            Debug.Log(Tag + $"Heart of Mossvale placed at {pos}");
        }

        /// <summary>Finds a clear spot near the preferred position (no colliders within 3 m apart from the ground).</summary>
        static Vector3 FindHeartSpot(Vector3 preferred)
        {
            Physics.SyncTransforms();
            Vector3 best = preferred;
            float bestScore = float.MaxValue;
            for (float r = 0; r <= 6f; r += 0.5f)
                for (int i = 0; i < Mathf.Max(1, (int)(r * 6)); i++)
                {
                    float a = i * Mathf.PI * 2f / Mathf.Max(1, (int)(r * 6));
                    var p = preferred + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r);
                    var hits = Physics.OverlapSphere(p + Vector3.up * 1.2f, 3.0f, ~0, QueryTriggerInteraction.Ignore)
                        .Where(c => c.name != "Ground" && !c.name.StartsWith("Bound_")).ToList();
                    if (hits.Count > 0) continue;
                    if (r < bestScore) { bestScore = r; best = p; }
                }
            if (bestScore == float.MaxValue) Debug.LogWarning(Tag + "No fully clear Heart spot found; using the preferred position");
            return best;
        }
    }
}
