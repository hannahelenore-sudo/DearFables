using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

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

public class IntroCutscene : MonoBehaviour
{
    [Header("Dialogue UI")]
    public GameObject dialoguePanel;
    public TMP_Text narratorName;
    public TMP_Text dialogueText;
    public Button continueButton;

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

    [Header("Jump Animation")]
    public float jumpHeight = 30f;
    public float jumpDuration = 0.3f;

    [Header("Fade Transition")]
    public Image fadeOverlay;
    public float fadeInDuration = 1f;
    public float fadeOutDuration = 1f;

    [Header("Player")]
    public Behaviour playerMovement;

    [Header("Dialogue Lines")]
    public DialogueLine[] dialogueLines;

    private int currentLine;
    private bool isActive;
    private bool originalMovementEnabled;

    private Vector2 fableOriginalPosition;
    private Vector2 beeOriginalPosition;

    private Coroutine fableJump;
    private Coroutine beeJump;

    private IEnumerator Start()
    {
        if (fableImage != null)
            fableOriginalPosition = fableImage.rectTransform.anchoredPosition;

        if (beeImage != null)
            beeOriginalPosition = beeImage.rectTransform.anchoredPosition;

        if (playerMovement != null)
        {
            originalMovementEnabled = playerMovement.enabled;
            playerMovement.enabled = false;
        }

        if (fadeOverlay != null)
        {
            fadeOverlay.gameObject.SetActive(true);

            Color color = fadeOverlay.color;
            color.a = 1f;
            fadeOverlay.color = color;
        }

        if (dialogueBackground != null)
            dialogueBackground.SetActive(true);

        if (dialoguePanel != null)
            dialoguePanel.SetActive(true);

        if (fableImage != null)
            fableImage.gameObject.SetActive(true);

        if (beeImage != null)
            beeImage.gameObject.SetActive(true);

        if (continueButton != null)
        {
            continueButton.onClick.RemoveListener(NextLine);
            continueButton.onClick.AddListener(NextLine);
        }

        if (dialogueLines != null && dialogueLines.Length > 0)
        {
            currentLine = 0;
            ShowLine();

            yield return StartCoroutine(Fade(0f, fadeInDuration));

            isActive = true;
        }
        else
        {
            yield return StartCoroutine(Fade(0f, fadeInDuration));
            EndCutscene();
        }
    }

    private void ShowLine()
    {
        if (currentLine >= dialogueLines.Length)
        {
            EndCutscene();
            return;
        }

        DialogueLine line = dialogueLines[currentLine];

        dialogueText.text = line.text;

        switch (line.speaker)
        {
            case DialogueSpeaker.Fable:
                narratorName.text = fableName;

                if (line.expression != null && fableImage != null)
                    fableImage.sprite = line.expression;

                break;

            case DialogueSpeaker.Bee:
                narratorName.text = beeName;

                if (line.expression != null && beeImage != null)
                    beeImage.sprite = line.expression;

                break;

            case DialogueSpeaker.Narrator:
                narratorName.text = narratorNameText;
                break;
        }

        UpdateCharacterLighting(line.speaker);
    }

    private void UpdateCharacterLighting(DialogueSpeaker speaker)
    {
        if (fableImage != null)
        {
            fableImage.color = speaker == DialogueSpeaker.Fable
                ? activeColor
                : inactiveColor;
        }

        if (beeImage != null)
        {
            beeImage.color = speaker == DialogueSpeaker.Bee
                ? activeColor
                : inactiveColor;
        }

        if (speaker == DialogueSpeaker.Fable && fableImage != null)
        {
            if (fableJump != null)
                StopCoroutine(fableJump);

            fableImage.rectTransform.anchoredPosition = fableOriginalPosition;

            fableJump = StartCoroutine(JumpCharacter(
                fableImage.rectTransform,
                fableOriginalPosition
            ));
        }
        else if (speaker == DialogueSpeaker.Bee && beeImage != null)
        {
            if (beeJump != null)
                StopCoroutine(beeJump);

            beeImage.rectTransform.anchoredPosition = beeOriginalPosition;

            beeJump = StartCoroutine(JumpCharacter(
                beeImage.rectTransform,
                beeOriginalPosition
            ));
        }
    }

    private IEnumerator JumpCharacter(RectTransform character, Vector2 originalPosition)
    {
        float elapsed = 0f;

        while (elapsed < jumpDuration)
        {
            elapsed += Time.deltaTime;

            float progress = Mathf.Clamp01(elapsed / jumpDuration);

            float height = Mathf.Sin(progress * Mathf.PI) * jumpHeight;

            character.anchoredPosition = originalPosition + Vector2.up * height;

            yield return null;
        }

        character.anchoredPosition = originalPosition;
    }

    private IEnumerator Fade(float targetAlpha, float duration)
    {
        if (fadeOverlay == null)
            yield break;

        float startAlpha = fadeOverlay.color.a;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float progress = Mathf.Clamp01(elapsed / duration);

            Color color = fadeOverlay.color;
            color.a = Mathf.Lerp(startAlpha, targetAlpha, progress);
            fadeOverlay.color = color;

            yield return null;
        }

        Color finalColor = fadeOverlay.color;
        finalColor.a = targetAlpha;
        fadeOverlay.color = finalColor;
    }

    public void NextLine()
    {
        if (!isActive)
            return;

        currentLine++;
        ShowLine();
    }

    private void Update()
    {
        if (isActive && Input.GetKeyDown(KeyCode.Space))
        {
            NextLine();
        }
    }

    private void EndCutscene()
    {
        if (!isActive && currentLine < 0)
            return;

        isActive = false;

        StartCoroutine(EndCutsceneSequence());
    }

    private IEnumerator EndCutsceneSequence()
    {
        yield return StartCoroutine(Fade(1f, fadeOutDuration));

        if (fableJump != null)
            StopCoroutine(fableJump);

        if (beeJump != null)
            StopCoroutine(beeJump);

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

        if (dialoguePanel != null)
            dialoguePanel.SetActive(false);

        if (dialogueBackground != null)
            dialogueBackground.SetActive(false);

        if (playerMovement != null)
            playerMovement.enabled = originalMovementEnabled;

        yield return StartCoroutine(Fade(0f, fadeInDuration));

        if (fadeOverlay != null)
            fadeOverlay.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (continueButton != null)
            continueButton.onClick.RemoveListener(NextLine);
    }
}