#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// Must live in a folder named "Editor".
[CustomEditor(typeof(DebugShortcuts))]
public class DebugShortcutsEditor : Editor
{
    // Keeps the numbers (batteries, health, stars) live while playing
    public override bool RequiresConstantRepaint() { return Application.isPlaying; }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        DebugShortcuts t = (DebugShortcuts)target;

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Debug Buttons (Play mode only)", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            if (GUILayout.Button("Open Boons Now  (overworld)", GUILayout.Height(28)))
                t.OpenBoons();

            if (GUILayout.Button("Start Card Game  (overworld)", GUILayout.Height(28)))
                t.StartCardGame();

            if (GUILayout.Button("End Card Game & Open Boons  (in card game)", GUILayout.Height(28)))
                t.EndCardGameAndOpenBoons();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Win Card Game", GUILayout.Height(28)))
                    t.WinCardGame();

                if (GUILayout.Button("Fail Card Game", GUILayout.Height(28)))
                    t.FailCardGame();
            }
        }

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Difficulty (Play mode only)", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            float points = DifficultyProgress.Points;
            float newPoints = EditorGUILayout.Slider("Difficulty points", points, 0f, DifficultyProgress.maxPoints);
            if (!Mathf.Approximately(newPoints, points))
                DifficultyProgress.SetDebugPoints(newPoints);

            EditorGUILayout.LabelField("Meter",
                Mathf.RoundToInt(DifficultyProgress.Meter * 100f) + "%" +
                (DifficultyProgress.IsOverridden ? "   (debug override)" : "   (real progress)"));

            EditorGUILayout.LabelField("Stars / Fights",
                PlayerProgress.TotalStars + " / " + DifficultyProgress.BattlesFought);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Min")) DifficultyProgress.SetDebugPoints(0f);
                if (GUILayout.Button("Mid")) DifficultyProgress.SetDebugPoints(DifficultyProgress.maxPoints * 0.5f);
                if (GUILayout.Button("Max")) DifficultyProgress.SetDebugPoints(DifficultyProgress.maxPoints);
            }

            if (GUILayout.Button("Use real progress"))
                DifficultyProgress.ClearDebugOverride();
        }

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Mood (Play mode only)", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Neutral")) MoodManager.Clear();
                if (GUILayout.Button("Sugar Rush")) MoodManager.Set(MoodManager.SugarRush);
                if (GUILayout.Button("Angry")) MoodManager.Set(MoodManager.Angry);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Gather a Candy")) MoodManager.AddCandy();
                if (GUILayout.Button("End a Fight (-1)")) MoodManager.OnFightEnded();
            }
        }

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Health (Play mode only)", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Health cards", PlayerHealth.Current + " / " + PlayerHealth.Max + (PlayerHealth.IsDead ? "   (dead)" : ""));

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Lose 1")) PlayerHealth.Lose(1);
                if (GUILayout.Button("Heal 1")) PlayerHealth.Heal(1);
                if (GUILayout.Button("Full health")) PlayerHealth.ResetAll();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Set to 1 HP (Angry)")) PlayerHealth.SetCurrent(1);
                if (GUILayout.Button("Kill (0 HP)")) PlayerHealth.SetCurrent(0);
            }
        }

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Batteries (Play mode only)", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Batteries", BatteryManager.Count.ToString());

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("+1")) t.GiveBatteries(1);
                if (GUILayout.Button("+5")) t.GiveBatteries(5);
                if (GUILayout.Button("+20")) t.GiveBatteries(20);
                if (GUILayout.Button("-5")) t.GiveBatteries(-5);
                if (GUILayout.Button("Zero")) t.GiveBatteries(-BatteryManager.Count);
            }
        }

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Stamina (Play mode only)", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Empty")) PlayerStamina.Set(0f);
                if (GUILayout.Button("Half")) PlayerStamina.Set(0.5f);
                if (GUILayout.Button("Refill")) PlayerStamina.Refill();
            }
        }

        if (!Application.isPlaying)
            EditorGUILayout.HelpBox("Enter Play mode to use the buttons.", MessageType.Info);
    }
}
#endif