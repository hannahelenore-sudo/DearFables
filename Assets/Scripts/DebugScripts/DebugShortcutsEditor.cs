#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// Must live in a folder named "Editor".
[CustomEditor(typeof(DebugShortcuts))]
public class DebugShortcutsEditor : Editor
{
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

        if (!Application.isPlaying)
            EditorGUILayout.HelpBox("Enter Play mode to use the buttons.", MessageType.Info);
    }
}
#endif