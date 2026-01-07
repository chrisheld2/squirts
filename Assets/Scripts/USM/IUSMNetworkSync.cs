/// <summary>
/// Unified interface for GameObjects that want network synchronization via USMNode.
/// Components implementing this interface get:
/// - Automatic transform/Rigidbody2D sync (handled by USMNode)
/// - Custom value sync via PerformSync() (optional - implement if you have custom values)
/// - Receiving network updates via OnSyncFromNetwork() (optional - implement if you need to receive custom values)
///
/// USMNode automatically detects components with this interface and handles:
/// - Network sync timing (respects network interval and thresholds)
/// - Position/rotation/velocity tracking with configurable thresholds
/// - Calling PerformSync() when sync is needed (if implemented)
/// - Calling OnSyncFromNetwork() when data arrives (if implemented)
/// </summary>
public interface IUSMNetworkSync
{
    /// <summary>
    /// (OPTIONAL) Called by USMNode when network data is received.
    /// Implement this ONLY if you need to receive custom variable updates.
    ///
    /// Note: Transform and Rigidbody2D are applied automatically by USMNode - focus on custom values only.
    /// Standard network message layout (handled by USMNode automatically):
    /// - Vector2_1: Position
    /// - Vector2_2: Velocity (if Rigidbody2D present)
    /// - Float1: Rotation
    /// - Float2: Angular velocity (if Rigidbody2D present)
    /// - Remaining slots (Int1-5, Float3-10, Bool1-5, String1-5, Vector2_3-5): Your custom values
    /// </summary>
    /// <param name="message">The network message containing synchronized data</param>
    void OnSyncFromNetwork(ref NetworkMessage message) { }
}
