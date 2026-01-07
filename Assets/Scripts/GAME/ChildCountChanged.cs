using UnityEngine;

public class ChildCountChanged : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        Debug.Log("OnTransformChildrenChanged()");
    }

    void OnTransformChildrenChanged()
    {
        // foreach (Transform go in transform)
        // {
        //     if (!go.gameObject.activeSelf) continue;

        //     var netGameObject = go.GetComponent<NETGameObject>();
        //     if (netGameObject == null) continue;

        //     if (!string.IsNullOrEmpty(netGameObject.guid)) continue;

        //     netGameObject.guid = Guid.NewGuid().ToString().Substring(0, 4);

        //     bool isLive = go.parent == transform;
        //     var goItem = GameObjectItem_AddToList(go.gameObject, ClientGUID, netGameObject.guid, isLive);
        //     goList.Add(netGameObject.guid, goItem);

        //     DL.Log("goItem added:" + goItem.unityGameObject.name, "yellow");



        // }
    }
}
