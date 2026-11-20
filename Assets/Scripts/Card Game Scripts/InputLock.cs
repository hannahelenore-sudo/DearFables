using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// One switch that makes EVERY button / clickable UI element unpressable while something is playing
/// (round intro, ending animation, tutorial steps ...).
///
///   InputLock.Push(this);   // start: nothing can be clicked or "Submit"-ed any more
///   InputLock.Pop(this);    // end: everything works again (only when nobody else still holds a lock)
///
/// Several scripts can lock at the same time; the buttons only come back when ALL of them have released.
/// It does not touch any button's own "Interactable" setting, so nothing can be left disabled by accident.
/// Put this file anywhere in Assets (it is a plain static class, nothing to add to a GameObject).
/// </summary>
public static class InputLock
{
    /// <summary>Sits above the whole game UI, below the tutorial overlay (32000).</summary>
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

    /// <summary>Lock all buttons. "owner" can be any object (usually "this" or a string).</summary>
    public static void Push(object owner)
    {
        if (owner == null) return;
        Hook();
        owners.Add(owner);
        Apply();
    }

    /// <summary>Release your lock. Buttons come back once nobody holds a lock any more.</summary>
    public static void Pop(object owner)
    {
        if (owner == null) return;
        if (!owners.Remove(owner)) return;
        if (owners.Count == 0) Remove();
    }

    /// <summary>Drops every lock at once (used when the card game scene closes).</summary>
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
            es.sendNavigationEvents = false;   // Space / Enter / arrows can't press a selected button
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

    // An invisible full-screen graphic: every click / tap lands on it instead of a button
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
