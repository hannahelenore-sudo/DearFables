using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

public enum DialogueSpeaker
{
    Fable,
    Bee,
    Narrator
}

[System.Serializable]
public class DialogueLine
{
    public DialogueSpeaker speaker;

    [TextArea(2, 4)]
    public string text;

    [Header("Expression")]
    public Sprite expression;
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

    [Header("Character Images")]
    public Image fableImage;
    public Image beeImage;

    [Header("Character Display Names")]
    public string fableName = "Fable";
    public string beeName = "Bee";
    public string narratorNameText = "Narrator";

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

    [Header("Player")]
    [Tooltip("Leave empty to auto-find the object tagged 'Player' and its PlayerMovement script.")]
    public Behaviour playerMovement;

    [Header("Dialogue Lines")]
    public DialogueLine[] dialogueLines;

    [Header("Start Behaviour")]
    public bool playOnStart = false;

    [Header("Events")]
    public UnityEvent onCutsceneStart;
    public UnityEvent onCutsceneEnd;

    /// <summary>True from the moment PlayCutscene() is called until the closing fade has finished.</summary>
    public bool IsPlaying { get; private set; }

    private int currentLine;
    private bool acceptingInput;
    private bool isTyping;
    private bool originalMovementEnabled;
    private float inputLockUntil;
    private int lastAdvanceFrame = -1;

    private Vector2 fableOriginalPosition;
    private Vector2 beeOriginalPosition;
    private Color fableTargetColor;
    private Color beeTargetColor;

    private Coroutine fableJump;
    private Coroutine beeJump;
    private Coroutine typewriter;

    // -------------------------------------------------------------------
    // Setup
    // -------------------------------------------------------------------

    // Awake runs the moment this object is enabled, so it is safe for the
    // cutscene object to start disabled and get enabled by the NPC click.
    private void Awake()
    {
        FindPlayerMovement();

        if (fableImage != null)
            fableOriginalPosition = fableImage.rectTransform.anchoredPosition;

        if (beeImage != null)
            beeOriginalPosition = beeImage.rectTransform.anchoredPosition;

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

    public void PlayCutscene()
    {
        if (IsPlaying) return;

        if (dialogueLines == null || dialogueLines.Length == 0)
        {
            Debug.LogWarning("IntroCutscene: Dialogue Lines is empty, nothing to play.", this);
            return;
        }

        IsPlaying = true;
        StartCoroutine(PlayRoutine());
    }

    // -------------------------------------------------------------------
    // Main sequence
    // -------------------------------------------------------------------

    private IEnumerator PlayRoutine()
    {
        onCutsceneStart?.Invoke();

        // Freeze the player right away
        FindPlayerMovement();
        if (playerMovement != null)
        {
            originalMovementEnabled = playerMovement.enabled;
            playerMovement.enabled = false;
            StopPlayerMotion();
        }

        // 1. Fade the game out to black
        yield return Fade(1f, fadeOutDuration);

        // 2. Show the cutscene UI (hidden behind the black overlay)
        ShowCutsceneUI();

        // 3. Fade back in to reveal it
        yield return Fade(0f, fadeInDuration);

        // 4. Start the dialogue
        currentLine = 0;
        acceptingInput = true;
        inputLockUntil = Time.unscaledTime + 0.15f;
        ShowLine();
    }

    private IEnumerator EndRoutine()
    {
        acceptingInput = false;

        yield return Fade(1f, fadeOutDuration);

        StopAllAnimations();
        HideCutsceneUI();

        if (playerMovement != null)
            playerMovement.enabled = originalMovementEnabled;

        yield return Fade(0f, fadeInDuration);

        if (fadeOverlay != null)
            fadeOverlay.gameObject.SetActive(false);

        IsPlaying = false;
        onCutsceneEnd?.Invoke();
    }

    // -------------------------------------------------------------------
    // Dialogue
    // -------------------------------------------------------------------

    private void ShowLine()
    {
        if (currentLine >= dialogueLines.Length)
        {
            EndCutscene();
            return;
        }

        DialogueLine line = dialogueLines[currentLine];

        switch (line.speaker)
        {
            case DialogueSpeaker.Fable:
                SetName(fableName);
                if (line.expression != null && fableImage != null)
                    fableImage.sprite = line.expression;
                break;

            case DialogueSpeaker.Bee:
                SetName(beeName);
                if (line.expression != null && beeImage != null)
                    beeImage.sprite = line.expression;
                break;

            default:
                SetName(narratorNameText);
                break;
        }

        UpdateCharacterLighting(line.speaker);

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

        if (fableImage != null)
            fableImage.color = Color.Lerp(fableImage.color, fableTargetColor, t);

        if (beeImage != null)
            beeImage.color = Color.Lerp(beeImage.color, beeTargetColor, t);

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

    private void UpdateCharacterLighting(DialogueSpeaker speaker)
    {
        fableTargetColor = speaker == DialogueSpeaker.Fable ? activeColor : inactiveColor;
        beeTargetColor = speaker == DialogueSpeaker.Bee ? activeColor : inactiveColor;

        if (speaker == DialogueSpeaker.Fable && fableImage != null)
        {
            if (fableJump != null) StopCoroutine(fableJump);
            fableImage.rectTransform.anchoredPosition = fableOriginalPosition;
            fableJump = StartCoroutine(JumpCharacter(fableImage.rectTransform, fableOriginalPosition));
        }
        else if (speaker == DialogueSpeaker.Bee && beeImage != null)
        {
            if (beeJump != null) StopCoroutine(beeJump);
            beeImage.rectTransform.anchoredPosition = beeOriginalPosition;
            beeJump = StartCoroutine(JumpCharacter(beeImage.rectTransform, beeOriginalPosition));
        }
    }

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

    private void ShowCutsceneUI()
    {
        if (dialogueBackground != null) dialogueBackground.SetActive(true);
        if (dialoguePanel != null) dialoguePanel.SetActive(true);

        // Start both characters dimmed; the first line will light up the speaker
        fableTargetColor = inactiveColor;
        beeTargetColor = inactiveColor;

        if (fableImage != null)
        {
            fableImage.gameObject.SetActive(true);
            fableImage.color = inactiveColor;
            fableImage.rectTransform.anchoredPosition = fableOriginalPosition;
        }

        if (beeImage != null)
        {
            beeImage.gameObject.SetActive(true);
            beeImage.color = inactiveColor;
            beeImage.rectTransform.anchoredPosition = beeOriginalPosition;
        }

        if (dialogueText != null) dialogueText.text = "";
        if (narratorName != null) narratorName.text = "";
        if (continueIndicator != null) continueIndicator.SetActive(false);
    }

    private void HideCutsceneUI()
    {
        if (fableImage != null)
        {
            fableImage.rectTransform.anchoredPosition = fableOriginalPosition;
            fableImage.gameObject.SetActive(false);
        }

        if (beeImage != null)
        {
            beeImage.rectTransform.anchoredPosition = beeOriginalPosition;
            beeImage.gameObject.SetActive(false);
        }

        if (dialoguePanel != null) dialoguePanel.SetActive(false);
        if (dialogueBackground != null) dialogueBackground.SetActive(false);
        if (continueIndicator != null) continueIndicator.SetActive(false);
    }

    private void StopAllAnimations()
    {
        if (typewriter != null) StopCoroutine(typewriter);
        if (fableJump != null) StopCoroutine(fableJump);
        if (beeJump != null) StopCoroutine(beeJump);

        typewriter = null;
        fableJump = null;
        beeJump = null;
        isTyping = false;
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