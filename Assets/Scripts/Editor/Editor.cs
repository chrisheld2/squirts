using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

public class BuildNumberIncrementer : IPreprocessBuildWithReport
{
    private const string buildNumberFilePath = "Assets/Resources/BuildNumber.txt";

    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        IncrementBuildNumber();
    }

    private void IncrementBuildNumber()
    {
        int buildNumber = 0;

        // Read the current build number from the file
        if (File.Exists(buildNumberFilePath))
        {
            string buildNumberString = File.ReadAllText(buildNumberFilePath);
            int.TryParse(buildNumberString, out buildNumber);
        }

        // Increment the build number
        buildNumber++;

        // Write the new build number back to the file
        File.WriteAllText(buildNumberFilePath, buildNumber.ToString());

        // Log the new build number for confirmation
        DL.Log($"New build number: {buildNumber}");
    }
}
