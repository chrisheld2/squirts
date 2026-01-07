using UnityEngine;
using System.Collections;

public class BuildNumberReader : MonoBehaviour
{
    private const string buildNumberFilePath = "BuildNumber";

    void Start()
    {
        StartCoroutine(ReadBuildNumber());
    }

    private IEnumerator ReadBuildNumber()
    {
        // Load the build number text asset
        ResourceRequest resourceRequest = Resources.LoadAsync<TextAsset>(buildNumberFilePath);
        yield return resourceRequest;

        TextAsset buildNumberAsset = resourceRequest.asset as TextAsset;

        if (buildNumberAsset != null)
        {
            string buildNumberString = buildNumberAsset.text;
            Debug.Log($"Current build number: {buildNumberString}");
        }
        else
        {
            Debug.LogError("Failed to load the build number file.");
        }
    }
}
