using UnityEngine;
using TMPro;

public class Crate : MonoBehaviour
{

    [SerializeField, Range(0, 10)]
    private int _life = 3;

    private readonly USMSyncVar<int> lifeSyncVar = new(3);

    public int Life
    {
        get => lifeSyncVar.Value;
        set => lifeSyncVar.Value = value;
    }

    private TextMeshPro floatingText;

    // Start is called before the first frame update
    void Awake()
    {
        Transform textTransform = transform.Find("FloatingText");
        floatingText = textTransform.GetComponent<TextMeshPro>();
    }
    void Update()
    {
        floatingText.text = Life.ToString();
    }

    void OnDestroy()
    {
        lifeSyncVar.Dispose();
    }

}