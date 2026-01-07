using TMPro;
using UnityEngine;

public class Version : MonoBehaviour
{

    void Start()
    {
        int version = GetBuildCount();

        GetComponentInChildren<TextMeshProUGUI>().text = "Version:" + version;

    }

    private int GetBuildCount()
    {
        // Load the BuildNumber.txt from the Resources folder
        TextAsset buildNumberAsset = Resources.Load<TextAsset>("BuildNumber");

        if (buildNumberAsset != null)
        {
            string buildNumberString = buildNumberAsset.text;
            if (int.TryParse(buildNumberString, out int buildNumber))
            {
                return buildNumber;
            }
        }

        return 0;
    }
}
