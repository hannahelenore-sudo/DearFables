using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// The ending NPC. It works exactly like the StarQuestNPC (Press E prompt, Intro on the first talk,
/// the star requirement floating above the character, Reminder while the player has too few stars,
/// Complete when they have enough), but instead of a card game it ends the game:
/// after the Complete dialogue the screen fades to white, "Thanks for playing!" appears, and a click
/// takes the player back to the main menu.
///
/// Setup: add this component to your NPC (instead of StarQuestNPC), set Required Stars, assign the star
/// sprite, and write three dialogues on the IntroCutscene using the Ids below
/// ("Ending Intro", "Ending Reminder", "Ending Complete"). Typing the Ids creates them for you.
///
/// Do not put a StarQuestNPC on the same object. Do not reuse the Star Quest's Ids or Quest Id.
/// </summary>
public class EndingNPC : StarQuestNPC
{
    [Header("Ending")]
    [Tooltip("Seconds between the end of the Complete dialogue and the start of the white fade.")]
    public float delayBeforeFade = 0.5f;
    public EndingSettings ending = new EndingSettings();

    // Defaults for a NEW component, so the Ids never collide with your first star quest
    public EndingNPC()
    {
        questId = "EndingQuest";
        requiredStars = 10;
        introId = "Ending Intro";
        reminderId = "Ending Reminder";
        completeId = "Ending Complete";
        doneId = "";
    }

    protected override void Awake()
    {
        // These parts of the star quest make no sense for an ending
        followUpId = "";
        becomeEnemy = false;
        afterGameId = "";
        tickAutomatically = false; // the player must talk to the NPC to finish the game

        base.Awake();

        if (onTutorialDone == null) onTutorialDone = new UnityEvent();
        onTutorialDone.AddListener(StartEnding);
    }

    // Runs right after the Complete dialogue has finished
    private void StartEnding()
    {
        if (delayBeforeFade > 0f) Invoke(nameof(PlayEnding), delayBeforeFade);
        else PlayEnding();
    }

    private void PlayEnding()
    {
        EndingSequence.Play(ending);
    }
}
