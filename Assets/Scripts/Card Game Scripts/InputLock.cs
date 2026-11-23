using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class InputLock
{
    public const int SortingOrder = 31000;

    private static readonly HashSet<object> owners = new HashSet<object>();
    private static GameObject blocker;
    private static bool prevNavigation = true;
    private static bool navigationSaved;
    private static bool hooked;

    public static bool IsLocked { get { return owners.Count > 0; } }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        owners.Clear();
        blocker = null;
        navigationSaved = false;
        prevNavigation = true;
        hooked = false;
    }

    public static void Push(object owner)
    {
        if (owner == null) return;
        Hook();
        owners.Add(owner);
        Apply();
    }

    public static void Pop(object owner)
    {
        if (owner == null) return;
        if (!owners.Remove(owner)) return;
        if (owners.Count == 0) Remove();
    }

    public static void ReleaseAll()
    {
        owners.Clear();
        Remove();
    }

    private static void Apply()
    {
        if (blocker == null) blocker = CreateBlocker();
        blocker.SetActive(true);

        EventSystem es = EventSystem.current;
        if (es != null)
        {
            if (!navigationSaved)
            {
                prevNavigation = es.sendNavigationEvents;
                navigationSaved = true;
            }
            es.sendNavigationEvents = false;
            es.SetSelectedGameObject(null);
        }
    }

    private static void Remove()
    {
        if (blocker != null) blocker.SetActive(false);

        EventSystem es = EventSystem.current;
        if (es != null && navigationSaved) es.sendNavigationEvents = prevNavigation;
        navigationSaved = false;
    }

    private static GameObject CreateBlocker()
    {
        GameObject go = new GameObject("InputLockBlocker", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        Canvas canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;

        GameObject block = new GameObject("Block", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        block.transform.SetParent(go.transform, false);

        RectTransform rt = block.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image img = block.GetComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0f);
        img.raycastTarget = true;

        return go;
    }

    private static void Hook()
    {
        if (hooked) return;
        hooked = true;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneUnloaded(Scene scene)
    {
        if (scene.name == CardGameTutorialGate.CardGameSceneName) ReleaseAll();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single) ReleaseAll();
    }
}