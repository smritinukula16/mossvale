using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Mossvale.Farming
{
    /// <summary>
    /// JSON save/load for every ISaveable in the scene (inventory, coins, plots, progression, barn, heart...).
    /// Loads on Start, saves shortly after changes (MarkDirty), every autosaveInterval seconds and on quit.
    /// File: Application.persistentDataPath/&lt;slot&gt;.json
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class SaveManager : MonoBehaviour
    {
        public static SaveManager Instance { get; private set; }
        /// <summary>Tests set this to use a separate save file.</summary>
        public static string SlotOverride { get; set; }
        public static bool IsRestoring { get; private set; }

        [SerializeField] string slot = "mossvale_save";
        [SerializeField] float autosaveInterval = 20f;
        [SerializeField] bool loadOnStart = true;

        static bool dirty;
        float dirtySince, lastSave;

        [Serializable] class Entry { public string key; public string json; }
        [Serializable] class SaveFile { public int version = 1; public string savedAt; public List<Entry> entries = new List<Entry>(); }

        public string Slot => string.IsNullOrEmpty(SlotOverride) ? slot : SlotOverride;
        public string FilePath => PathFor(Slot);
        public bool LoadedFromSave { get; private set; }
        public event Action Saved;
        public event Action Loaded;

        public static string PathFor(string slotName) => Path.Combine(Application.persistentDataPath, slotName + ".json");

        void Awake() => Instance = this;
        void OnDestroy() { if (Instance == this) Instance = null; }

        void Start()
        {
            if (loadOnStart) Load();
            lastSave = Time.unscaledTime;
            dirty = false;
        }

        void Update()
        {
            if (dirty && Time.unscaledTime - dirtySince > 1.5f) SaveNow();
            else if (Time.unscaledTime - lastSave > autosaveInterval) SaveNow();
        }

        void OnApplicationQuit() => SaveNow();
        void OnApplicationPause(bool paused) { if (paused) SaveNow(); }

        /// <summary>Request a save soon (debounced).</summary>
        public static void MarkDirty()
        {
            if (IsRestoring || Instance == null) return;
            if (!dirty) Instance.dirtySince = Time.unscaledTime;
            dirty = true;
        }

        static IEnumerable<ISaveable> Saveables() =>
            FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .OfType<ISaveable>().OrderBy(s => s.RestoreOrder);

        public void SaveNow()
        {
            var file = new SaveFile { savedAt = DateTime.Now.ToString("s") };
            foreach (var s in Saveables())
            {
                try { file.entries.Add(new Entry { key = s.SaveKey, json = s.CaptureState() }); }
                catch (Exception e) { Debug.LogError($"[Mossvale] Save failed for {s.SaveKey}: {e}"); }
            }
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                string tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, JsonUtility.ToJson(file, true));
                if (File.Exists(FilePath)) File.Delete(FilePath);
                File.Move(tmp, FilePath);
            }
            catch (Exception e) { Debug.LogError("[Mossvale] Could not write save: " + e); return; }
            dirty = false;
            lastSave = Time.unscaledTime;
            Saved?.Invoke();
        }

        public bool Load()
        {
            LoadedFromSave = false;
            if (!File.Exists(FilePath)) return false;
            SaveFile file;
            try { file = JsonUtility.FromJson<SaveFile>(File.ReadAllText(FilePath)); }
            catch (Exception e) { Debug.LogError("[Mossvale] Corrupt save ignored: " + e); return false; }
            if (file == null) return false;
            var map = new Dictionary<string, string>();
            foreach (var e in file.entries) if (e != null && e.key != null) map[e.key] = e.json;
            IsRestoring = true;
            try
            {
                foreach (var s in Saveables())
                    if (map.TryGetValue(s.SaveKey, out var json))
                    {
                        try { s.RestoreState(json); }
                        catch (Exception e) { Debug.LogError($"[Mossvale] Restore failed for {s.SaveKey}: {e}"); }
                    }
            }
            finally { IsRestoring = false; }
            LoadedFromSave = true;
            dirty = false;
            Loaded?.Invoke();
            Debug.Log($"[Mossvale] Loaded save ({file.entries.Count} entries, saved {file.savedAt})");
            return true;
        }

        public static void DeleteSlot(string slotName)
        {
            var p = PathFor(slotName);
            if (File.Exists(p)) File.Delete(p);
        }

        public void DeleteSave() => DeleteSlot(Slot);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { dirty = false; IsRestoring = false; Instance = null; }
    }
}
