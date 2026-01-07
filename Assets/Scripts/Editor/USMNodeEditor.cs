#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(USMNode))]
// [CanEditMultipleObjects]
public class USMNodeEditor : Editor
{
    private bool showAutoSyncThresholds = true;
    private bool showInterpolationSettings = true;
    private bool showLiveSwapSettings = true;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // Draw default fields (nodeID, nodeType, etc.)
        SerializedProperty prop = serializedObject.GetIterator();
        prop.NextVisible(true); // Skip script field

        // Draw fields until we hit the first Header section
        while (prop.NextVisible(false))
        {
            if (prop.name == "positionThreshold") break;
            EditorGUILayout.PropertyField(prop, true);
        }

        EditorGUILayout.Space(10);

        // Auto-Sync Thresholds Section
        showAutoSyncThresholds = EditorGUILayout.BeginFoldoutHeaderGroup(showAutoSyncThresholds, "Auto-Sync Thresholds");
        if (showAutoSyncThresholds)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(serializedObject.FindProperty("positionThreshold"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("rotationThreshold"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("velocityThreshold"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("angularVelocityThreshold"));
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        EditorGUILayout.Space(10);

        // Interpolation Settings Section
        showInterpolationSettings = EditorGUILayout.BeginFoldoutHeaderGroup(showInterpolationSettings, "Interpolation Settings");
        if (showInterpolationSettings)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(serializedObject.FindProperty("enableInterpolation"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("interpolationSpeed"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("teleportThreshold"));
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        EditorGUILayout.Space(10);

        // Live Swap Settings Section
        showLiveSwapSettings = EditorGUILayout.BeginFoldoutHeaderGroup(showLiveSwapSettings, "Live Swap Settings");
        if (showLiveSwapSettings)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(serializedObject.FindProperty("swapLive"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("swapTriggerRadius"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("swapTriggerOffset"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("swapCooldown"));
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
