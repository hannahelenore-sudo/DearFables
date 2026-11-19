using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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

    [Header("Sound")]
    public AudioClip pickupSound;
    [Range(0f, 1f)] public float pickupVolume = 1f;

    private Vector3 startPos;
    private bool taken;
    private float questDoneTimer;

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