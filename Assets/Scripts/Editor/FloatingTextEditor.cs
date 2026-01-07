using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(FloatingText))]
public class FloatingTextEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // Draw the default inspector, but we will exclude the fixed size properties to draw them manually
        serializedObject.Update();

        // Draw all properties except the ones we want to control
        SerializedProperty iterator = serializedObject.GetIterator();
        bool enterChildren = true;
        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (iterator.name != "useFixedBackgroundSize" && iterator.name != "fixedBackgroundSize")
            {
                EditorGUILayout.PropertyField(iterator, true);
            }
        }

        // Draw our custom logic
        SerializedProperty useFixedSizeProp = serializedObject.FindProperty("useFixedBackgroundSize");
        SerializedProperty fixedSizeProp = serializedObject.FindProperty("fixedBackgroundSize");

        EditorGUILayout.PropertyField(useFixedSizeProp);

        if (useFixedSizeProp.boolValue)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(fixedSizeProp);
            
            if (GUILayout.Button("Auto Size"))
            {
                FloatingText script = (FloatingText)target;
                script.AutoSize();
            }
            EditorGUI.indentLevel--;
        }

        serializedObject.ApplyModifiedProperties();
    }
}
