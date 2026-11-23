using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

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

    private static bool pendingOpenBoons;

    private IEnumerator Start()
    {
        if (!pendingOpenBoons) yield break;
        pendingOpenBoons = false;

        yield return new WaitForSecondsRealtime(0.6f);
        OpenBoons();
    }

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

    string ReturnSceneName()
    {
        BattleManager battle = Object.FindAnyObjectByType<BattleManager>();
        return (battle != null && !string.IsNullOrEmpty(battle.returnSceneName))
            ? battle.returnSceneName
            : overworldSceneName;
    }

    public void WinCardGame()
    {
        BatteryManager.QueueDrop(BatteryManager.CalculateFightReward(debugHandsLeft));
        if (debugStarsOnWin > 0) PlayerProgress.AddStars(debugStarsOnWin);
        MoodManager.OnFightEnded();

        Time.timeScale = 1f;
        SceneManager.LoadScene(ReturnSceneName());
    }

    public void FailCardGame()
    {
        Time.timeScale = 1f;
        MoodManager.OnFightEnded();
        PlayerHealth.Lose(1);

        if (PlayerHealth.IsDead) return;
        SceneManager.LoadScene(ReturnSceneName());
    }

    public void GiveBatteries(int amount)
    {
        BatteryManager.Add(amount);
    }

    public void AddWindPower(int amount)
    {
        CardData.DebugWindPower += amount;
    }

    public void AddSunPower(int amount)
    {
        CardData.DebugSunPower += amount;
    }

    public void ResetCardPower()
    {
        CardData.DebugWindPower = 0;
        CardData.DebugSunPower = 0;
    }

    public bool HasDeck { get { return Object.FindAnyObjectByType<DeckManager>() != null; } }

    public float LowPowerChance
    {
        get { DeckManager d = Object.FindAnyObjectByType<DeckManager>(); return d != null ? d.lowPowerChance : 0f; }
        set { DeckManager d = Object.FindAnyObjectByType<DeckManager>(); if (d != null) d.lowPowerChance = Mathf.Clamp01(value); }
    }

    public void MakeHandLowPower(bool onlySelected)
    {
        DeckManager d = Object.FindAnyObjectByType<DeckManager>();
        if (d == null) { Debug.LogWarning("DebugShortcuts: no DeckManager in this scene (use this in the card game).", this); return; }
        int n = d.DebugMakeHandLowPower(onlySelected);
        Debug.Log("DebugShortcuts: " + n + " card(s) are now Low Power.", this);
    }

    public void RestoreHand(bool onlySelected)
    {
        DeckManager d = Object.FindAnyObjectByType<DeckManager>();
        if (d == null) { Debug.LogWarning("DebugShortcuts: no DeckManager in this scene (use this in the card game).", this); return; }
        int n = d.DebugRestoreHand(onlySelected);
        Debug.Log("DebugShortcuts: " + n + " card(s) restored to normal.", this);
    }
}