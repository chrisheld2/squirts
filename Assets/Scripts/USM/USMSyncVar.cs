using System;
using UnityEngine;


public class USMSyncVar<T>
{
    private T _value;
    private string _nodeID = "";
    private UnifiedSyncMatrix _syncMatrix;
    private string _syncEventName;
    private MonoBehaviour _owner;


    public event Action<T> OnValueChanged;

    public string NodeID => _nodeID;
    public string SyncEventName => _syncEventName;
    public string TypeName => typeof(T).Name;
    public MonoBehaviour Owner => _owner;

    public T Value
    {
        get => _value;
        set
        {
            if (!Equals(_value, value))
            {
                _value = value;
                OnValueChanged?.Invoke(_value);
                _syncMatrix.Call(_syncEventName, _value);
            }
        }
    }

    public USMSyncVar(T initialValue = default)
    {
        _value = initialValue;
    }

    public void SetValueSilent(T value)
    {
        if (!Equals(_value, value))
        {
            _value = value;
            OnValueChanged?.Invoke(_value);
        }
    }

    public void Init(MonoBehaviour owner)
    {
        _owner = owner;

        var node = owner.GetComponent<USMNode>();
        if (node != null)
        {
            _nodeID = node.NodeID;
            _syncEventName = node.SyncEventName;
        }

        _syncMatrix = UnityEngine.Object.FindObjectOfType<UnifiedSyncMatrix>();

        if (_syncMatrix != null)
        {
            _syncMatrix.OnNetworkUpdate += HandleNetworkUpdate;

            _syncMatrix.OnEventTriggered += HandleEventTriggered;
        }
    }

    private void HandleNetworkUpdate(string nodeID, NetworkMessage networkMessage)
    {
        // Only process updates for this specific node
        if (nodeID != _nodeID) return;

        // Extract the value from the network message based on the type
        // The value is stored in the appropriate typed field (Bool1, Int1, Float1, String1, or Vector2_1)
        object newValue = null;

        if (typeof(T) == typeof(bool) && networkMessage.Bool1.HasValue)
            newValue = networkMessage.Bool1.Value;
        else if (typeof(T) == typeof(int) && networkMessage.Int1.HasValue)
            newValue = networkMessage.Int1.Value;
        else if (typeof(T) == typeof(float) && networkMessage.Float1.HasValue)
            newValue = networkMessage.Float1.Value;
        else if (typeof(T) == typeof(string) && networkMessage.String1 != null)
            newValue = networkMessage.String1;
        else if (typeof(T) == typeof(Vector2) && networkMessage.Vector2_1.HasValue)
            newValue = networkMessage.Vector2_1.Value;

        if (newValue != null)
        {
            // Use SetValueSilent to prevent echo - this updates the local value and fires
            // OnValueChanged for local subscribers, but does NOT trigger another network send
            SetValueSilent((T)newValue);
        }
    }

    private void HandleEventTriggered(string eventName, string value)
    {
        // Only respond if this event is meant for us (we are the receiver)
        if (eventName == _syncEventName)
        {
            _value = (T)Convert.ChangeType(value, typeof(T));

            // Fire OnValueChanged with the current value
            // This allows local pub/sub without network traffic
            OnValueChanged?.Invoke(_value);

            DL.Log($"[USMSyncVar] Local event received: {eventName}, current value: {_value}", "brown");
        }
    }

    public void Dispose()
    {
        if (_syncMatrix != null)
        {
            _syncMatrix.OnNetworkUpdate -= HandleNetworkUpdate;
            _syncMatrix.OnEventTriggered -= HandleEventTriggered;
        }
    }
}
