#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Draws any string marked [CharacterName] as a dropdown of the names in the IntroCutscene's Characters list.
/// Put this file in a folder called "Editor" inside Assets.
/// </summary>
[CustomPropertyDrawer(typeof(CharacterNameAttribute))]
public class CharacterNameDrawer : PropertyDrawer
{
    private const string MissingSuffix = " (not in list)";

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.String)
        {
            EditorGUI.PropertyField(position, property, label);
            return;
        }

        var names = new List<string> { "(none / narrator)" };

        IntroCutscene cutscene = property.serializedObject.targetObject as IntroCutscene;
        if (cutscene != null && cutscene.characters != null)
        {
            foreach (CutsceneActor c in cutscene.characters)
                if (c != null && !string.IsNullOrEmpty(c.name) && !names.Contains(c.name))
                    names.Add(c.name);
        }

        string current = property.stringValue;
        int index = 0;
        if (!string.IsNullOrEmpty(current))
        {
            index = names.FindIndex(1, n => string.Equals(n, current, System.StringComparison.OrdinalIgnoreCase));
            if (index < 0)
            {
                names.Add(current + MissingSuffix);
                index = names.Count - 1;
            }
        }

        EditorGUI.BeginProperty(position, label, property);
        EditorGUI.BeginChangeCheck();
        int selected = EditorGUI.Popup(position, label.text, index, names.ToArray());
        if (EditorGUI.EndChangeCheck() && selected != index)
            property.stringValue = selected == 0 ? "" : names[selected];
        EditorGUI.EndProperty();
    }
}
#endif
