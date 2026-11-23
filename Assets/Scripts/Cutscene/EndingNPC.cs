using UnityEngine;
using UnityEngine.Events;

public class EndingNPC : StarQuestNPC
{
    [Header("Ending")]
    [Tooltip("Seconds between the end of the Complete dialogue and the start of the white fade.")]
    public float delayBeforeFade = 0.5f;
    public EndingSettings ending = new EndingSettings();

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
        followUpId = "";
        becomeEnemy = false;
        afterGameId = "";
        tickAutomatically = false;

        base.Awake();

        if (onTutorialDone == null) onTutorialDone = new UnityEvent();
        onTutorialDone.AddListener(StartEnding);
    }

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