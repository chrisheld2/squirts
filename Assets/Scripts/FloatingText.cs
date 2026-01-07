using UnityEngine;
using TMPro;

[ExecuteAlways]
public class FloatingText : MonoBehaviour
{
    private TextMeshPro textMeshPro;
    private SpriteRenderer backgroundRenderer;
    private Quaternion initialRotation;
    private Vector3 worldOffset;

    [SerializeField]
    private Vector2 padding = new Vector2(0.5f, 0.25f);

    [SerializeField]
    private Color backgroundColor = new Color(0, 0, 0, 0.5f);

    [SerializeField]
    private Color textColor = Color.white;

    [SerializeField]
    private float fontSize = 8f;

    [SerializeField]
    private TMP_FontAsset font;

    [SerializeField]
    private bool useFixedBackgroundSize = false;

    [SerializeField]
    private Vector2 fixedBackgroundSize = new Vector2(1, 1);

    public string Text
    {
        get { return textMeshPro != null ? textMeshPro.text : ""; }
        set
        {
            if (textMeshPro != null)
            {
                textMeshPro.text = value;
                UpdateBackground();
            }
        }
    }

    public Color TextColor
    {
        get { return textColor; }
        set
        {
            textColor = value;
            if (textMeshPro != null) textMeshPro.color = value;
        }
    }

    public Color Color
    {
        set { TextColor = value; }
    }

    public Color BackgroundColor
    {
        get { return backgroundColor; }
        set
        {
            backgroundColor = value;
            if (backgroundRenderer != null) backgroundRenderer.color = value;
        }
    }

    public float FontSize
    {
        get { return fontSize; }
        set
        {
            fontSize = value;
            if (textMeshPro != null)
            {
                textMeshPro.fontSize = value;
                UpdateBackground();
            }
        }
    }

    public TMP_FontAsset Font
    {
        get { return font; }
        set
        {
            font = value;
            if (textMeshPro != null)
            {
                textMeshPro.font = value;
                UpdateBackground();
            }
        }
    }

    public bool Visible
    {
        get { return gameObject.activeSelf; }
        set { gameObject.SetActive(value); }
    }

    public void AutoSize()
    {
        if (textMeshPro != null)
        {
            Vector2 preferredValues = textMeshPro.GetPreferredValues();
            fixedBackgroundSize = new Vector2(preferredValues.x + padding.x, preferredValues.y + padding.y);
            UpdateBackground();
        }
    }

    void Awake()
    {
        Initialize();
    }

    void OnEnable()
    {
        Initialize();
    }

    private void Initialize()
    {
        // Store the initial world rotation
        initialRotation = transform.rotation;

        // Calculate world offset from parent (if we have a parent)
        if (transform.parent != null)
        {
            worldOffset = transform.position - transform.parent.position;
        }

        textMeshPro = GetComponent<TextMeshPro>();
        if (textMeshPro != null)
        {
            // Sync initial values from serialized fields if they are set
            if (textColor != default) textMeshPro.color = textColor;
            if (fontSize > 0) textMeshPro.fontSize = fontSize;
            if (font != null) textMeshPro.font = font;

            // Configure auto-sizing and alignment
            textMeshPro.enableAutoSizing = false; // We handle sizing manually
            textMeshPro.alignment = TextAlignmentOptions.Center;
            textMeshPro.horizontalMapping = TextureMappingOptions.Character;
            textMeshPro.overflowMode = TextOverflowModes.Overflow;
        }
        CreateBackground();
        UpdateBackground();
    }

    void Start()
    {
        UpdateBackground();
    }

    void Update()
    {
        // Maintain initial world rotation regardless of parent rotation
        transform.rotation = initialRotation;

        // Maintain fixed world offset from parent
        if (Application.isPlaying && transform.parent != null)
        {
            // Set world position to parent position + world offset
            transform.position = transform.parent.position + worldOffset;

            // Counter-flip the local scale to keep text readable when parent is flipped
            Vector3 parentScale = transform.parent.localScale;
            Vector3 currentScale = transform.localScale;
            currentScale.x = parentScale.x < 0 ? -1 : 1;
            transform.localScale = currentScale;
        }

        // In editor, we might want to update background if text changes, but usually OnValidate handles properties.
        // However, if the text mesh changes size for other reasons, we might want to update.
        if (!Application.isPlaying)
        {
            UpdateBackground();
        }
    }

    void OnValidate()
    {
        // Apply values when changed in Inspector
        if (textMeshPro != null)
        {
            textMeshPro.color = textColor;
            textMeshPro.fontSize = fontSize;
            if (font != null) textMeshPro.font = font;
        }
        if (backgroundRenderer != null)
        {
            backgroundRenderer.color = backgroundColor;
        }
        UpdateBackground();
    }

    private void CreateBackground()
    {
        Transform bgTransform = transform.Find("Background");
        if (bgTransform == null)
        {
            GameObject bgObj = new GameObject("Background");
            bgObj.transform.SetParent(transform, false);
            bgObj.transform.localPosition = new Vector3(0, 0, 0.1f);

            backgroundRenderer = bgObj.AddComponent<SpriteRenderer>();
            backgroundRenderer.sortingOrder = -1;
        }
        else
        {
            backgroundRenderer = bgTransform.GetComponent<SpriteRenderer>();
        }

        // Ensure sprite exists (it might be lost in editor reloads if generated at runtime)
        if (backgroundRenderer.sprite == null)
        {
            Texture2D tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            // HideAndDontSave to avoid polluting the project with generated sprites
            tex.hideFlags = HideFlags.HideAndDontSave;

            // Set pixelsPerUnit to 1 so that the 1x1 pixel texture is 1x1 Unity units in size.
            // This ensures that setting localScale to (width, height) results in a background of that size.
            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1.0f);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            backgroundRenderer.sprite = sprite;
        }

        backgroundRenderer.color = backgroundColor;
    }

    private void UpdateBackground()
    {
        if (backgroundRenderer != null && textMeshPro != null)
        {
            // Set RectTransform to a very large size to prevent text wrapping
            RectTransform rectTransform = textMeshPro.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                rectTransform.sizeDelta = new Vector2(9999, 9999);
            }

            if (useFixedBackgroundSize)
            {
                backgroundRenderer.transform.localScale = new Vector3(fixedBackgroundSize.x, fixedBackgroundSize.y, 1);
            }
            else
            {
                // Force TextMeshPro to update its layout before getting preferred values
                textMeshPro.ForceMeshUpdate();
                Vector2 preferredValues = textMeshPro.GetPreferredValues();
                backgroundRenderer.transform.localScale = new Vector3(preferredValues.x + padding.x, preferredValues.y + padding.y, 1);
            }
        }
    }
}
