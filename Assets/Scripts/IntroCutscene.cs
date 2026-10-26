
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[System.Serializable]
public class DialogueLine
{
    public string speaker;

    [TextArea(2, 4)]
    public string text;
}

public class IntroCutscene : MonoBehaviour
{
    [Header("Dialogue UI")]
    public GameObject dialoguePanel;
    public Image narratorPortrait;
    public TMP_Text narratorName;
    public TMP_Text dialogueText;
    public Button continueButton;

    [Header("Speaker Portraits")]
    public Sprite girlSprite;
    public Sprite narratorSprite;

    [Header("Player")]
    public Behaviour playerMovement;

    [Header("Dialogue Lines")]
    public DialogueLine[] dialogueLines;

    private int currentLine;
    private bool isActive;
    private bool originalMovementEnabled;

    private void Start()
    {
        if (playerMovement != null)
        {
            originalMovementEnabled = playerMovement.enabled;
            playerMovement.enabled = false;
        }

        dialoguePanel.SetActive(true);

        if (continueButton != null)
        {
            continueButton.onClick.RemoveListener(NextLine);
            continueButton.onClick.AddListener(NextLine);
        }

        if (dialogueLines != null && dialogueLines.Length > 0)
        {
            currentLine = 0;
            isActive = true;
            ShowLine();
        }
        else
        {
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
        narratorName.text = line.speaker;

        if (narratorPortrait != null)
        {
            if (line.speaker.ToLower() == "girl")
            {
                narratorPortrait.sprite = girlSprite;
            }
            else if (line.speaker.ToLower() == "narrator")
            {
                narratorPortrait.sprite = narratorSprite;
            }
        }
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
        isActive = false;

        if (dialoguePanel != null)
            dialoguePanel.SetActive(false);

        if (playerMovement != null)
            playerMovement.enabled = originalMovementEnabled;
    }

    private void OnDestroy()
    {
        if (continueButton != null)
            continueButton.onClick.RemoveListener(NextLine);
    }
}
