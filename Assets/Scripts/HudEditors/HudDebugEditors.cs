#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// Must live in a folder named "Editor". Test buttons shown under each HUD component while the game is running.

[CustomEditor(typeof(MoodHUD))]
public class MoodHUDEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Test Buttons (Play mode)", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Neutral")) MoodManager.Clear();
            if (GUILayout.Button("Sugar Rush")) MoodManager.Set(MoodManager.SugarRush);
            if (GUILayout.Button("Angry")) MoodManager.Set(MoodManager.Angry);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Gather a Candy")) MoodManager.AddCandy();
            if (GUILayout.Button("End a Fight (-1)")) MoodManager.OnFightEnded();
            EditorGUILayout.EndHorizontal();
        }
    }
}

[CustomEditor(typeof(BatteryHUD))]
public class BatteryHUDEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Test Buttons (Play mode)", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+1")) BatteryManager.Add(1);
            if (GUILayout.Button("+5")) BatteryManager.Add(5);
            if (GUILayout.Button("-3")) BatteryManager.Add(-3);
            if (GUILayout.Button("Can't afford")) BatteryHUD.FlashCantAfford();
            EditorGUILayout.EndHorizontal();
        }
    }
}

[CustomEditor(typeof(HealthCardsHUD))]
public class HealthCardsHUDEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Test Buttons (Play mode)", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Lose 1")) PlayerHealth.Lose(1);
            if (GUILayout.Button("Heal 1")) PlayerHealth.Heal(1);
            if (GUILayout.Button("Full health")) PlayerHealth.ResetAll();
            EditorGUILayout.EndHorizontal();
        }
    }
}

[CustomEditor(typeof(BatteryDropper))]
public class BatteryDropperEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space(8);

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            if (GUILayout.Button("Test Drop (Play mode)")) ((BatteryDropper)target).Drop();
        }
    }
}
#endif
