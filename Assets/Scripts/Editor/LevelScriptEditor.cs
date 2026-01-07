using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(LevelScript))]
public class LevelScriptEditor : Editor
{
    private string newProfileName = "";
    private int selectedProfileIndex = 0;

    public override void OnInspectorGUI()
    {
        // Get the LevelScript component
        LevelScript levelScript = (LevelScript)target;

        // Ensure currentProfileName is synced with PlayerPrefs (in case we just exited Play mode)
        string savedLastProfile = PlayerPrefs.GetString("LevelScript_LastProfile", "Default");
        if (string.IsNullOrEmpty(levelScript.currentProfileName) || levelScript.currentProfileName != savedLastProfile)
        {
            // Update the field to match what's saved
            levelScript.currentProfileName = savedLastProfile;
            EditorUtility.SetDirty(levelScript);
        }

        // Initialize newProfileName field if empty
        if (string.IsNullOrEmpty(newProfileName))
        {
            string currentProfile = levelScript.currentProfileName;
            if (currentProfile.Contains(" "))
            {
                int spaceIndex = currentProfile.IndexOf(" ");
                newProfileName = currentProfile.Substring(spaceIndex + 1);
            }
            else
            {
                newProfileName = currentProfile;
            }
        }

        // Profile Management Section
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Profile Management", EditorStyles.boldLabel);

        // Get available profiles
        string[] profiles = levelScript.GetAvailableProfiles();

        // Find current profile index
        selectedProfileIndex = System.Array.IndexOf(profiles, levelScript.currentProfileName);
        if (selectedProfileIndex < 0) selectedProfileIndex = 0;

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Current Profile:", GUILayout.Width(100));

        // Profile dropdown
        int newSelectedIndex = EditorGUILayout.Popup(selectedProfileIndex, profiles);
        if (newSelectedIndex != selectedProfileIndex)
        {
            selectedProfileIndex = newSelectedIndex;
            levelScript.LoadProfile(profiles[selectedProfileIndex]);

            // Update the newProfileName field to match the current profile name
            string currentProfile = levelScript.currentProfileName;
            if (currentProfile.Contains(" "))
            {
                int spaceIndex = currentProfile.IndexOf(" ");
                newProfileName = currentProfile.Substring(spaceIndex + 1);
            }
            else
            {
                newProfileName = currentProfile;
            }

            EditorUtility.SetDirty(levelScript);
        }
        EditorGUILayout.EndHorizontal();

        // Save/Load Profile Section
        EditorGUILayout.BeginHorizontal();
        newProfileName = EditorGUILayout.TextField("Profile Name:", newProfileName);

        if (GUILayout.Button("Save Profile", GUILayout.Width(100)))
        {
            if (!string.IsNullOrEmpty(newProfileName))
            {
                levelScript.SaveProfile(newProfileName);
                EditorUtility.SetDirty(levelScript);
            }
        }
        EditorGUILayout.EndHorizontal();




        // Show preview of what the profile name will be
        if (!string.IsNullOrEmpty(newProfileName) && newProfileName != "Default")
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField($"Will save as: {levelScript.widthGet()}x{levelScript.heightGet()} {newProfileName}", EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        // Delete Profile Button (only if not Default)
        if (levelScript.currentProfileName != "Default")
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUI.backgroundColor = Color.red;
            if (GUILayout.Button($"Delete '{levelScript.currentProfileName}'", GUILayout.Width(150)))
            {
                if (EditorUtility.DisplayDialog("Delete Profile",
                    $"Are you sure you want to delete profile '{levelScript.currentProfileName}'?",
                    "Delete", "Cancel"))
                {
                    levelScript.DeleteProfile(levelScript.currentProfileName);
                    levelScript.LoadProfile("Default");
                    // After deleting, the new profile is "Default", so newProfileName should be updated
                    newProfileName = "Default";
                    EditorUtility.SetDirty(levelScript);
                }
            }
            GUI.backgroundColor = Color.white;
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space();
        EditorGUILayout.Space();


        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);


        // --- BUTTONS AT TOP ---
        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        // Create button style
        var buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 12,
            fontStyle = FontStyle.Bold
        };

        // Random Seed button
        if (GUILayout.Button("Random Seed", buttonStyle, GUILayout.Width(120), GUILayout.Height(30)))
        {
            levelScript.CreateWithNewSeed();
            EditorUtility.SetDirty(levelScript);
            Repaint();
        }

        GUILayout.Space(10);

        // Reset to Defaults button
        if (GUILayout.Button("Reset to Defaults", buttonStyle, GUILayout.Width(150), GUILayout.Height(30)))
        {
            levelScript.ResetToDefaults();
            EditorUtility.SetDirty(levelScript);
            Repaint();
        }

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space();
        // --- END OF BUTTONS ---


        // Draw the default inspector
        DrawDefaultInspector();

        // Add some space
        EditorGUILayout.Space();
    }
}