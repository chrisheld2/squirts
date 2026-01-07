using UnityEngine;

[ExecuteAlways]
public class PlayerStart : MonoBehaviour
{
    [SerializeField]
    private int playerIndex = 0;

    private FloatingText floatingText;

    public int PlayerIndex
    {
        get { return playerIndex; }
        set
        {
            playerIndex = value;
            UpdateFloatingText();
        }
    }

    void Awake()
    {
        EnsureFloatingText();
        UpdateFloatingText();
    }

    void OnEnable()
    {
        EnsureFloatingText();
        UpdateFloatingText();
    }

    void OnValidate()
    {
        // Update the floating text when the value changes in the Inspector
        EnsureFloatingText();
        UpdateFloatingText();
    }

    private void EnsureFloatingText()
    {
        if (floatingText == null)
        {
            floatingText = GetComponentInChildren<FloatingText>();
        }
    }

    private void UpdateFloatingText()
    {
        if (floatingText != null)
        {
            floatingText.Text = $"Player {playerIndex}";
        }
    }
}
