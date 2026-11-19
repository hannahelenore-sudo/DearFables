using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// An NPC the player talks to. The player has to walk close to it; while they are close a
/// "Press E to Interact" prompt shows at the bottom of the screen, and pressing E starts the cutscene.
/// Standing too far away does nothing (clicks included).
///
/// The distance is measured in the WORLD between this object and the object tagged "Player".
/// If your NPC is a UI Image on a Canvas, its position is not a world position, so put an empty
/// object where the NPC stands in the world and drag it into "Distance From".
/// </summary>
public class NPCTrigger : MonoBehaviour, IPointerClickHandler
{
    [Header("Cutscene")]
    [Tooltip("The cutscene manager (the IntroCutscene in your scene).")]
    public IntroCutscene cutscene;
    [Tooltip("Which dialogue to play: the Id of an entry in the manager's Cutscenes list. Leave EMPTY to play the manager's Default Dialogue.")]
    public string cutsceneId = "";
    public bool playOnlyOnce = true;

    [Header("Interaction")]
    [Tooltip("Drag your Player object in here. If left empty, the object tagged 'Player' is used.")]
    public Transform player;
    [Tooltip("How close the player has to be (world units) to see the prompt and talk.")]
    public float interactDistance = 2.5f;
    [Tooltip("Optional. Measure the distance from this object instead of the NPC itself (useful when the NPC is a UI Image).")]
    public Transform distanceFrom;
    [Tooltip("ON: measure only along the ground (ignore up/down). Fixes the prompt not showing when the NPC's pivot sits higher or lower than the player's feet.")]
    public bool ignoreHeight = true;
    [Tooltip("Also allow clicking the NPC with the mouse, but only while the player is close enough.")]
    public bool allowMouseClick = false;

    [Header("Debug")]
    [Tooltip("Writes what the script is seeing to the Console (is the player found, how far away, is the prompt allowed).")]
    public bool debugLogs = false;

    [Header("Prompt")]
    public string promptText = "Press E to Interact";
    [Tooltip("Optional. Your own prompt object (e.g. a panel with text at the bottom of your HUD). It is shown/hidden for you. Leave empty to use an automatic text at the bottom of the screen.")]
    public GameObject promptObject;
    [Tooltip("Automatic prompt only: distance from the bottom edge of the screen.")]
    public float promptBottomOffset = 80f;
    [Tooltip("Automatic prompt only.")]
    public float promptFontSize = 36f;
    [Tooltip("Automatic prompt only: drag in your font file (.ttf / .otf) from the Project window. The TextMeshPro font is generated for you when the game runs.")]
    public Font promptFontFile;
    [Tooltip("Optional, instead of the font file: a TextMeshPro Font Asset you already made.")]
    public TMP_FontAsset promptFont;
    [Tooltip("Automatic prompt only.")]
    public Color promptColor = Color.white;

    private bool hasPlayed;
    private bool warnedNoPlayer;
    private bool warnedNoCutscene;
    private float lastDistance;
    private float debugTimer;
    private bool lastShow;

    // One shared automatic prompt for every NPC in the scene
    private static GameObject sharedPrompt;
    private static TMP_Text sharedText;
    private static Text sharedLegacyText;
    private static NPCTrigger promptOwner;

    // -------------------------------------------------------------------
    // Editor: a new Id automatically gets an entry on the IntroCutscene
    // -------------------------------------------------------------------

#if UNITY_EDITOR
    private bool addPending;
    private double lastEditTime;

    protected virtual void OnValidate()
    {
        if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (cutscene == null || !HasMissingCutsceneIds()) return;

        // Wait until you stop typing so "S", "Sh", "Sho"... don't each become a cutscene
        lastEditTime = UnityEditor.EditorApplication.timeSinceStartup;
        if (!addPending)
        {
            addPending = true;
            UnityEditor.EditorApplication.update += AddEntryWhenIdle;
        }
    }

    private bool HasMissingCutsceneIds()
    {
        foreach (string id in AllCutsceneIds())
            if (!string.IsNullOrWhiteSpace(id) && !cutscene.HasCutscene(id)) return true;
        return false;
    }

    private void AddEntryWhenIdle()
    {
        if (this == null)
        {
            UnityEditor.EditorApplication.update -= AddEntryWhenIdle;
            return;
        }

        if (UnityEditor.EditorApplication.timeSinceStartup - lastEditTime < 1.0) return;

        UnityEditor.EditorApplication.update -= AddEntryWhenIdle;
        addPending = false;

        if (cutscene == null) return;

        UnityEditor.Undo.RecordObject(cutscene, "Add cutscenes");
        foreach (string id in AllCutsceneIds())
        {
            if (cutscene.EnsureCutscene(id))
            {
                UnityEditor.EditorUtility.SetDirty(cutscene);
                Debug.Log("NPCTrigger: added a new cutscene '" + id + "' to the IntroCutscene's Cutscenes list. Select the IntroCutscene to write its lines.", cutscene);
            }
        }
    }
#endif

    /// <summary>Every cutscene Id this NPC uses (so the editor can create the missing entries). Subclasses add theirs.</summary>
    protected virtual IEnumerable<string> AllCutsceneIds()
    {
        yield return cutsceneId;
    }

    /// <summary>The Id to play when the player presses E.</summary>
    protected virtual string CurrentCutsceneId()
    {
        return cutsceneId;
    }

    /// <summary>Whether the NPC can be talked to right now (hides the prompt when false).</summary>
    protected virtual bool CanTalk()
    {
        return !(playOnlyOnce && hasPlayed);
    }

    /// <summary>Called when a cutscene started from this NPC really begins.</summary>
    protected virtual void OnCutsceneStarted(string id) { }

    // -------------------------------------------------------------------
    // Unity
    // -------------------------------------------------------------------

    protected virtual void Awake()
    {
        if (promptObject != null) promptObject.SetActive(false);
    }

    protected virtual void Update()
    {
        bool canInteract = cutscene != null
                           && !cutscene.IsPlaying
                           && CanTalk();

        if (cutscene == null && !warnedNoCutscene)
        {
            warnedNoCutscene = true;
            Debug.LogWarning("NPCTrigger: no cutscene assigned, so the prompt will never show.", this);
        }

        bool near = IsPlayerNear();
        bool show = canInteract && near;

        if (debugLogs)
        {
            debugTimer += Time.unscaledDeltaTime;
            if (show != lastShow || debugTimer >= 1f)
            {
                debugTimer = 0f;
                Debug.Log("NPCTrigger '" + name + "': prompt " + (show ? "SHOWN" : "hidden")
                          + " | player found: " + (player != null)
                          + " | distance: " + lastDistance.ToString("F2") + " (needs <= " + interactDistance + ")"
                          + " | can interact: " + canInteract
                          + " | cutscene playing: " + (cutscene != null && cutscene.IsPlaying)
                          + " | already played: " + hasPlayed, this);
            }
            lastShow = show;
        }

        if (show) ShowPrompt();
        else HidePrompt();

        if (show && InteractPressed())
        {
            if (debugLogs) Debug.Log("NPCTrigger '" + name + "': E pressed, trying to play '" + CurrentCutsceneId() + "'.", this);
            TryPlay();
        }
    }

    protected virtual void OnDisable()
    {
        HidePrompt();
    }

    protected virtual void OnDestroy()
    {
        HidePrompt();
    }

    protected virtual void OnDrawGizmosSelected()
    {
        Transform origin = distanceFrom != null ? distanceFrom : transform;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(origin.position, interactDistance);
    }

    // Works when the NPC is a UI Image on a Canvas (only if Allow Mouse Click is on)
    public void OnPointerClick(PointerEventData eventData)
    {
        if (allowMouseClick && IsPlayerNear())
            TryPlay();
    }

    // Works when the NPC is a sprite/object in the world with a Collider (only if Allow Mouse Click is on)
    private void OnMouseDown()
    {
        if (!allowMouseClick || !IsPlayerNear()) return;

        // Ignore clicks that land on UI in front of the NPC
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        TryPlay();
    }

    // -------------------------------------------------------------------
    // Proximity + input
    // -------------------------------------------------------------------

    protected bool IsPlayerNear()
    {
        if (player == null)
        {
            GameObject p = GameObject.FindWithTag("Player");
            if (p != null) player = p.transform;
        }
        if (player == null)
        {
            if (!warnedNoPlayer)
            {
                warnedNoPlayer = true;
                Debug.LogWarning("NPCTrigger: no player found. Drag your Player object into the Player field.", this);
            }
            return false;
        }

        Transform origin = distanceFrom != null ? distanceFrom : transform;
        Vector3 a = origin.position;
        Vector3 b = player.position;
        if (ignoreHeight) { a.y = 0f; b.y = 0f; }

        lastDistance = Vector3.Distance(a, b);
        return lastDistance <= interactDistance;
    }

    /// <summary>True if this collider belongs to the player (tag 'Player' on it, its Rigidbody, or its root).</summary>
    protected bool IsPlayerCollider(Collider other)
    {
        if (other == null) return false;
        if (player != null && (other.transform == player || other.transform.IsChildOf(player))) return true;
        if (other.CompareTag("Player")) return true;
        if (other.attachedRigidbody != null && other.attachedRigidbody.CompareTag("Player")) return true;
        return other.transform.root.CompareTag("Player");
    }

    // Works with the old Input Manager, the new Input System, or both
    private static bool InteractPressed()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        var kb = UnityEngine.InputSystem.Keyboard.current;
        return kb != null && kb.eKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.E);
#endif
    }

    // -------------------------------------------------------------------
    // Prompt
    // -------------------------------------------------------------------

    private void ShowPrompt()
    {
        promptOwner = this;

        if (promptObject != null)
        {
            if (!promptObject.activeSelf) promptObject.SetActive(true);
            return;
        }

        EnsureSharedPrompt();
        if (sharedText != null && sharedText.text != promptText) sharedText.text = promptText;
        if (sharedLegacyText != null && sharedLegacyText.text != promptText) sharedLegacyText.text = promptText;
        if (sharedPrompt != null && !sharedPrompt.activeSelf) sharedPrompt.SetActive(true);
    }

    private void HidePrompt()
    {
        if (promptObject != null)
        {
            if (promptObject.activeSelf) promptObject.SetActive(false);
            return;
        }

        // Only hide the shared prompt if it is ours (another NPC may be showing it)
        if (promptOwner != this) return;
        promptOwner = null;

        if (sharedPrompt != null) sharedPrompt.SetActive(false);
    }

    private void EnsureSharedPrompt()
    {
        if (sharedPrompt != null) return;

        GameObject canvasGo = new GameObject("NPCPromptCanvas");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject textGo = new GameObject("PromptText", typeof(RectTransform));
        textGo.transform.SetParent(canvasGo.transform, false);

        RectTransform rt = (RectTransform)textGo.transform;
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(1000f, 80f);
        rt.anchoredPosition = new Vector2(0f, promptBottomOffset);

        bool useTMP = promptFont != null || promptFontFile != null || TMP_Settings.defaultFontAsset != null;

        if (useTMP)
        {
            TextMeshProUGUI text = textGo.AddComponent<TextMeshProUGUI>();
            if (promptFont != null)
                text.font = promptFont;
            else if (promptFontFile != null)
                text.font = TMP_FontAsset.CreateFontAsset(promptFontFile);
            text.text = promptText;
            text.fontSize = promptFontSize;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = promptColor;
            text.raycastTarget = false;
            sharedText = text;
        }
        else
        {
            // TextMeshPro has no default font in this project: use Unity's built-in font so the prompt is always visible
            Debug.LogWarning("NPCTrigger: TextMeshPro has no default font, so the prompt uses Unity's built-in font. Drag your font into Prompt Font File to change it.", this);

            Text text = textGo.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = promptText;
            text.fontSize = Mathf.RoundToInt(promptFontSize);
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.color = promptColor;
            text.raycastTarget = false;
            sharedLegacyText = text;
        }

        sharedPrompt = canvasGo;
        sharedPrompt.SetActive(false);
    }

    // -------------------------------------------------------------------
    // Play
    // -------------------------------------------------------------------

    /// <summary>Presses-E entry point: plays whatever this NPC currently has to say.</summary>
    protected bool TryPlay()
    {
        if (!CanTalk()) return false;
        return PlayId(CurrentCutsceneId());
    }

    /// <summary>Plays the cutscene with this Id (with clear warnings when something is wrong). Returns true if it started.</summary>
    protected bool PlayId(string id)
    {
        if (cutscene == null)
        {
            Debug.LogWarning("NPCTrigger: no cutscene assigned.", this);
            return false;
        }

        if (cutscene.IsPlaying) return false;

        // Say clearly when the Id doesn't match anything on the IntroCutscene
        if (!string.IsNullOrEmpty(id) && !cutscene.HasCutscene(id))
        {
            Debug.LogWarning("NPCTrigger: the IntroCutscene has no cutscene with the Id '" + id
                             + "'. Check the Cutscenes list on the IntroCutscene (the Id must match exactly, no extra spaces).", this);
            return false;
        }

        // Cutscene object can stay disabled until now
        if (!cutscene.gameObject.activeSelf)
            cutscene.gameObject.SetActive(true);

        if (!cutscene.gameObject.activeInHierarchy)
        {
            Debug.LogWarning("NPCTrigger: the cutscene object is still inactive because a parent object is disabled. Enable the parent, or drag in an IntroCutscene that is not under a disabled object.", this);
            return false;
        }

        // Only counts as played if it really started (e.g. the Id exists and has lines)
        if (cutscene.TryPlayCutscene(id))
        {
            hasPlayed = true;
            OnCutsceneStarted(id);
            return true;
        }

        Debug.LogWarning("NPCTrigger: the cutscene '" + id + "' did not start. See the IntroCutscene message just above (usually: it has no lines).", this);
        return false;
    }

    // -------------------------------------------------------------------
    // Text helper for subclasses (uses the same font settings as the prompt)
    // -------------------------------------------------------------------

    protected struct LabelRef
    {
        public TMP_Text tmp;
        public Text legacy;

        public void SetText(string value)
        {
            if (tmp != null) { if (tmp.text != value) tmp.text = value; }
            else if (legacy != null && legacy.text != value) legacy.text = value;
        }
    }

    /// <summary>Adds a text component (TextMeshPro, or Unity's built-in font if TMP has no default font) to an object that has a RectTransform.</summary>
    protected LabelRef AddLabel(GameObject go, string text, float size, Color color, bool alignLeft)
    {
        var label = new LabelRef();
        bool useTMP = promptFont != null || promptFontFile != null || TMP_Settings.defaultFontAsset != null;

        if (useTMP)
        {
            TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
            if (promptFont != null)
                t.font = promptFont;
            else if (promptFontFile != null)
                t.font = TMP_FontAsset.CreateFontAsset(promptFontFile);
            t.text = text;
            t.fontSize = size;
            t.fontStyle = FontStyles.Bold;
            t.alignment = alignLeft ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.color = color;
            t.raycastTarget = false;
            label.tmp = t;
        }
        else
        {
            Text t = go.AddComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.text = text;
            t.fontSize = Mathf.RoundToInt(size);
            t.fontStyle = FontStyle.Bold;
            t.alignment = alignLeft ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.color = color;
            t.raycastTarget = false;
            label.legacy = t;
        }

        return label;
    }
}