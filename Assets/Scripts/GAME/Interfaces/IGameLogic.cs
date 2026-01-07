using UnityEngine;

public interface IGameLogic
{
    void Init(UnifiedSyncMatrix.GAMEMODE gameMode);
    int GenerateLevel();
    void OnGameMessageReceived(string message, string value);
    // int ClientJoined();
    GameObject GetPlayer();
    Vector2 GetPlayerStartingPosition();
    void RepositionPlayerToStart();
}