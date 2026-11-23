using UnityEngine;

/// <summary>
/// ONE place that wipes the whole play session. MainMenu calls GameReset.NewGame() when the player
/// presses Start, so going back to the main menu (without saving) and starting again is a completely fresh game.
///
/// Whenever you add a new system that remembers something between scenes (a static, or a
/// DontDestroyOnLoad object), give it a reset method and add ONE line below.
/// </summary>
public static class GameReset
{
    public static void NewGame()
    {
        // ---- Progress ----
        PlayerProgress.ResetAll();              // stars
        EnemyChase.ResetCaughtEnemies();        // every enemy comes back
        DifficultyProgress.ResetAll();          // blinds, defeats, battles, debug override
        DifficultyMeter.ResetShown();           // difficulty meter starts empty again

        // ---- Stars lying in the world + star quests / cutscenes ----
        StarHover.ResetCollected();             // every collected star respawns (the class lives in StarCollectible.cs)
        StarQuestProgress.ResetAll();           // quests accepted / done / enemy / after-game cutscene played

        // ---- Card pickup + tutorial ----
        CardPickup.ResetAll();                  // card lies on the ground again, shooter/boon locked
        CardGameTutorialGate.ResetAll();        // tutorial plays again (clears every flag)

        // ---- Boons (meter back to half, extra hands / hand size / wind power gone) ----
        BoonProgress.ResetAll();

        // ---- Trinkets (also refreshes the sidebar, it listens to the Cleared event) ----
        TrinketInventory.Clear();
        TrinketSidebar.EndScoring();            // make sure the order is not left locked by an interrupted battle

        // ---- Not in the files I received: add a ResetAll() to each and uncomment ----
        // BatteryManager.ResetAll();           // batteries (shop money)
        // MoodManager.ResetAll();              // candy count / Sugar Rush and other moods
        // ActiveBuffsSidebar... / buffs        // active buffs, if they are stored statically
    }
}