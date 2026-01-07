using System;
using UnityEngine;
// using static CacheEngine;

public class BlockTags : MonoBehaviour
{
    // public CacheEngineItem Cache;
    public bool BlocksLight = true;
    public bool Climbable = true;
    public bool Stairs = false;
    public bool ParentIsACompositeCollider = false;
    public bool Pilotable = false;
    public bool Zoned = false;
    public bool Interactable = false;
    public bool Damageable = false;

    void Start()
    {

        if (ParentIsACompositeCollider)
        {
            var compositeOperation = Collider2D.CompositeOperation.None;

            if (transform.TryGetComponent<Collider2D>(out var col))
            {
                compositeOperation = Collider2D.CompositeOperation.Merge;
            }
            else
            {
                compositeOperation = Collider2D.CompositeOperation.None;
            }

            transform.GetComponentInChildren<Collider2D>().compositeOperation = compositeOperation;

        }

    }


}
