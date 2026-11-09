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

        if (!Application.isPlaying)
            EditorGUILayout.HelpBox("Enter Play mode to use the buttons.", MessageType.Info);
    }
}
#endif
