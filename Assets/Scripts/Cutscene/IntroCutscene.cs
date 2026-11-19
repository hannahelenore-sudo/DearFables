using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

// Legacy: only kept so old dialogue lines convert to a Speaker Name automatically.
public enum DialogueSpeaker { Fable, Bee, Narrator }

// Legacy: only kept so old "Default Characters" cutscenes convert to a cast automatically.
public enum CastMode { DefaultCharacters, CustomCast }

/// <summary>Put on a string field to get a dropdown of the names in the IntroCutscene's character list.</summary>
public class CharacterNameAttribute : PropertyAttribute { }

[System.Serializable]
public class DialogueLine
{
    [Tooltip("Who is speaking: pick a character from the list. Leave on (none) for the narrator (nobody lit up). The character should also be in the cutscene's Cast, otherwise only the name box changes.")]
    [CharacterName]
    public string speakerName;

    [TextArea(2, 4)]
    public string text;

    [Header("Expression")]
    public Sprite expression;

    [Header("Tutorial Mode only")]
    [Tooltip("Only used when the cutscene's Tutorial Mode is ON. The Boon UI stays bright above the gray screen during this line.")]
    public bool highlightBoon;

    [HideInInspector] public DialogueSpeaker speaker = DialogueSpeaker.Narrator; // legacy
}

/// <summary>One character: a name and the Image in your scene that shows them.</summary>
[System.Serializable]
public class CutsceneActor
{
    [Tooltip("Shown in the name box when this character speaks. Casts and lines pick the character by this name.")]
    public string name = "Character";
    [Tooltip("The Image object in your dialogue UI for this character. Put it where you want them to stand.")]
    public Image image;
}

/// <summary>
/// One named encounter. Triggers (NPCTrigger, CardPickup, your own scripts) play it by its Id.
/// </summary>
[System.Serializable]
public class CutsceneSequence
{
    [Tooltip("The name triggers use to play this cutscene. Must match exactly (not case sensitive).")]
    public string id = "New Cutscene";

    [Tooltip("ON: no fade to black / no Dialogue Background. The screen is grayed out instead, lines with 'Highlight Boon' keep the Boon UI bright, and the Card Shooter is frozen. OFF: the normal fade-to-black cutscene.")]
    public bool tutorialMode = false;

    [Tooltip("Which characters appear in this cutscene. Pick each from the dropdown (the list comes from the Characters list on the IntroCutscene). Empty = nobody appears.")]
    [CharacterName]
    public string[] castNames;

    public DialogueLine[] lines;

    [Tooltip("Runs when this cutscene starts / ends (in addition to the general events at the bottom).")]
    public UnityEvent onStart;
    public UnityEvent onEnd;

    [HideInInspector] public CastMode castMode = CastMode.CustomCast; // legacy
}

/// <summary>
/// Dialogue cutscene. Does NOTHING until PlayCutscene() is called (NPCTrigger calls it when the NPC is clicked).
/// Flow: fade to black -> show dialogue UI -> fade in -> lines (typewriter + talk ticks) -> fade to black -> hide UI -> fade out.
/// Advance with Space / Enter or the continue button. First press finishes the typing, second goes to the next line.
/// </summary>
public class IntroCutscene : MonoBehaviour
{
    [Header("Dialogue UI")]
    public GameObject dialoguePanel;
    public TMP_Text narratorName;
    public TMP_Text dialogueText;
    public Button continueButton;
    [Tooltip("Optional: little arrow/icon shown when a line has finished typing.")]
    public GameObject continueIndicator;

    [Header("Background Image")]
    public GameObject dialogueBackground;

    [Header("Characters (add as many as you like)")]
    [Tooltip("Every character you can use. Press + to add one: give it a name and drag in its Image from the dialogue UI. Cutscenes then pick who appears from this list.")]
    public List<CutsceneActor> characters = new List<CutsceneActor>();
    [Tooltip("Name box text for lines with no speaker.")]
    public string narratorNameText = "Narrator";

    // Legacy fields (kept hidden so old scenes convert themselves into the Characters list)
    [HideInInspector] public Image fableImage;
    [HideInInspector] public Image beeImage;
    [HideInInspector] public string fableName = "Fable";
    [HideInInspector] public string beeName = "Bee";
    [HideInInspector] public bool migratedToRoster;

    [Header("Character Lighting")]
    public Color activeColor = Color.white;
    public Color inactiveColor = new Color(0.4f, 0.4f, 0.4f, 1f);
    [Tooltip("How fast characters brighten/dim when the speaker changes.")]
    public float lightingSpeed = 10f;

    [Header("Jump Animation")]
    public float jumpHeight = 30f;
    public float jumpDuration = 0.3f;

    [Header("Typewriter")]
    [Tooltip("Characters per second. Set to 0 to show the whole line instantly.")]
    public float charsPerSecond = 45f;

    [Header("Talking Sound")]
    [Tooltip("A single short tick that repeats while someone is talking.")]
    public AudioClip talkSound;
    [Range(0f, 1f)] public float talkVolume = 1f;
    [Tooltip("Seconds between each tick while the text is typing.")]
    public float talkTickInterval = 0.08f;
    [Tooltip("Small random pitch change so the ticks don't sound robotic. 0 = every tick identical.")]
    [Range(0f, 0.3f)] public float talkPitchVariation = 0.05f;

    [Header("Fade Transition")]
    public Image fadeOverlay;
    public float fadeInDuration = 0.6f;
    public float fadeOutDuration = 0.6f;

    [Header("Tutorial Mode settings (for cutscenes with Tutorial Mode ticked)")]
    [Tooltip("The part of the screen kept bright. Leave empty to auto-find the object with the BoonMeterUI script. It should contain BOTH the container and the liquid.")]
    public GameObject highlightTarget;
    public Color dimColor = new Color(0.08f, 0.08f, 0.1f, 0.8f);
    [Tooltip("Sorting order of the gray overlay. Raised automatically if your HUD canvas is higher.")]
    public int overlaySortingOrder = 900;

    [Header("Player")]
    [Tooltip("Leave empty to auto-find the object tagged 'Player' and its PlayerMovement script.")]
    public Behaviour playerMovement;

    [Header("Default Dialogue (PlayCutscene() with no name, e.g. your intro NPC)")]
    [Tooltip("Which characters appear during the Default Dialogue.")]
    [CharacterName]
    public string[] defaultCast;
    public DialogueLine[] dialogueLines;

    [Header("Cutscenes (played by name: PlayCutscene(\"Id\"))")]
    public CutsceneSequence[] cutscenes = DefaultCutscenes();

    [Header("Start Behaviour")]
    public bool playOnStart = false;

    [Header("Events")]
    public UnityEvent onCutsceneStart;
    public UnityEvent onCutsceneEnd;

    /// <summary>True from the moment PlayCutscene() is called until the closing fade has finished.</summary>
    public bool IsPlaying { get; private set; }

    // What is playing right now
    private DialogueLine[] activeLines;
    private CutsceneSequence activeSequence;
    private bool activeTutorial;
    private string[] activeCast;

    // Per-character runtime state (same index as the Characters list)
    private Vector2[] charBase = new Vector2[0];
    private Color[] charTarget = new Color[0];
    private Coroutine[] charJump = new Coroutine[0];
    private readonly List<int> castIndices = new List<int>();

    private int currentLine;
    private bool acceptingInput;
    private bool isTyping;
    private bool originalMovementEnabled;
    private float inputLockUntil;
    private int lastAdvanceFrame = -1;

    private Coroutine typewriter;

    // Tutorial mode
    private Canvas dimCanvas;
    private Image dimImage;
    private Canvas highlightCanvas;
    private bool addedHighlightCanvas;
    private bool highlightOrigOverride;
    private int highlightOrigOrder;
    private int highlightOrigLayer;
    private int highlightOrder;
    private int highlightLayer;
    private Canvas cutsceneCanvas;

    private class SavedCanvas
    {
        public Canvas canvas;
        public RenderMode mode;
        public Camera camera;
        public float planeDistance;
        public int order;
        public int layer;
    }
    private readonly List<SavedCanvas> savedCanvases = new List<SavedCanvas>();
    private CardShooter pausedShooter;
    private bool shooterWasEnabled;

    // -------------------------------------------------------------------
    // Setup
    // -------------------------------------------------------------------

    // Awake runs the moment this object is enabled, so it is safe for the
    // cutscene object to start disabled and get enabled by the NPC click.
    private void OnValidate()
    {
        MigrateToRoster();
    }

    private void Awake()
    {
        MigrateToRoster();
        FindPlayerMovement();
        SetupCharacterState();

        if (continueButton != null)
        {
            continueButton.onClick.RemoveListener(Advance);
            continueButton.onClick.AddListener(Advance);
        }

        // Make sure nothing from the cutscene is visible until it plays
        HideCutsceneUI();
    }

    private void Start()
    {
        if (playOnStart)
            PlayCutscene();
    }

    private void OnDestroy()
    {
        if (continueButton != null)
            continueButton.onClick.RemoveListener(Advance);
    }

    private void FindPlayerMovement()
    {
        if (playerMovement != null) return;

        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
            playerMovement = player.GetComponent("PlayerMovement") as Behaviour;

        if (playerMovement == null)
            Debug.LogWarning("IntroCutscene: couldn't find the player movement script. Assign it in the Inspector.", this);
    }

    private void StopPlayerMotion()
    {
        if (playerMovement == null) return;

        Rigidbody rb = playerMovement.GetComponent<Rigidbody>();
        if (rb != null) rb.linearVelocity = Vector3.zero;

        Rigidbody2D rb2d = playerMovement.GetComponent<Rigidbody2D>();
        if (rb2d != null) rb2d.linearVelocity = Vector2.zero;
    }

    // -------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------

    /// <summary>Plays the Default Dialogue. (void, so it can be hooked to a Button / UnityEvent.)</summary>
    public void PlayCutscene()
    {
        TryPlayCutscene(null);
    }

    /// <summary>Plays the cutscene with this Id. (void, so it can be hooked to a UnityEvent.)</summary>
    public void PlayCutscene(string id)
    {
        TryPlayCutscene(id);
    }

    /// <summary>
    /// Plays the cutscene with this Id (from the Cutscenes list). An empty Id plays the Default Dialogue.
    /// Returns true if it actually started.
    /// </summary>
    public bool TryPlayCutscene(string id)
    {
        if (IsPlaying) return false;

        DialogueLine[] lines;
        CutsceneSequence sequence = null;

        if (string.IsNullOrEmpty(id))
        {
            lines = dialogueLines;
        }
        else
        {
            sequence = FindSequence(id);
            if (sequence == null)
            {
                Debug.LogWarning("IntroCutscene: no cutscene with the Id '" + id + "'. Check the Cutscenes list.", this);
                return false;
            }
            lines = sequence.lines;
        }

        if (lines == null || lines.Length == 0)
        {
            Debug.LogWarning("IntroCutscene: '" + (string.IsNullOrEmpty(id) ? "Default Dialogue" : id) + "' has no lines.", this);
            return false;
        }

        activeSequence = sequence;
        activeLines = lines;
        activeTutorial = sequence != null && sequence.tutorialMode;
        activeCast = sequence != null ? sequence.castNames : defaultCast;

        IsPlaying = true;
        StartCoroutine(PlayRoutine());
        return true;
    }

    public bool HasCutscene(string id)
    {
        return FindSequence(id) != null;
    }

    /// <summary>
    /// Adds an empty cutscene with this Id (plus one placeholder line to edit) if the Cutscenes list has none.
    /// Used by NPCTrigger in the editor so a new Id gets its entry automatically. Returns true if one was added.
    /// </summary>
    public bool EnsureCutscene(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || HasCutscene(id)) return false;

        var list = new List<CutsceneSequence>();
        if (cutscenes != null) list.AddRange(cutscenes);

        list.Add(new CutsceneSequence
        {
            id = id,
            lines = new[] { new DialogueLine { text = "Write your dialogue here." } }
        });

        cutscenes = list.ToArray();
        return true;
    }

    private CutsceneSequence FindSequence(string id)
    {
        if (cutscenes == null || string.IsNullOrEmpty(id)) return null;

        foreach (CutsceneSequence c in cutscenes)
        {
            if (c != null && string.Equals(c.id, id, System.StringComparison.OrdinalIgnoreCase))
                return c;
        }
        return null;
    }

    private void FinishCutscene()
    {
        IsPlaying = false;
        onCutsceneEnd?.Invoke();
        if (activeSequence != null) activeSequence.onEnd?.Invoke();
    }

    private static CutsceneSequence[] DefaultCutscenes()
    {
        return new[]
        {
            new CutsceneSequence
            {
                id = "Card Tutorial",
                tutorialMode = true,
                lines = new[]
                {
                    Line("Nice find! This Card lets you fight back. Aim with the mouse and left click to throw it at an enemy.", false),
                    Line("A card only flies a short way before it drops, and every throw uses some of your stamina, so make your throws count.", false),
                    Line("Hit an enemy with a card and a card battle begins!", false),
                    Line("Press E to switch between the items you carry. Some trinkets can be thrown or used too.", false),
                    Line("This is your Boon Meter. Every enemy you fight fills it up a little.", true),
                    Line("When it is full, you get to Choose Your Boon: pick one of the upgrades on offer.", true),
                    Line("Boons are permanent. You can gain extra hands per battle, a bigger hand size, or more Wind Power on every hand you play.", true),
                    Line("Any extra meter carries over to the next boon, so keep fighting. Good luck out there!", true)
                }
            }
        };
    }

    private static DialogueLine Line(string text, bool highlightBoon)
    {
        return new DialogueLine { speaker = DialogueSpeaker.Narrator, text = text, highlightBoon = highlightBoon };
    }

    // -------------------------------------------------------------------
    // Main sequence
    // -------------------------------------------------------------------

    private IEnumerator PlayRoutine()
    {
        onCutsceneStart?.Invoke();
        if (activeSequence != null) activeSequence.onStart?.Invoke();

        // Freeze the player right away
        FindPlayerMovement();
        if (playerMovement != null)
        {
            originalMovementEnabled = playerMovement.enabled;
            playerMovement.enabled = false;
            StopPlayerMotion();
        }

        if (activeTutorial)
        {
            // Gray out the screen (boon UI stays bright) and show the dialogue on top
            BeginTutorialMode();
            ShowCutsceneUI();
            yield return FadeDim(true, fadeInDuration);
        }
        else
        {
            // 1. Fade the game out to black
            yield return Fade(1f, fadeOutDuration);

            // 2. Show the cutscene UI (hidden behind the black overlay)
            ShowCutsceneUI();

            // 3. Fade back in to reveal it
            yield return Fade(0f, fadeInDuration);
        }

        // 4. Start the dialogue
        currentLine = 0;
        acceptingInput = true;
        inputLockUntil = Time.unscaledTime + 0.15f;
        ShowLine();
    }

    private IEnumerator EndRoutine()
    {
        acceptingInput = false;

        if (activeTutorial)
        {
            StopAllAnimations();
            HideCutsceneUI();

            yield return FadeDim(false, fadeOutDuration);
            EndTutorialMode();

            if (playerMovement != null)
                playerMovement.enabled = originalMovementEnabled;

            FinishCutscene();
            yield break;
        }

        yield return Fade(1f, fadeOutDuration);

        StopAllAnimations();
        HideCutsceneUI();

        if (playerMovement != null)
            playerMovement.enabled = originalMovementEnabled;

        yield return Fade(0f, fadeInDuration);

        if (fadeOverlay != null)
            fadeOverlay.gameObject.SetActive(false);

        FinishCutscene();
    }

    // -------------------------------------------------------------------
    // Dialogue
    // -------------------------------------------------------------------

    private void ShowLine()
    {
        if (currentLine >= activeLines.Length)
        {
            EndCutscene();
            return;
        }

        DialogueLine line = activeLines[currentLine];

        if (activeTutorial) SetHighlight(line.highlightBoon);

        ShowSpeaker(line);

        if (typewriter != null)
            StopCoroutine(typewriter);

        typewriter = StartCoroutine(TypeLine(line.text));
    }

    private IEnumerator TypeLine(string text)
    {
        isTyping = true;

        if (continueIndicator != null)
            continueIndicator.SetActive(false);

        if (dialogueText != null)
        {
            dialogueText.text = text;
            dialogueText.ForceMeshUpdate();

            int total = dialogueText.textInfo.characterCount;

            if (charsPerSecond > 0f && total > 0)
            {
                dialogueText.maxVisibleCharacters = 0;

                float elapsed = 0f;
                float tickTimer = talkTickInterval; // makes the first tick play right away
                int lastVisible = 0;

                while (dialogueText.maxVisibleCharacters < total)
                {
                    float dt = Time.unscaledDeltaTime;
                    elapsed += dt;
                    tickTimer += dt;

                    int visible = Mathf.Min(total, Mathf.FloorToInt(elapsed * charsPerSecond));
                    dialogueText.maxVisibleCharacters = visible;

                    // Tick at a steady interval, only while new characters are appearing
                    if (visible > lastVisible && tickTimer >= talkTickInterval)
                    {
                        tickTimer = 0f;
                        PlayTalkTick();
                    }
                    lastVisible = visible;

                    yield return null;
                }
            }
        }

        FinishTyping();
    }

    private void PlayTalkTick()
    {
        if (talkSound == null) return;

        float pitch = 1f + Random.Range(-talkPitchVariation, talkPitchVariation);
        GameSFX.Play(talkSound, talkVolume, pitch);
    }

    private void FinishTyping()
    {
        isTyping = false;
        typewriter = null;

        if (dialogueText != null)
            dialogueText.maxVisibleCharacters = int.MaxValue;

        if (continueIndicator != null)
            continueIndicator.SetActive(true);
    }

    /// <summary>Called by the continue button, Space or Enter.</summary>
    public void Advance()
    {
        if (!acceptingInput || Time.unscaledTime < inputLockUntil)
            return;

        // Space can fire both our key check and the selected button in the same frame
        if (lastAdvanceFrame == Time.frameCount)
            return;
        lastAdvanceFrame = Time.frameCount;

        // First press: finish the line instantly
        if (isTyping)
        {
            if (typewriter != null)
                StopCoroutine(typewriter);

            FinishTyping();
            return;
        }

        // Second press: go to the next line
        currentLine++;
        ShowLine();
    }

    private void EndCutscene()
    {
        if (!acceptingInput) return;

        acceptingInput = false;
        StartCoroutine(EndRoutine());
    }

    private void SetName(string value)
    {
        if (narratorName != null)
            narratorName.text = value;
    }

    private void Update()
    {
        if (!IsPlaying) return;

        // Smoothly brighten the speaker and dim everyone else
        float t = 1f - Mathf.Exp(-lightingSpeed * Time.unscaledDeltaTime);

        foreach (int i in castIndices)
        {
            Image img = characters[i].image;
            if (img != null)
                img.color = Color.Lerp(img.color, charTarget[i], t);
        }

        if (acceptingInput && AdvancePressed())
            Advance();
    }

    // Works with the old Input Manager, the new Input System, or both
    private bool AdvancePressed()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        var kb = UnityEngine.InputSystem.Keyboard.current;
        return kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.Space)
            || Input.GetKeyDown(KeyCode.Return)
            || Input.GetKeyDown(KeyCode.KeypadEnter);
#endif
    }

    // -------------------------------------------------------------------
    // Characters
    // -------------------------------------------------------------------

    private IEnumerator JumpCharacter(RectTransform character, Vector2 originalPosition)
    {
        float elapsed = 0f;

        while (elapsed < jumpDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float progress = Mathf.Clamp01(elapsed / jumpDuration);
            float height = Mathf.Sin(progress * Mathf.PI) * jumpHeight;

            character.anchoredPosition = originalPosition + Vector2.up * height;
            yield return null;
        }

        character.anchoredPosition = originalPosition;
    }

    // -------------------------------------------------------------------
    // Show / hide
    // -------------------------------------------------------------------

    // -------------------------------------------------------------------
    // Characters / cast
    // -------------------------------------------------------------------

    /// <summary>Moves old Fable / Bee images and old speaker settings into the Characters list (runs once).</summary>
    private void MigrateToRoster()
    {
        if (characters == null) characters = new List<CutsceneActor>();
        if (migratedToRoster) return;
        migratedToRoster = true;

        AddToRoster(fableName, fableImage);
        AddToRoster(beeName, beeImage);

        if ((defaultCast == null || defaultCast.Length == 0) && (fableImage != null || beeImage != null))
        {
            var names = new List<string>();
            if (fableImage != null) names.Add(fableName);
            if (beeImage != null) names.Add(beeName);
            defaultCast = names.ToArray();
        }

        ConvertLines(dialogueLines);

        if (cutscenes != null)
        {
            foreach (CutsceneSequence seq in cutscenes)
            {
                if (seq == null || seq.castMode != CastMode.DefaultCharacters) continue;

                if (seq.castNames == null || seq.castNames.Length == 0)
                {
                    var names = new List<string>();
                    if (fableImage != null) names.Add(fableName);
                    if (beeImage != null) names.Add(beeName);
                    seq.castNames = names.ToArray();
                }
                ConvertLines(seq.lines);
                seq.castMode = CastMode.CustomCast;
            }
        }
    }

    private void AddToRoster(string actorName, Image image)
    {
        if (image == null || string.IsNullOrEmpty(actorName)) return;

        foreach (CutsceneActor a in characters)
        {
            if (a != null && string.Equals(a.name, actorName, System.StringComparison.OrdinalIgnoreCase))
            {
                if (a.image == null) a.image = image;
                return;
            }
        }
        characters.Add(new CutsceneActor { name = actorName, image = image });
    }

    private void ConvertLines(DialogueLine[] lines)
    {
        if (lines == null) return;
        foreach (DialogueLine l in lines)
        {
            if (l == null || !string.IsNullOrEmpty(l.speakerName)) continue;
            if (l.speaker == DialogueSpeaker.Fable) l.speakerName = fableName;
            else if (l.speaker == DialogueSpeaker.Bee) l.speakerName = beeName;
        }
    }

    private void SetupCharacterState()
    {
        int n = characters != null ? characters.Count : 0;
        charBase = new Vector2[n];
        charTarget = new Color[n];
        charJump = new Coroutine[n];

        for (int i = 0; i < n; i++)
        {
            charTarget[i] = inactiveColor;
            if (characters[i] != null && characters[i].image != null)
                charBase[i] = characters[i].image.rectTransform.anchoredPosition;
        }
    }

    private int FindCharacter(string actorName)
    {
        if (string.IsNullOrEmpty(actorName) || characters == null) return -1;

        for (int i = 0; i < characters.Count; i++)
        {
            if (characters[i] != null && string.Equals(characters[i].name, actorName, System.StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }

    private void ShowSpeaker(DialogueLine line)
    {
        int speaker = -1;
        foreach (int i in castIndices)
        {
            if (string.Equals(characters[i].name, line.speakerName, System.StringComparison.OrdinalIgnoreCase))
            {
                speaker = i;
                break;
            }
        }

        // No speaker picked on the line, but only one character is in the cast: that character is talking
        if (speaker < 0 && string.IsNullOrEmpty(line.speakerName) && castIndices.Count == 1)
            speaker = castIndices[0];

        if (speaker >= 0)
        {
            SetName(characters[speaker].name);
            if (line.expression != null) characters[speaker].image.sprite = line.expression;
        }
        else
        {
            SetName(string.IsNullOrEmpty(line.speakerName) ? narratorNameText : line.speakerName);
        }

        foreach (int i in castIndices)
            charTarget[i] = i == speaker ? activeColor : inactiveColor;

        if (speaker >= 0)
        {
            RectTransform rt = characters[speaker].image.rectTransform;
            if (charJump[speaker] != null) StopCoroutine(charJump[speaker]);
            rt.anchoredPosition = charBase[speaker];
            charJump[speaker] = StartCoroutine(JumpCharacter(rt, charBase[speaker]));
        }
    }

    private void ShowCutsceneUI()
    {
        // In tutorial mode the game must stay visible (grayed), so the opaque background is skipped
        if (dialogueBackground != null && !activeTutorial) dialogueBackground.SetActive(true);
        if (dialoguePanel != null) dialoguePanel.SetActive(true);

        int count = characters != null ? characters.Count : 0;
        if (charBase.Length != count) SetupCharacterState();

        // Work out who appears in this cutscene
        castIndices.Clear();
        if (activeCast != null)
        {
            foreach (string actorName in activeCast)
            {
                if (string.IsNullOrEmpty(actorName)) continue;

                int idx = FindCharacter(actorName);
                if (idx < 0 || characters[idx].image == null)
                {
                    Debug.LogWarning("IntroCutscene: '" + actorName + "' is in the cast but is not in the Characters list (or has no Image).", this);
                    continue;
                }
                if (!castIndices.Contains(idx)) castIndices.Add(idx);
            }
        }

        // Show the cast (dimmed; the first line lights up the speaker), hide everyone else
        for (int i = 0; i < count; i++)
        {
            Image img = characters[i] != null ? characters[i].image : null;
            if (img == null) continue;

            bool appears = castIndices.Contains(i);
            img.gameObject.SetActive(appears);
            if (appears)
            {
                charTarget[i] = inactiveColor;
                img.color = inactiveColor;
                img.rectTransform.anchoredPosition = charBase[i];
            }
        }

        if (dialogueText != null) dialogueText.text = "";
        if (narratorName != null) narratorName.text = "";
        if (continueIndicator != null) continueIndicator.SetActive(false);
    }

    private void HideCutsceneUI()
    {
        castIndices.Clear();

        int count = characters != null ? characters.Count : 0;
        for (int i = 0; i < count; i++)
        {
            Image img = characters[i] != null ? characters[i].image : null;
            if (img == null) continue;

            if (i < charBase.Length) img.rectTransform.anchoredPosition = charBase[i];
            img.gameObject.SetActive(false);
        }

        if (dialoguePanel != null) dialoguePanel.SetActive(false);
        if (dialogueBackground != null) dialogueBackground.SetActive(false);
        if (continueIndicator != null) continueIndicator.SetActive(false);
    }

    private void StopAllAnimations()
    {
        if (typewriter != null) StopCoroutine(typewriter);

        for (int i = 0; i < charJump.Length; i++)
        {
            if (charJump[i] != null) StopCoroutine(charJump[i]);
            charJump[i] = null;
        }

        typewriter = null;
        isTyping = false;
    }

    // -------------------------------------------------------------------
    // Tutorial mode (gray-out + highlight)
    // -------------------------------------------------------------------

    private void BeginTutorialMode()
    {
        if (highlightTarget == null)
        {
            BoonMeterUI meter = FindAnyObjectByType<BoonMeterUI>();
            if (meter != null) highlightTarget = meter.gameObject;
        }

        Canvas cc = (dialoguePanel != null ? dialoguePanel : gameObject).GetComponentInParent<Canvas>(true);
        cutsceneCanvas = cc != null ? cc.rootCanvas : null;

        Canvas boonRoot = highlightTarget != null ? highlightTarget.GetComponentInParent<Canvas>() : null;
        if (boonRoot != null) boonRoot = boonRoot.rootCanvas;

        // Everything involved is switched to Screen Space - Overlay for the duration (and restored after),
        // so no other canvas (like the HUD) can ever draw above the gray screen.
        ForceOverlay(boonRoot);
        ForceOverlay(cutsceneCanvas);

        // Find the topmost overlay canvas in the scene (including nested ones that override sorting)
        bool found = false;
        int bestValue = 0, bestLayerId = 0, bestOrder = 0;
        foreach (Canvas c in FindObjectsByType<Canvas>(FindObjectsInactive.Exclude))
        {
            if (c == cutsceneCanvas) continue;
            if (!c.isRootCanvas && !c.overrideSorting) continue;
            if (c.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;

            int value = SortingLayer.GetLayerValueFromID(c.sortingLayerID);
            if (!found || value > bestValue || (value == bestValue && c.sortingOrder > bestOrder))
            {
                found = true;
                bestValue = value;
                bestLayerId = c.sortingLayerID;
                bestOrder = c.sortingOrder;
            }
        }

        int baseOrder = Mathf.Max(overlaySortingOrder, found ? bestOrder + 1 : 0);
        highlightLayer = found ? bestLayerId : 0;
        highlightOrder = baseOrder + 1;

        // Gray overlay (blocks clicks on the game behind it)
        GameObject dimGo = new GameObject("TutorialDim");
        dimCanvas = dimGo.AddComponent<Canvas>();
        dimCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        dimCanvas.sortingLayerID = highlightLayer;
        dimCanvas.sortingOrder = baseOrder;
        dimGo.AddComponent<GraphicRaycaster>();

        GameObject imgGo = new GameObject("Dim", typeof(RectTransform));
        imgGo.transform.SetParent(dimGo.transform, false);
        RectTransform rt = imgGo.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        dimImage = imgGo.AddComponent<Image>();
        dimImage.color = new Color(dimColor.r, dimColor.g, dimColor.b, 0f);
        dimImage.raycastTarget = true;

        // The cutscene's own canvas goes above the highlighted boon UI
        if (cutsceneCanvas != null)
        {
            cutsceneCanvas.sortingLayerID = highlightLayer;
            cutsceneCanvas.sortingOrder = baseOrder + 2;
        }

        // Prepare the boon UI to be raised above the overlay
        if (highlightTarget != null)
        {
            highlightCanvas = highlightTarget.GetComponent<Canvas>();
            if (highlightCanvas == null)
            {
                highlightCanvas = highlightTarget.AddComponent<Canvas>();
                addedHighlightCanvas = true;
            }
            else
            {
                highlightOrigOverride = highlightCanvas.overrideSorting;
                highlightOrigOrder = highlightCanvas.sortingOrder;
                highlightOrigLayer = highlightCanvas.sortingLayerID;
            }
        }
        else
        {
            Debug.LogWarning("IntroCutscene: tutorial mode found no Boon UI to highlight. Assign Highlight Target.", this);
        }

        SetHighlight(activeLines[0].highlightBoon);

        // Clicking to continue must never throw a card
        pausedShooter = FindAnyObjectByType<CardShooter>();
        if (pausedShooter != null)
        {
            shooterWasEnabled = pausedShooter.enabled;
            pausedShooter.enabled = false;
        }
    }

    private void ForceOverlay(Canvas c)
    {
        if (c == null) return;

        foreach (SavedCanvas saved in savedCanvases)
            if (saved.canvas == c) return;

        savedCanvases.Add(new SavedCanvas
        {
            canvas = c,
            mode = c.renderMode,
            camera = c.worldCamera,
            planeDistance = c.planeDistance,
            order = c.sortingOrder,
            layer = c.sortingLayerID
        });

        if (c.renderMode != RenderMode.ScreenSpaceOverlay)
            c.renderMode = RenderMode.ScreenSpaceOverlay;
    }

    private void SetHighlight(bool on)
    {
        if (highlightCanvas == null) return;

        if (on)
        {
            highlightCanvas.overrideSorting = true;
            highlightCanvas.sortingLayerID = highlightLayer;
            highlightCanvas.sortingOrder = highlightOrder;
        }
        else if (addedHighlightCanvas)
        {
            highlightCanvas.overrideSorting = false;
        }
        else
        {
            highlightCanvas.overrideSorting = highlightOrigOverride;
            highlightCanvas.sortingLayerID = highlightOrigLayer;
            highlightCanvas.sortingOrder = highlightOrigOrder;
        }
    }

    private void EndTutorialMode()
    {
        if (highlightCanvas != null)
        {
            if (addedHighlightCanvas)
            {
                Destroy(highlightCanvas);
            }
            else
            {
                highlightCanvas.overrideSorting = highlightOrigOverride;
                highlightCanvas.sortingLayerID = highlightOrigLayer;
                highlightCanvas.sortingOrder = highlightOrigOrder;
            }
        }
        highlightCanvas = null;
        addedHighlightCanvas = false;

        // Put the boon / cutscene canvases back exactly how they were
        foreach (SavedCanvas saved in savedCanvases)
        {
            if (saved.canvas == null) continue;

            saved.canvas.renderMode = saved.mode;
            if (saved.mode == RenderMode.ScreenSpaceCamera)
            {
                saved.canvas.worldCamera = saved.camera;
                saved.canvas.planeDistance = saved.planeDistance;
            }
            saved.canvas.sortingLayerID = saved.layer;
            saved.canvas.sortingOrder = saved.order;
        }
        savedCanvases.Clear();
        cutsceneCanvas = null;

        if (dimCanvas != null) Destroy(dimCanvas.gameObject);
        dimCanvas = null;
        dimImage = null;

        if (pausedShooter != null) pausedShooter.enabled = shooterWasEnabled;
        pausedShooter = null;
    }

    private IEnumerator FadeDim(bool show, float duration)
    {
        if (dimImage == null) yield break;

        float from = show ? 0f : dimColor.a;
        float to = show ? dimColor.a : 0f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Mathf.Min(Time.unscaledDeltaTime, 0.033f);
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = t * t * (3f - 2f * t);

            Color c = dimColor;
            c.a = Mathf.Lerp(from, to, eased);
            dimImage.color = c;
            yield return null;
        }

        Color end = dimColor;
        end.a = to;
        dimImage.color = end;
    }

    // -------------------------------------------------------------------
    // Fade
    // -------------------------------------------------------------------

    private IEnumerator Fade(float targetAlpha, float duration)
    {
        if (fadeOverlay == null)
            yield break;

        fadeOverlay.gameObject.SetActive(true);
        fadeOverlay.raycastTarget = true; // block clicks while fading

        float startAlpha = fadeOverlay.color.a;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            // Clamp the step so one slow frame can't make the fade jump
            elapsed += Mathf.Min(Time.unscaledDeltaTime, 0.033f);

            float t = Mathf.Clamp01(elapsed / duration);
            float eased = t * t * t * (t * (t * 6f - 15f) + 10f); // smootherstep

            SetOverlayAlpha(Mathf.Lerp(startAlpha, targetAlpha, eased));
            yield return null;
        }

        SetOverlayAlpha(targetAlpha);

        if (targetAlpha <= 0f)
            fadeOverlay.raycastTarget = false;
    }

    private void SetOverlayAlpha(float alpha)
    {
        Color c = fadeOverlay.color;
        c.a = alpha;
        fadeOverlay.color = c;
    }
}