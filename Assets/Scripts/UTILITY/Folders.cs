using UnityEngine;

/// <summary>
/// Provides static access to common hierarchy folder transforms.
/// </summary>
public static class Folders
{
    private static Transform _gamezone;
    private static Transform _players;
    private static Transform _level;

    /// <summary>
    /// Gets the GAMEZONE transform from the hierarchy.
    /// </summary>
    /// <returns>The GAMEZONE transform, or null if not found.</returns>
    public static Transform GAMEZONE
    {
        get
        {
            if (_gamezone == null)
            {
                GameObject gamezoneObject = GameObject.Find("GAMEZONE");
                if (gamezoneObject != null)
                {
                    _gamezone = gamezoneObject.transform;
                }
                else
                {
                    DL.Warning("GAMEZONE object not found in hierarchy.");
                }
            }
            return _gamezone;
        }
    }

    /// <summary>
    /// Gets the PLAYERS transform from the hierarchy.
    /// </summary>
    /// <returns>The PLAYERS transform, or null if not found.</returns>
    public static Transform PLAYERS
    {
        get
        {
            if (_players == null)
            {
                GameObject playersObject = GameObject.Find("PLAYERS");
                if (playersObject != null)
                {
                    _players = playersObject.transform;
                }
                else
                {
                    DL.Warning("PLAYERS object not found in hierarchy.");
                }
            }
            return _players;
        }
    }

    /// <summary>
    /// Gets the LEVEL transform from the hierarchy.
    /// </summary>
    /// <returns>The LEVEL transform, or null if not found.</returns>
    public static Transform LEVEL
    {
        get
        {
            if (_level == null)
            {
                GameObject levelObject = GameObject.Find("LEVEL");
                if (levelObject != null)
                {
                    _level = levelObject.transform;
                }
                else
                {
                    DL.Warning("LEVEL object not found in hierarchy.");
                }
            }
            return _level;
        }
    }

    /// <summary>
    /// Clears all cached references. Call this when hierarchy changes significantly.
    /// </summary>
    public static void ClearCache()
    {
        _gamezone = null;
        _players = null;
        _level = null;
    }
}
