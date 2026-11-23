using UnityEngine;

public static class GameReset
{
    public static void NewGame()
    {
        Time.timeScale = 1f;
        InputLock.ReleaseAll();

        SaveSystem.ResetProgress();

        PlayerProgress.ResetAll();
        PlayerHealth.ResetAll();
        BatteryManager.ResetAll();
        MoodManager.ResetAll();
        StructureProgress.ResetAll();
        AdvantageHit.ResetAll();
        BoonProgress.ResetAll();
        TrinketInventory.Clear();
        TrinketSidebar.EndScoring();

        EnemyChase.ResetCaughtEnemies();
        DifficultyProgress.ResetAll();
        DifficultyMeter.ResetShown();

        StarHover.ResetCollected();
        StarQuestProgress.ResetAll();
        CardPickup.ResetAll();
        CardGameTutorialGate.ResetAll();

        BattleEnemy.Clear();
        PlayerPositionSaver.ResetAll();
        PlayerRollUI.isHighRollActive = false;
        PlayerRollUI.isLowRollActive = false;
    }
}