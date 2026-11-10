using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// The shopkeeper's dialogue box: a name plus a line of text that types out with talking ticks
/// (like the intro cutscene, but with no character sprites, fades or button presses).
/// TrinketShopUI calls SayGreeting / SayBought / SayReshuffle. You can also call Say("...") yourself.
/// </summary>
public class ShopkeeperDialogue : MonoBehaviour
{
    [Header("Dialogue UI")]
    [Tooltip("The box. Hidden until the shopkeeper says something. Optional - if empty only the texts are used.")]
    public GameObject dialogueBox;
    public TMP_Text nameText;
    public TMP_Text dialogueText;
    public string shopkeeperName = "Shopkeeper";

    [Header("Typewriter")]
    [Tooltip("Characters per second. 0 = show the whole line instantly.")]
    public float charsPerSecond = 45f;

    [Header("Talking Sound")]
    public AudioClip talkSound;
    [Range(0f, 1f)] public float talkVolume = 1f;
    [Tooltip("Seconds between each tick while the text is typing.")]
    public float talkTickInterval = 0.08f;
    [Range(0f, 0.3f)] public float talkPitchVariation = 0.05f;

    [Header("Lines (one is picked at random)")]
    [TextArea(1, 3)]
    public string[] greetingLines =
    {
        "Psst! Come take a look at what I've got!",
        "Welcome, welcome! Have a look around the trunk.",
        "Ah, a customer! Take your pick!"
    };
    [TextArea(1, 3)]
    [Tooltip("{item} is replaced with the trinket's name.")]
    public string[] boughtLines =
    {
        "Excellent choice! The {item} is all yours.",
        "Ooh, the {item}! You have good taste.",
        "Take good care of that {item}!"
    };
    [TextArea(1, 3)]
    public string[] reshuffleLines =
    {
        "Hold on, let me dig through the trunk again...",
        "Fresh stock, coming right up!",
        "I've got plenty more in the back!"
    };

    public bool IsTalking { get; private set; }

    private Coroutine typewriter;
    private string lastLine;

    private void Awake()
    {
        if (nameText != null) nameText.text = shopkeeperName;
        if (dialogueBox != null) dialogueBox.SetActive(false);
    }

    // -------------------------------------------------------------------
    // Ready-made reactions
    // -------------------------------------------------------------------

    public void SayGreeting() { SayRandom(greetingLines, null); }
    public void SayBought(string itemName) { SayRandom(boughtLines, itemName); }
    public void SayReshuffle() { SayRandom(reshuffleLines, null); }

    // -------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------

    public void SayRandom(string[] pool, string itemName)
    {
        if (pool == null || pool.Length == 0) return;

        // Pick a random line, but not the same one twice in a row
        string line = pool[Random.Range(0, pool.Length)];
        for (int tries = 0; tries < 5 && pool.Length > 1 && line == lastLine; tries++)
            line = pool[Random.Range(0, pool.Length)];
        lastLine = line;

        if (!string.IsNullOrEmpty(itemName)) line = line.Replace("{item}", itemName);
        Say(line);
    }

    public void Say(string text)
    {
        if (dialogueText == null || !gameObject.activeInHierarchy) return;

        if (dialogueBox != null) dialogueBox.SetActive(true);
        if (nameText != null) nameText.text = shopkeeperName;

        if (typewriter != null) StopCoroutine(typewriter);
        typewriter = StartCoroutine(TypeLine(text));
    }

    /// <summary>Stops talking and hides the box (called when the shop closes).</summary>
    public void Clear()
    {
        if (typewriter != null) StopCoroutine(typewriter);
        typewriter = null;
        IsTalking = false;

        if (dialogueText != null) dialogueText.text = "";
        if (dialogueBox != null) dialogueBox.SetActive(false);
    }

    // -------------------------------------------------------------------
    // Typewriter (same idea as IntroCutscene)
    // -------------------------------------------------------------------

    private IEnumerator TypeLine(string text)
    {
        IsTalking = true;

        dialogueText.text = text;
        dialogueText.ForceMeshUpdate();

        int total = dialogueText.textInfo.characterCount;

        if (charsPerSecond > 0f && total > 0)
        {
            dialogueText.maxVisibleCharacters = 0;

            float elapsed = 0f;
            float tickTimer = talkTickInterval; // first tick plays right away
            int lastVisible = 0;

            while (dialogueText.maxVisibleCharacters < total)
            {
                float dt = Time.unscaledDeltaTime;
                elapsed += dt;
                tickTimer += dt;

                int visible = Mathf.Min(total, Mathf.FloorToInt(elapsed * charsPerSecond));
                dialogueText.maxVisibleCharacters = visible;

                if (visible > lastVisible && tickTimer >= talkTickInterval)
                {
                    tickTimer = 0f;
                    PlayTalkTick();
                }
                lastVisible = visible;

                yield return null;
            }
        }

        dialogueText.maxVisibleCharacters = int.MaxValue;
        IsTalking = false;
        typewriter = null;
    }

    private void PlayTalkTick()
    {
        if (talkSound == null) return;

        float pitch = 1f + Random.Range(-talkPitchVariation, talkPitchVariation);
        GameSFX.Play(talkSound, talkVolume, pitch);
    }
}
