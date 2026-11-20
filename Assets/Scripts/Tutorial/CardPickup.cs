using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A card the player walks into to pick up.
///
/// While this object is in the scene and has NOT been picked up:
///   - the Card Shooter (and anything else you list) is disabled
///   - the Boon Meter UI (and anything else you list) is hidden
///   - fights do not fill the boon meter (see the BoonProgress.AddMeter change)
///
/// When the player touches it, all of that is switched on and the boon meter is revealed.
/// The pickup is remembered for the play session (static, like BoonProgress), so after a card
/// battle the overworld loads with everything already unlocked and this object removes itself.
/// If you delete this object from the scene, nothing is locked and the game behaves as before.
///
/// Setup: put this on an object with a sprite/model and a Collider (a trigger Sphere Collider is
/// added automatically if there is none). The Player needs the tag "Player" and a Rigidbody or
/// CharacterController, as usual for trigger events.
/// </summary>
public class CardPickup : MonoBehaviour
{
    // -------------------------------------------------------------------
    // Session state (survives scene loads, not saved to disk)
    // -------------------------------------------------------------------

    /// <summary>True once the player has picked the card up.</summary>
    public static bool Collected { get; private set; }

    /// <summary>True while a pickup exists that has not been collected yet. Boon/shooter stay off.</summary>
    public static bool Locked { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnLaunch()
    {
        ResetAll();
    }

    /// <summary>Call from your "New Game" button (next to BoonProgress.ResetAll()).</summary>
    public static void ResetAll()
    {
        Collected = false;
        Locked = false;
    }

    // -------------------------------------------------------------------
    // Inspector
    // -------------------------------------------------------------------

    [Header("Boon system (drag it in here)")]
    [Tooltip("Drag in everything that makes up the boon system (the Boon Meter UI, its canvas, the boon stuff in your scene). All of it is switched OFF while the card is still lying there and switched ON when the player picks the card up. Leave empty to auto-find the Boon Meter UI.")]
    public GameObject[] boonSystem;

    [Header("Locked until picked up")]
    [Tooltip("Leave empty to auto-find the CardShooter in the scene.")]
    public CardShooter cardShooter;
    [Tooltip("Other scripts to disable until pickup (e.g. WindBoost, CardStamina, HeldItemIcons if they already sit on the player).")]
    public Behaviour[] extraBehaviours;
    [Tooltip("Anything else that should be hidden until pickup (the stamina bar...). Boon objects go in Boon System above.")]
    public GameObject[] objectsToHide;

    [Header("Unlock by star quest (optional)")]
    [Tooltip("The Quest Id of your Star Quest NPC. When that quest is done, the card shooter and boon system unlock by themselves and the card tutorial plays (touching the pickup is then ignored). Leave EMPTY to unlock by touching instead.")]
    public string unlockWhenQuestDone = "";
    [Tooltip("Seconds to wait after the quest is done (so any 'go to...' cutscene plays first). It also waits for any cutscene that is still playing.")]
    public float unlockDelay = 0.5f;

    [Header("Pickup")]
    [Tooltip("ON: the player collects it by walking into it. OFF: only something else can give it by calling Collect().")]
    public bool touchToCollect = true;
    public string playerTag = "Player";
    [Tooltip("Size of the auto-added trigger.")]
    public float triggerRadius = 0.8f;

    [Header("Idle animation")]
    public float bobHeight = 0.15f;
    public float bobSpeed = 2f;
    public float spinSpeed = 90f;

    [Header("Reveal")]
    [Tooltip("The hidden objects pop in with a little scale animation.")]
    public float revealPopDuration = 0.35f;

    [Header("Tutorial")]
    [Tooltip("Play the tutorial cutscene right after the card is picked up. This is the only thing that starts it.")]
    public bool playTutorial = true;
    [Tooltip("The cutscene manager (the IntroCutscene in your scene). Leave empty to auto-find it.")]
    public IntroCutscene tutorial;
    [Tooltip("Id of the entry in the manager's Cutscenes list to play. Edit its lines on the IntroCutscene.")]
    public string tutorialId = "Card Tutorial";

    [Header("Highlight (same spotlight as the card game tutorial)")]
    [Tooltip("Dims the screen and leaves the card lit up inside a frame, so the player notices it. Only used when the card is collected by touching it. It never pauses the game or blocks any clicks.")]
    public bool highlightPickup = true;
    [Tooltip("Seconds after the scene starts before the highlight fades in. Waits while a cutscene is playing.")]
    public float highlightDelay = 0.5f;
    [Tooltip("How long the highlight stays on. 0 = until the card is picked up.")]
    public float highlightDuration = 4f;
    public float highlightFade = 0.4f;
    public Color dimColor = new Color(0f, 0f, 0f, 0.8f);
    public Color frameColor = new Color(1f, 0.85f, 0.25f, 1f);
    public float frameThickness = 4f;
    [Tooltip("Extra space (in screen pixels) around the card inside the frame.")]
    public float highlightPadding = 40f;
    [Tooltip("Camera used to find the card on screen. Empty = Camera.main.")]
    public Camera worldCamera;
    [Tooltip("Stays under the tutorial overlay (32000) so dialogue always shows on top.")]
    public int overlaySortingOrder = 31000;

    [Header("Sound")]
    public AudioClip pickupSound;
    [Range(0f, 1f)] public float pickupVolume = 1f;

    private Vector3 startPos;
    private bool taken;
    private float questDoneTimer;

    // highlight runtime
    private GameObject hlRoot;
    private TutorialSpotlightGraphic hlSpot;
    private float hlTimer;
    private bool hlDone;
    private bool hlLookedForCutscene;
    private readonly List<Rect> hlHoles = new List<Rect>(1);
    private readonly Vector3[] hlCorners = new Vector3[8];

    // -------------------------------------------------------------------
    // Unity
    // -------------------------------------------------------------------

    private void Awake()
    {
        // Already picked up earlier in this session (e.g. back from a battle): nothing to lock
        if (Collected)
        {
            Destroy(gameObject);
            return;
        }

        Locked = true;

        if (cardShooter == null) cardShooter = FindAnyObjectByType<CardShooter>();

        // Everything dragged into Boon System is locked together with Objects To Hide
        if (boonSystem != null && boonSystem.Length > 0)
        {
            var merged = new List<GameObject>();
            if (objectsToHide != null)
                foreach (GameObject go in objectsToHide) AddUnique(merged, go);
            foreach (GameObject go in boonSystem) AddUnique(merged, go);
            objectsToHide = merged.ToArray();
        }

        // Nothing assigned at all: hide the whole boon meter automatically. The BoonMeterUI script often sits on
        // a small object next to the Container / Liquid images (not above them), so every visual it points at is collected too.
        if (objectsToHide == null || objectsToHide.Length == 0)
        {
            BoonMeterUI meter = FindAnyObjectByType<BoonMeterUI>();
            if (meter != null)
            {
                var list = new List<GameObject>();
                AddUnique(list, meter.gameObject);
                if (meter.containerImage != null) AddUnique(list, meter.containerImage.gameObject);
                if (meter.liquidGraphic != null) AddUnique(list, meter.liquidGraphic.gameObject);
                if (meter.liquidImage != null) AddUnique(list, meter.liquidImage.gameObject);
                if (meter.percentText != null) AddUnique(list, meter.percentText.gameObject);
                if (meter.meterButton != null) AddUnique(list, meter.meterButton.gameObject);
                objectsToHide = list.ToArray();
            }
        }

        SetLocked(true);

        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            SphereCollider sphere = gameObject.AddComponent<SphereCollider>();
            sphere.radius = triggerRadius;
            col = sphere;
        }
        col.isTrigger = true;
    }

    private void Start()
    {
        startPos = transform.position;
    }

    private void Update()
    {
        if (taken) return;

        CheckQuestUnlock();
        if (taken) return;

        float y = Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        transform.position = startPos + Vector3.up * y;
        transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
    }

    private void LateUpdate()
    {
        UpdateHighlight();
    }

    private void OnDestroy()
    {
        if (hlRoot != null) Destroy(hlRoot);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (taken || !touchToCollect || !string.IsNullOrEmpty(unlockWhenQuestDone)) return;
        if (!other.CompareTag(playerTag) && (other.attachedRigidbody == null || !other.attachedRigidbody.CompareTag(playerTag)))
            return;

        StartCoroutine(CollectRoutine());
    }

    // -------------------------------------------------------------------
    // Collect
    // -------------------------------------------------------------------

    private void CheckQuestUnlock()
    {
        if (string.IsNullOrEmpty(unlockWhenQuestDone) || !StarQuestProgress.IsDone(unlockWhenQuestDone)) return;

        questDoneTimer += Time.deltaTime;
        if (questDoneTimer < unlockDelay) return;

        if (tutorial == null) tutorial = FindAnyObjectByType<IntroCutscene>(FindObjectsInactive.Include);
        if (tutorial != null && tutorial.IsPlaying) return;

        Collect();
    }

    /// <summary>Gives the card to the player right now (used by CardGiverNPC). Does nothing if already collected.</summary>
    public void Collect()
    {
        if (taken) return;
        StartCoroutine(CollectRoutine());
    }

    private IEnumerator CollectRoutine()
    {
        taken = true;
        Collected = true;
        Locked = false;
        EndHighlight();

        if (pickupSound != null)
            GameSFX.Play(pickupSound, pickupVolume, 1f);

        // Hide the pickup itself right away
        foreach (Renderer r in GetComponentsInChildren<Renderer>()) r.enabled = false;
        foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = false;

        SetLocked(false);

        // Pop the revealed objects in (BoonMeterUI also starts its own fill animation when it is enabled)
        if (objectsToHide != null && revealPopDuration > 0f)
        {
            var targets = new System.Collections.Generic.List<Transform>();
            var baseScales = new System.Collections.Generic.List<Vector3>();
            foreach (GameObject go in objectsToHide)
            {
                if (go == null) continue;
                targets.Add(go.transform);
                baseScales.Add(go.transform.localScale);
                go.transform.localScale = Vector3.zero;
            }

            float elapsed = 0f;
            while (elapsed < revealPopDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / revealPopDuration);

                // ease out back
                const float c1 = 1.70158f;
                const float c3 = c1 + 1f;
                float eased = 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);

                for (int i = 0; i < targets.Count; i++)
                    if (targets[i] != null) targets[i].localScale = baseScales[i] * Mathf.Max(0f, eased);

                yield return null;
            }

            for (int i = 0; i < targets.Count; i++)
                if (targets[i] != null) targets[i].localScale = baseScales[i];
        }

        // Boon UI is now revealed: explain how everything works (only ever started from here)
        if (playTutorial)
        {
            if (tutorial == null) tutorial = FindAnyObjectByType<IntroCutscene>(FindObjectsInactive.Include);

            if (tutorial == null)
            {
                Debug.LogWarning("CardPickup: Play Tutorial is on but no IntroCutscene was found in the scene.", this);
            }
            else
            {
                // The cutscene object may start disabled (like for the NPC), so make sure it is active
                if (!tutorial.gameObject.activeSelf) tutorial.gameObject.SetActive(true);

                if (tutorial.TryPlayCutscene(tutorialId))
                {
                    // Stay alive (hidden) until it closes
                    while (tutorial != null && tutorial.IsPlaying) yield return null;
                }
            }
        }

        Destroy(gameObject);
    }

    // -------------------------------------------------------------------
    // Highlight (dim + lit-up cut-out + frame, like CardGameTutorial)
    // -------------------------------------------------------------------

    private void UpdateHighlight()
    {
        if (hlDone) return;

        if (taken || !highlightPickup || !touchToCollect || !string.IsNullOrEmpty(unlockWhenQuestDone))
        {
            EndHighlight();
            return;
        }

        // Don't compete with a cutscene: wait until it is over
        if (!hlLookedForCutscene)
        {
            hlLookedForCutscene = true;
            if (tutorial == null) tutorial = FindAnyObjectByType<IntroCutscene>(FindObjectsInactive.Include);
        }
        if (tutorial != null && tutorial.IsPlaying)
        {
            SetHighlightVisible(false);
            return;
        }

        hlTimer += Time.unscaledDeltaTime;
        float t = hlTimer - highlightDelay;
        if (t < 0f) return;

        if (highlightDuration > 0f && t >= highlightDuration)
        {
            EndHighlight();
            return;
        }

        float fade = Mathf.Max(0.01f, highlightFade);
        float a = Mathf.Clamp01(t / fade);
        if (highlightDuration > 0f) a = Mathf.Min(a, Mathf.Clamp01((highlightDuration - t) / fade));

        if (!TryGetScreenRect(out Rect rect))
        {
            SetHighlightVisible(false); // card is off screen / behind the camera
            return;
        }

        rect.xMin -= highlightPadding; rect.xMax += highlightPadding;
        rect.yMin -= highlightPadding; rect.yMax += highlightPadding;

        EnsureHighlightOverlay();
        SetHighlightVisible(true);

        Color dc = dimColor; dc.a *= a;
        Color fc = frameColor; fc.a *= a;
        hlHoles.Clear();
        hlHoles.Add(rect);
        hlSpot.SetHoles(hlHoles, dc, fc, frameThickness);
    }

    private void EndHighlight()
    {
        hlDone = true;
        if (hlRoot != null) Destroy(hlRoot);
        hlRoot = null;
        hlSpot = null;
    }

    private void SetHighlightVisible(bool visible)
    {
        if (hlRoot != null && hlRoot.activeSelf != visible) hlRoot.SetActive(visible);
    }

    private void EnsureHighlightOverlay()
    {
        if (hlRoot != null) return;

        hlRoot = new GameObject("CardPickupHighlight", typeof(RectTransform));

        Canvas canvas = hlRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = overlaySortingOrder;

        // Same canvas setup as the tutorial overlay so the cut-out lines up the same way
        CanvasScaler scaler = hlRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        // No GraphicRaycaster and raycastTarget off: the player can keep walking / clicking underneath
        GameObject dimGo = new GameObject("Dim", typeof(RectTransform), typeof(CanvasRenderer));
        dimGo.transform.SetParent(hlRoot.transform, false);
        RectTransform dimRt = dimGo.GetComponent<RectTransform>();
        dimRt.anchorMin = Vector2.zero; dimRt.anchorMax = Vector2.one;
        dimRt.offsetMin = dimRt.offsetMax = Vector2.zero;
        dimRt.pivot = Vector2.zero;
        hlSpot = dimGo.AddComponent<TutorialSpotlightGraphic>();
        hlSpot.raycastTarget = false;
    }

    /// <summary>Screen-space box around the card (it bobs and spins, so this is recalculated every frame).</summary>
    private bool TryGetScreenRect(out Rect rect)
    {
        rect = default;
        Camera cam = worldCamera != null ? worldCamera : Camera.main;
        if (cam == null) return false;

        Bounds b = default;
        bool hasBounds = false;
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled) continue;
            if (!hasBounds) { b = r.bounds; hasBounds = true; }
            else b.Encapsulate(r.bounds);
        }
        if (!hasBounds)
        {
            Collider col = GetComponent<Collider>();
            if (col == null) return false;
            b = col.bounds;
        }

        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        bool any = false;
        for (int i = 0; i < 8; i++)
        {
            hlCorners[i] = new Vector3(
                (i & 1) == 0 ? b.min.x : b.max.x,
                (i & 2) == 0 ? b.min.y : b.max.y,
                (i & 4) == 0 ? b.min.z : b.max.z);
            Vector3 sp = cam.WorldToScreenPoint(hlCorners[i]);
            if (sp.z <= 0f) continue;
            any = true;
            minX = Mathf.Min(minX, sp.x); maxX = Mathf.Max(maxX, sp.x);
            minY = Mathf.Min(minY, sp.y); maxY = Mathf.Max(maxY, sp.y);
        }
        if (!any) return false;

        minX = Mathf.Max(0f, minX); minY = Mathf.Max(0f, minY);
        maxX = Mathf.Min(Screen.width, maxX); maxY = Mathf.Min(Screen.height, maxY);
        if (maxX - minX < 2f || maxY - minY < 2f) return false;

        rect = Rect.MinMaxRect(minX, minY, maxX, maxY);
        return true;
    }

    // -------------------------------------------------------------------
    // Lock / unlock
    // -------------------------------------------------------------------

    private static void AddUnique(List<GameObject> list, GameObject go)
    {
        if (go != null && !list.Contains(go)) list.Add(go);
    }

    private void SetLocked(bool locked)
    {
        if (cardShooter != null) cardShooter.enabled = !locked;

        if (extraBehaviours != null)
            foreach (Behaviour b in extraBehaviours)
                if (b != null) b.enabled = !locked;

        if (objectsToHide != null)
            foreach (GameObject go in objectsToHide)
                if (go != null) go.SetActive(!locked);
    }
}