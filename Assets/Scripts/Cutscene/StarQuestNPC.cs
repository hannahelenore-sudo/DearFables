using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Remembers star quests for the play session (static, like BoonProgress / CardPickup), so a quest
/// stays accepted / done after a card battle loads the overworld again.
/// Call StarQuestProgress.ResetAll() from your "New Game" button.
/// Anything can check it, e.g. a gate: StarQuestProgress.IsDone("StarQuest").
/// </summary>
public static class StarQuestProgress
{
    private static readonly HashSet<string> accepted = new HashSet<string>();
    private static readonly HashSet<string> done = new HashSet<string>();
    private static readonly HashSet<string> enemy = new HashSet<string>();
    private static readonly HashSet<string> afterGame = new HashSet<string>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnLaunch()
    {
        ResetAll();
    }

    public static void ResetAll()
    {
        accepted.Clear();
        done.Clear();
        enemy.Clear();
        afterGame.Clear();
    }

    public static bool IsAccepted(string questId) { return accepted.Contains(questId ?? ""); }
    public static bool IsDone(string questId) { return done.Contains(questId ?? ""); }
    public static void Accept(string questId) { accepted.Add(questId ?? ""); }

    /// <summary>True once the quest giver has turned into the stationary enemy (after the card game talk).</summary>
    public static bool IsEnemy(string questId) { return enemy.Contains(questId ?? ""); }
    public static void StartEnemy(string questId) { enemy.Add(questId ?? ""); }

    /// <summary>True once the cutscene that plays after the card game has been shown (it only plays once).</summary>
    public static bool IsAfterGameDone(string questId) { return afterGame.Contains(questId ?? ""); }
    public static void MarkAfterGameDone(string questId) { afterGame.Add(questId ?? ""); }

    public static void Complete(string questId)
    {
        accepted.Add(questId ?? "");
        done.Add(questId ?? "");
    }
}

/// <summary>
/// A star quest giver (a special NPCTrigger). Everything about talking (Press E prompt, distance, fonts...)
/// works like NPCTrigger. On top of that:
///
///  1. First talk (or stepping into the generated zone): plays the Intro dialogue. Afterwards the quest is
///     "accepted" and the star sprite + requirement ("2 / 5") floats above the character.
///  2. Talking again before you have enough stars plays the Reminder dialogue, as often as you like.
///  3. Talking with enough stars plays the Complete dialogue and ticks the star tutorial as DONE
///     (or it ticks by itself the moment you have enough, if Tick Automatically is on).
///  4. Right after that, the Card Game dialogue (Follow Up Id) explains the card game. Then the NPC turns into
///     an ENEMY that stands still: add an EnemyChase component to this same object. Throwing a card at it
///     starts the card game (the battle scene shows its sprite). Once beaten it is gone, like any enemy.
///
/// The four dialogues are entries on the IntroCutscene's Cutscenes list; typing their Ids here creates them for you.
/// The inherited "Cutscene Id" / "Play Only Once" fields are not used by this NPC.
/// </summary>
public class StarQuestNPC : NPCTrigger
{
    [Header("Star Quest")]
    [Tooltip("Unique name for this quest. Progress is remembered under it (and other scripts can check StarQuestProgress.IsDone with it).")]
    public string questId = "StarQuest";
    public int requiredStars = 5;
    [Tooltip("ON: the star tutorial ticks as done the moment the player has enough stars, without talking again. OFF: the player has to talk to the NPC to hand in.")]
    public bool tickAutomatically = false;

    [Header("Dialogues (Ids on the IntroCutscene, created for you)")]
    [Tooltip("Played the first time (talking, or stepping into the zone).")]
    public string introId = "Star Intro";
    [Tooltip("Played whenever the player talks to the NPC without enough stars yet.")]
    public string reminderId = "Star Reminder";
    [Tooltip("Played when the player talks to the NPC with enough stars. Ticks the tutorial as done afterwards.")]
    public string completeId = "Star Complete";
    [Tooltip("Optional. Only used if Become Enemy is OFF: played when talking after the quest is done. Leave EMPTY and the NPC stops reacting.")]
    public string doneId = "";

    [Header("After the star tutorial is done")]
    [Tooltip("The Card Game dialogue: plays right after the Complete dialogue (or the moment it ticks automatically). The NPC turns into an enemy when it ends. If a CardPickup is set to unlock on this quest, its card tutorial plays after this one. Leave EMPTY to skip it.")]
    public string followUpId = "Card Game Intro";
    [Tooltip("ON: after the Card Game dialogue this NPC stops talking and becomes a stationary enemy. A thrown card starts the card game. Needs an EnemyChase component on this same object.")]
    public bool becomeEnemy = true;
    [Tooltip("Leave empty to use the EnemyChase on this object. It is switched OFF until the NPC becomes an enemy.")]
    public EnemyChase enemy;
    [Tooltip("Switched ON when the tutorial is done (and stay on after loading back from a battle). Start them disabled in the scene: the card NPC, a marker, the Enemies group...")]
    public GameObject[] activateWhenDone;

    [Header("After the card game is won")]
    [Tooltip("Dialogue / cutscene that plays in the game scene after the player beat this NPC in the card game (right after coming back). Plays once, then the NPC is gone. Write its lines on the IntroCutscene like the other Ids. Leave EMPTY for no extra cutscene.")]
    public string afterGameId = "After Card Game";
    [Tooltip("Seconds to wait after the game scene appears (lets the fade-in and the battery drop start) before the cutscene begins.")]
    public float afterGameDelay = 1.2f;
    [Tooltip("Runs when the after-card-game cutscene has finished.")]
    public UnityEvent onAfterGameDone;

    [Header("Requirement above the character")]
    [Tooltip("The star picture shown next to the number.")]
    public Sprite starSprite;
    [Tooltip("Where it floats, relative to the NPC (world units).")]
    public Vector3 displayOffset = new Vector3(0f, 2.5f, 0f);
    [Tooltip("Makes the whole display bigger / smaller.")]
    public float displayScale = 1f;
    [Tooltip("{have} = stars the player has (up to the requirement), {need} = stars required.")]
    public string textFormat = "{have} / {need}";
    public float textSize = 60f;
    public Color textColor = Color.white;

    [Header("Generated dialogue zone (optional)")]
    [Tooltip("Generates an invisible trigger box for you. Not needed if you add a Star Quest Zone script to your own object instead.")]
    public bool createZone = false;
    public Vector3 zoneSize = new Vector3(4f, 3f, 4f);
    [Tooltip("Where the zone is, relative to the NPC.")]
    public Vector3 zoneOffset = Vector3.zero;
    [Tooltip("Generated zone only: ON = it disappears once the quest is accepted, OFF = it stays but does nothing.")]
    public bool destroyZoneWhenAccepted = true;

    [Header("Events")]
    public UnityEvent onQuestAccepted;
    [Tooltip("Runs when the star tutorial is ticked as done.")]
    public UnityEvent onTutorialDone;

    private GameObject zoneObject;
    private GameObject displayRoot;
    private LabelRef displayLabel;

    // -------------------------------------------------------------------
    // State
    // -------------------------------------------------------------------

    public bool Accepted { get { return StarQuestProgress.IsAccepted(questId); } }
    public bool Done { get { return StarQuestProgress.IsDone(questId); } }
    public bool HasEnoughStars { get { return PlayerProgress.TotalStars >= requiredStars; } }

    // -------------------------------------------------------------------
    // NPCTrigger hooks
    // -------------------------------------------------------------------

    protected override IEnumerable<string> AllCutsceneIds()
    {
        yield return introId;
        yield return reminderId;
        yield return completeId;
        yield return doneId;
        yield return followUpId;
        yield return afterGameId;
    }

    protected override string CurrentCutsceneId()
    {
        if (!Accepted) return introId;
        if (!Done) return HasEnoughStars ? completeId : reminderId;
        return doneId;
    }

    protected override bool CanTalk()
    {
        if (StarQuestProgress.IsEnemy(questId)) return false; // it is an enemy now: no more talking
        if (Done) return !string.IsNullOrWhiteSpace(doneId);
        return true;
    }

    protected override void OnCutsceneStarted(string id)
    {
        StartCoroutine(WatchCutscene(id));
    }

    // -------------------------------------------------------------------
    // Unity
    // -------------------------------------------------------------------

    protected override void Awake()
    {
        base.Awake();

        // The enemy part stays off until the NPC has finished talking about the card game
        if (becomeEnemy)
        {
            if (enemy == null) enemy = GetComponent<EnemyChase>();
            if (enemy != null) enemy.enabled = false;

            if (Done)
            {
                // Loaded back after the quest (e.g. from the battle): already beaten = gone, otherwise it's an enemy again
                if (enemy != null && enemy.WasCaught)
                {
                    ActivateObjects();

                    // Beaten in the card game: play the after-game cutscene once, THEN disappear
                    if (!string.IsNullOrWhiteSpace(afterGameId) && !StarQuestProgress.IsAfterGameDone(questId))
                    {
                        BeginAfterGame();
                        return;
                    }

                    Destroy(gameObject);
                    return;
                }
                BecomeEnemy();
            }
        }

        if (createZone && !Accepted) CreateZone();
        if (Done) ActivateObjects();
        RefreshDisplay();
    }

    protected override void Update()
    {
        base.Update();

        if (Accepted && zoneObject != null && destroyZoneWhenAccepted)
        {
            Destroy(zoneObject);
            zoneObject = null;
        }

        if (tickAutomatically && Accepted && !Done && HasEnoughStars)
            CompleteQuest();

        RefreshDisplay();
    }

    private void LateUpdate()
    {
        if (displayRoot == null || !displayRoot.activeSelf) return;

        displayRoot.transform.position = transform.position + displayOffset;

        Camera cam = Camera.main;
        if (cam != null) displayRoot.transform.rotation = cam.transform.rotation;
    }

    protected override void OnDestroy()
    {
        if (zoneObject != null) Destroy(zoneObject);
        if (displayRoot != null) Destroy(displayRoot);
        base.OnDestroy();
    }

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();

        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(transform.position + zoneOffset, zoneSize);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position + displayOffset, 0.25f * displayScale);
    }

    // -------------------------------------------------------------------
    // Quest flow
    // -------------------------------------------------------------------

    private IEnumerator WatchCutscene(string id)
    {
        while (cutscene != null && cutscene.IsPlaying)
            yield return null;

        if (string.Equals(id, introId, System.StringComparison.OrdinalIgnoreCase))
            AcceptQuest();
        else if (string.Equals(id, completeId, System.StringComparison.OrdinalIgnoreCase))
            CompleteQuest();
        else if (string.Equals(id, followUpId, System.StringComparison.OrdinalIgnoreCase))
            BecomeEnemy();
    }

    private void AcceptQuest()
    {
        if (Accepted) return;

        StarQuestProgress.Accept(questId);
        onQuestAccepted?.Invoke();
        RefreshDisplay();
    }

    private void CompleteQuest()
    {
        if (Done) return;

        StarQuestProgress.Complete(questId);
        ActivateObjects();
        onTutorialDone?.Invoke();
        RefreshDisplay();

        if (!string.IsNullOrWhiteSpace(followUpId))
            StartCoroutine(FollowUpRoutine());
        else
            BecomeEnemy();
    }

    // The NPC was beaten: hide it right away, play the after-card-game cutscene, then remove it
    private void BeginAfterGame()
    {
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true)) r.enabled = false;
        foreach (Collider c in GetComponentsInChildren<Collider>(true)) c.enabled = false;
        if (enemy != null) enemy.enabled = false;

        StartCoroutine(AfterGameRoutine());
    }

    private IEnumerator AfterGameRoutine()
    {
        // Remember first, so reloading the scene mid-cutscene can never play it twice
        StarQuestProgress.MarkAfterGameDone(questId);

        if (afterGameDelay > 0f) yield return new WaitForSecondsRealtime(afterGameDelay);

        // Wait if some other dialogue is playing
        while (cutscene != null && cutscene.IsPlaying)
            yield return null;

        if (cutscene != null && PlayId(afterGameId))
        {
            yield return null;
            while (cutscene != null && cutscene.IsPlaying)
                yield return null;
        }

        onAfterGameDone?.Invoke();
        Destroy(gameObject);
    }

    /// <summary>Turns this NPC into a stationary enemy: a thrown card starts the card game.</summary>
    private void BecomeEnemy()
    {
        if (!becomeEnemy) return;

        StarQuestProgress.StartEnemy(questId);

        if (enemy == null) enemy = GetComponent<EnemyChase>();
        if (enemy == null)
        {
            Debug.LogWarning("StarQuestNPC: Become Enemy is on but there is no EnemyChase on this object. Add an EnemyChase component to it.", this);
            return;
        }

        if (enemy.player == null)
        {
            GameObject p = GameObject.FindWithTag("Player");
            if (p != null) enemy.player = p.transform;
        }

        // Stands still, and only a thrown card starts the fight
        enemy.moveSpeed = 0f;
        enemy.detectionRange = 0f;
        enemy.returnToStartWhenLost = false;
        enemy.touchStartsBattle = false;
        enemy.startBattleOnCardHit = true;
        enemy.explodeWhenOverpowered = false;
        enemy.enabled = true;
    }

    private void ActivateObjects()
    {
        if (activateWhenDone == null) return;

        foreach (GameObject go in activateWhenDone)
            if (go != null && !go.activeSelf) go.SetActive(true);
    }

    // Waits for any dialogue to finish, then plays the "go to the next NPC" cutscene
    private IEnumerator FollowUpRoutine()
    {
        while (cutscene != null && cutscene.IsPlaying)
            yield return null;

        yield return null;

        // If the dialogue can't play (missing lines...), don't leave the NPC stuck
        if (!PlayId(followUpId))
            BecomeEnemy();
    }

    // -------------------------------------------------------------------
    // Zone
    // -------------------------------------------------------------------

    private void CreateZone()
    {
        zoneObject = new GameObject("StarQuestZone_" + questId);
        zoneObject.transform.position = transform.position + zoneOffset;

        BoxCollider box = zoneObject.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = zoneSize;

        StarQuestZone zone = zoneObject.AddComponent<StarQuestZone>();
        zone.npc = this;
        zone.disappearWhenAccepted = false; // this NPC removes its own generated zone
    }

    /// <summary>Called by the zone while something is inside it.</summary>
    public void ZoneTriggered(Collider other)
    {
        if (Accepted) return;
        if (!IsPlayerCollider(other)) return;
        if (cutscene == null || cutscene.IsPlaying) return;

        PlayId(introId);
    }

    // -------------------------------------------------------------------
    // Star requirement above the character
    // -------------------------------------------------------------------

    private void RefreshDisplay()
    {
        bool visible = Accepted && !Done;

        if (!visible)
        {
            if (displayRoot != null && displayRoot.activeSelf) displayRoot.SetActive(false);
            return;
        }

        EnsureDisplay();
        if (!displayRoot.activeSelf) displayRoot.SetActive(true);

        int have = Mathf.Min(PlayerProgress.TotalStars, requiredStars);
        displayLabel.SetText(textFormat.Replace("{have}", have.ToString()).Replace("{need}", requiredStars.ToString()));
    }

    private void EnsureDisplay()
    {
        if (displayRoot != null) return;

        displayRoot = new GameObject("StarRequirement_" + questId);

        Canvas canvas = displayRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 300;

        RectTransform rootRect = displayRoot.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(400f, 120f);
        rootRect.localScale = Vector3.one * 0.01f * displayScale;
        rootRect.position = transform.position + displayOffset;

        // Icon and number sit side by side, centered
        HorizontalLayoutGroup row = displayRoot.AddComponent<HorizontalLayoutGroup>();
        row.childAlignment = TextAnchor.MiddleCenter;
        row.spacing = 15f;
        row.childControlWidth = true;
        row.childControlHeight = true;
        row.childForceExpandWidth = false;
        row.childForceExpandHeight = false;

        if (starSprite != null)
        {
            GameObject iconGo = new GameObject("StarIcon", typeof(RectTransform));
            iconGo.transform.SetParent(displayRoot.transform, false);

            Image icon = iconGo.AddComponent<Image>();
            icon.sprite = starSprite;
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            LayoutElement iconSize = iconGo.AddComponent<LayoutElement>();
            iconSize.preferredWidth = 110f;
            iconSize.preferredHeight = 110f;
        }

        GameObject textGo = new GameObject("RequirementText", typeof(RectTransform));
        textGo.transform.SetParent(displayRoot.transform, false);
        displayLabel = AddLabel(textGo, "", textSize, textColor, true);
    }
}