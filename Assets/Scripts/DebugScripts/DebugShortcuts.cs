using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Developer shortcut. Put this on an empty GameObject in BOTH the overworld scene and the card game scene,
/// and set the GameObject's Tag to "EditorOnly" so Unity strips it from builds.
/// It has no in-game UI: use the buttons in this component's Inspector while in Play mode.
/// </summary>
public class DebugShortcuts : MonoBehaviour
{
    [Header("Scenes")]
    [Tooltip("Scene that holds the card game / battle.")]
    public string cardGameSceneName = "YourCardGameScene";
    [Tooltip("Overworld scene. In the card game scene, BattleManager.returnSceneName is used instead if it is set.")]
    public string overworldSceneName = "YourOtherSceneName";
    public bool fadeWhenStartingCardGame = true;
    [Tooltip("Start Card Game as if you had hit the enemy with a card first (smaller score to beat + the Advantage Hit animation).")]
    public bool startWithAdvantageHit = false;

    [Header("Boons")]
    [Tooltip("Assign in the overworld scene. If empty, the first BoonSelectionUI in the scene is used.")]
    public BoonSelectionUI boonSelectionUI;
    [Tooltip("Off = the debug pick is free and doesn't touch the meter.")]
    public bool consumeMeterWhenPicked = false;

    [Header("Fake fight results")]
    [Tooltip("Hands 'left over' when you press Win Card Game. Decides the battery reward (3 + 1 per hand).")]
    public int debugHandsLeft = 1;
    [Tooltip("Stars given by Win Card Game.")]
    public int debugStarsOnWin = 1;

    // Static so it survives the scene load from the card game back to the overworld
    private static bool pendingOpenBoons;

    private IEnumerator Start()
    {
        if (!pendingOpenBoons) yield break;
        pendingOpenBoons = false;

        yield return new WaitForSecondsRealtime(0.6f); // let the scene / fade settle
        OpenBoons();
    }

    /// <summary>Overworld: opens the Choose Your Boon screen right now.</summary>
    public void OpenBoons()
    {
        BoonSelectionUI ui = boonSelectionUI != null ? boonSelectionUI : Object.FindAnyObjectByType<BoonSelectionUI>();
        if (ui == null)
        {
            Debug.LogWarning("DebugShortcuts: no BoonSelectionUI found in this scene.", this);
            return;
        }

        ui.Open(boon =>
        {
            BoonProgress.ApplyBoon(boon);
            if (consumeMeterWhenPicked) BoonProgress.ConsumeFullMeter();
        });
    }

    /// <summary>Overworld: jumps straight into the card game.</summary>
    public void StartCardGame()
    {
        if (string.IsNullOrEmpty(cardGameSceneName)) return;

        if (startWithAdvantageHit) AdvantageHit.Arm();
        else AdvantageHit.Disarm();
        Time.timeScale = 1f;

        if (fadeWhenStartingCardGame)
            BattleTransition.PlayFade(() => SceneManager.LoadScene(cardGameSceneName), 0.6f);
        else
            SceneManager.LoadScene(cardGameSceneName);
    }

    /// <summary>Card game: ends the battle (no win/loss, no stars, no meter) and opens the boon screen in the overworld.</summary>
    public void EndCardGameAndOpenBoons()
    {
        BattleManager battle = Object.FindAnyObjectByType<BattleManager>();
        string target = (battle != null && !string.IsNullOrEmpty(battle.returnSceneName))
            ? battle.returnSceneName
            : overworldSceneName;

        pendingOpenBoons = true;
        Time.timeScale = 1f;
        SceneManager.LoadScene(target);
    }

    // ---------------- Fake fight results ----------------

    string ReturnSceneName()
    {
        BattleManager battle = Object.FindAnyObjectByType<BattleManager>();
        return (battle != null && !string.IsNullOrEmpty(battle.returnSceneName))
            ? battle.returnSceneName
            : overworldSceneName;
    }

    /// <summary>
    /// Pretends you won a fight: batteries drop in the overworld, stars are added, the mood counts down one fight,
    /// then you go back to the overworld. (It doesn't run BattleManager's own win code.)
    /// </summary>
    public void WinCardGame()
    {
        BatteryManager.QueueDrop(BatteryManager.CalculateFightReward(debugHandsLeft));
        if (debugStarsOnWin > 0) PlayerProgress.AddStars(debugStarsOnWin);
        MoodManager.OnFightEnded();

        Time.timeScale = 1f;
        SceneManager.LoadScene(ReturnSceneName());
    }

    /// <summary>
    /// Pretends you lost a fight: the mood counts down, you lose a health card (Angry at 1, game over at 0),
    /// then you go back to the overworld. (It doesn't run BattleManager's own loss code.)
    /// </summary>
    public void FailCardGame()
    {
        Time.timeScale = 1f;
        MoodManager.OnFightEnded();
        PlayerHealth.Lose(1);

        if (PlayerHealth.IsDead) return; // the game over screen takes over
        SceneManager.LoadScene(ReturnSceneName());
    }

    public void GiveBatteries(int amount)
    {
        BatteryManager.Add(amount);
    }
}