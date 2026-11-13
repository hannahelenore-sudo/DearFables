using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

[Serializable]
public class SaveEntry
{
    public string key;
    public string kind;      // simple / list / dict
    public string value;
    public string[] items;
    public string[] keys;
}

[Serializable]
public class SaveFile
{
    public int version = 1;
    public string scene;
    public string savedAt;
    public string summary;
    public bool hasPlayer;
    public float px, py, pz;
    public List<SaveEntry> entries = new List<SaveEntry>();
    public List<QuestLog.Quest> quests = new List<QuestLog.Quest>();
}

// Tiny hidden object that lets the static SaveSystem run a coroutine after a scene loads
public class SaveRunner : MonoBehaviour { }

/// <summary>
/// Save / load to 3 slots (JSON files in Application.persistentDataPath).
///
/// What is saved:
///  - Every static field of PlayerProgress, BoonProgress, MoodManager, StructureProgress and TrinketInventory
///    (numbers, bools, text, enums, and Lists / HashSets / Dictionaries of those; ScriptableObject lists such as owned trinkets by asset name)
///  - The quest log
///  - The player's position in the overworld
/// Saving is only allowed in the scenes listed in SaveScenes (not in the middle of a card fight).
/// To add another class's static data, add it to SavedTypes below.
///
/// From anywhere (e.g. a Continue button in the main menu):  SaveSystem.Load(1);
/// </summary>
public static class SaveSystem
{
    public const int SlotCount = 3;

    /// <summary>Scenes where saving is allowed.</summary>
    public static string[] SaveScenes = { "GameScene" };
    /// <summary>Name of the player GameObject whose position is saved.</summary>
    public static string PlayerObjectName = "Player";

    private static readonly Type[] SavedTypes =
    {
        typeof(PlayerProgress),
        typeof(BoonProgress),
        typeof(MoodManager),
        typeof(StructureProgress),
        typeof(TrinketInventory),
    };

    private static SaveFile pending;
    private static SaveFile fresh; // the untouched starting values, used by New Game
    private static SaveRunner runner;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        pending = null;
        runner = null;
        fresh = null;
    }

    // Remember what a brand new game looks like, before anything has been played
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void TakeFreshSnapshot()
    {
        if (fresh != null) return;
        try
        {
            fresh = new SaveFile();
            foreach (KeyValuePair<string, FieldInfo> kv in Fields())
            {
                SaveEntry e = Capture(kv.Key, kv.Value);
                if (e != null) fresh.entries.Add(e);
            }
        }
        catch (Exception ex)
        {
            fresh = null;
            Debug.LogWarning("SaveSystem: could not take the starting snapshot: " + ex.Message);
        }
    }

    /// <summary>Puts all saved data (stars, boons, trinkets, quests...) back to a brand new game.</summary>
    public static void ResetProgress()
    {
        if (fresh != null) ApplyEntries(fresh);
        QuestLog.ReplaceAll(null);
    }

    // ------------------------------------------------------------------ public API

    public static bool CanSaveHere
    {
        get
        {
            string current = SceneManager.GetActiveScene().name;
            for (int i = 0; i < SaveScenes.Length; i++)
                if (SaveScenes[i] == current) return true;
            return false;
        }
    }

    public static bool HasSave(int slot) { return File.Exists(PathFor(slot)); }

    /// <summary>Header info for a slot (scene, date, summary), or null when empty.</summary>
    public static SaveFile GetInfo(int slot) { return Read(slot); }

    public static bool Save(int slot)
    {
        if (!CanSaveHere) return false;

        try
        {
            SaveFile f = new SaveFile();
            f.scene = SceneManager.GetActiveScene().name;
            f.savedAt = DateTime.Now.ToString("d MMM yyyy  HH:mm", CultureInfo.InvariantCulture);
            f.summary = Summary();

            List<string> skipped = new List<string>();
            foreach (KeyValuePair<string, FieldInfo> kv in Fields())
            {
                SaveEntry e = Capture(kv.Key, kv.Value);
                if (e != null) f.entries.Add(e); else skipped.Add(kv.Key);
            }
            if (skipped.Count > 0)
                Debug.Log("SaveSystem: not saved (unsupported type or read-only): " + string.Join(", ", skipped.ToArray()));

            foreach (QuestLog.Quest q in QuestLog.All) f.quests.Add(q);

            GameObject player = GameObject.Find(PlayerObjectName);
            if (player != null)
            {
                f.hasPlayer = true;
                Vector3 p = player.transform.position;
                f.px = p.x; f.py = p.y; f.pz = p.z;
            }

            File.WriteAllText(PathFor(slot), JsonUtility.ToJson(f, true));
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("SaveSystem: save failed: " + ex.Message);
            return false;
        }
    }

    public static bool Load(int slot)
    {
        SaveFile f = Read(slot);
        if (f == null || string.IsNullOrEmpty(f.scene)) return false;

        ResetProgress(); // anything the save does not mention goes back to its starting value
        ApplyEntries(f);
        QuestLog.ReplaceAll(f.quests);

        pending = f;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.LoadScene(f.scene);
        return true;
    }

    // ------------------------------------------------------------------ after the scene is loaded

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (pending == null) return;
        SceneManager.sceneLoaded -= OnSceneLoaded;

        SaveFile f = pending;
        pending = null;
        if (scene.name == f.scene && f.hasPlayer) Run(ApplyPosition(f));
    }

    private static IEnumerator ApplyPosition(SaveFile f)
    {
        // a few frames, so the scene's own Start() code does not move the player afterwards
        for (int i = 0; i < 3; i++) yield return null;
        MovePlayer(f);
        yield return new WaitForSecondsRealtime(0.3f);
        MovePlayer(f);
    }

    private static void MovePlayer(SaveFile f)
    {
        GameObject player = GameObject.Find(PlayerObjectName);
        if (player == null) return;

        Vector3 pos = new Vector3(f.px, f.py, f.pz);
        player.transform.position = pos;
        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
        if (rb != null) rb.position = new Vector2(pos.x, pos.y);
    }

    private static void Run(IEnumerator routine)
    {
        if (runner == null)
        {
            GameObject go = new GameObject("SaveRunner");
            UnityEngine.Object.DontDestroyOnLoad(go);
            runner = go.AddComponent<SaveRunner>();
        }
        runner.StartCoroutine(routine);
    }

    // ------------------------------------------------------------------ files

    private static string PathFor(int slot)
    {
        return Path.Combine(Application.persistentDataPath, "save_slot_" + slot + ".json");
    }

    private static SaveFile Read(int slot)
    {
        try
        {
            string path = PathFor(slot);
            if (!File.Exists(path)) return null;
            return JsonUtility.FromJson<SaveFile>(File.ReadAllText(path));
        }
        catch (Exception ex)
        {
            Debug.LogWarning("SaveSystem: could not read slot " + slot + ": " + ex.Message);
            return null;
        }
    }

    private static string Summary()
    {
        Type t = typeof(PlayerProgress);
        BindingFlags f = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        string[] names = { "Stars", "stars", "TotalStars", "totalStars", "StarCount", "starCount" };
        foreach (string n in names)
        {
            PropertyInfo p = t.GetProperty(n, f);
            if (p != null && p.CanRead) return "Stars: " + p.GetValue(null, null);
            FieldInfo fi = t.GetField(n, f);
            if (fi != null) return "Stars: " + fi.GetValue(null);
        }
        return "";
    }

    // ------------------------------------------------------------------ reflection over static data

    private static IEnumerable<KeyValuePair<string, FieldInfo>> Fields()
    {
        BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        foreach (Type t in SavedTypes)
        {
            foreach (FieldInfo fi in t.GetFields(flags))
            {
                if (fi.IsLiteral || fi.IsNotSerialized) continue;
                yield return new KeyValuePair<string, FieldInfo>(t.FullName + "." + fi.Name, fi);
            }
        }
    }

    private static bool IsSimple(Type t) { return t.IsPrimitive || t == typeof(string) || t.IsEnum; }
    private static bool IsAsset(Type t) { return typeof(ScriptableObject).IsAssignableFrom(t); }
    private static bool IsStorable(Type t) { return IsSimple(t) || IsAsset(t); }

    private static SaveEntry Capture(string key, FieldInfo fi)
    {
        Type ft = fi.FieldType;
        object v = fi.GetValue(null);

        if (IsSimple(ft))
        {
            if (fi.IsInitOnly) return null;
            return new SaveEntry { key = key, kind = "simple", value = Encode(v) };
        }

        if (!ft.IsGenericType) return null;
        Type def = ft.GetGenericTypeDefinition();
        Type[] args = ft.GetGenericArguments();

        if ((def == typeof(List<>) || def == typeof(HashSet<>)) && IsStorable(args[0]))
        {
            List<string> items = new List<string>();
            IEnumerable en = v as IEnumerable;
            if (en != null)
                foreach (object item in en)
                    if (item != null) items.Add(Encode(item));
            return new SaveEntry { key = key, kind = "list", items = items.ToArray() };
        }

        if (def == typeof(Dictionary<,>) && IsSimple(args[0]) && IsSimple(args[1]))
        {
            List<string> keys = new List<string>();
            List<string> vals = new List<string>();
            IDictionary d = v as IDictionary;
            if (d != null)
                foreach (DictionaryEntry de in d)
                {
                    keys.Add(Encode(de.Key));
                    vals.Add(Encode(de.Value));
                }
            return new SaveEntry { key = key, kind = "dict", keys = keys.ToArray(), items = vals.ToArray() };
        }

        return null;
    }

    private static void ApplyEntries(SaveFile f)
    {
        Dictionary<string, SaveEntry> byKey = new Dictionary<string, SaveEntry>();
        foreach (SaveEntry e in f.entries) byKey[e.key] = e;

        foreach (KeyValuePair<string, FieldInfo> kv in Fields())
        {
            SaveEntry e;
            if (!byKey.TryGetValue(kv.Key, out e)) continue;

            try { Apply(kv.Value, e); }
            catch (Exception ex) { Debug.LogWarning("SaveSystem: could not restore " + kv.Key + ": " + ex.Message); }
        }
    }

    private static void Apply(FieldInfo fi, SaveEntry e)
    {
        Type ft = fi.FieldType;

        if (e.kind == "simple")
        {
            if (!fi.IsInitOnly) fi.SetValue(null, Decode(ft, e.value));
            return;
        }

        object target = fi.GetValue(null);
        if (target == null)
        {
            if (fi.IsInitOnly) return;
            target = Activator.CreateInstance(ft);
            fi.SetValue(null, target);
        }

        Type[] args = ft.GetGenericArguments();
        MethodInfo clear = ft.GetMethod("Clear");
        MethodInfo add = ft.GetMethod("Add");
        clear.Invoke(target, null);

        if (e.kind == "list")
        {
            Type elem = args[0];
            foreach (string s in e.items)
            {
                object item = IsAsset(elem) ? FindAsset(elem, s) : Decode(elem, s);
                if (item != null) add.Invoke(target, new object[] { item });
            }
        }
        else if (e.kind == "dict")
        {
            for (int i = 0; i < e.keys.Length; i++)
                add.Invoke(target, new object[] { Decode(args[0], e.keys[i]), Decode(args[1], e.items[i]) });
        }
    }

    private static UnityEngine.Object FindAsset(Type type, string assetName)
    {
        foreach (UnityEngine.Object o in Resources.FindObjectsOfTypeAll(type))
            if (o.name == assetName) return o;
        foreach (UnityEngine.Object o in Resources.LoadAll("", type))
            if (o.name == assetName) return o;

        Debug.LogWarning("SaveSystem: could not find " + type.Name + " asset '" + assetName + "' to restore.");
        return null;
    }

    private static string Encode(object v)
    {
        if (v == null) return "";
        UnityEngine.Object uo = v as UnityEngine.Object;
        if (uo != null) return uo.name;
        if (v is float) return ((float)v).ToString("R", CultureInfo.InvariantCulture);
        if (v is double) return ((double)v).ToString("R", CultureInfo.InvariantCulture);
        if (v is Enum) return v.ToString();
        return Convert.ToString(v, CultureInfo.InvariantCulture);
    }

    private static object Decode(Type t, string s)
    {
        if (t.IsEnum) return Enum.Parse(t, s);
        if (t == typeof(string)) return s;
        if (t == typeof(bool)) return bool.Parse(s);
        return Convert.ChangeType(s, t, CultureInfo.InvariantCulture);
    }
}