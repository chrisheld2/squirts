using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Text;

using UnityEngine;

public class UnifiedSyncMatrix : MonoBehaviour
{
    #region Events
    /// <summary>
    /// Event fired when the server is ready and has assigned a client GUID and session GUID.
    /// </summary>
    public event Action<string, string> OnServerIsReady;

    /// <summary>
    /// Event fired when a new client joins the session.
    /// </summary>
    public event Action<string> OnClientJoined;

    /// <summary>
    /// Event fired when a client leaves the session.
    /// </summary>
    public event Action<string> OnClientLeft;

    /// <summary>
    /// Event fired when a message is received from another client.
    /// </summary>
    public event Action<NetworkMessage> OnClientMessage;

    /// <summary>
    /// Event fired on a guest client when a query request is received.
    /// </summary>
    public event Action<string, string> onGuest_QueryRequest;

    /// <summary>
    /// Event fired on the host when a query response is received.
    /// </summary>
    public event Action<string> OnHost_QueryRequest;

    /// <summary>
    /// Event fired when a game-specific message is received.
    /// </summary>
    public event Action<string, string> OnGameMessage;

    /// <summary>
    /// Event fired when a network update is received for a specific node.
    /// Parameters: nodeID, NetworkMessage containing the update data
    /// </summary>
    public event Action<string, NetworkMessage> OnNetworkUpdate;
    /// <summary>
    /// Public event that other GameObjects can subscribe to
    /// </summary>
    public event Action<string, string> OnEventTriggered;

    #endregion

    #region Serialized Fields
    [SerializeField]
    private List<StringStringEntry> eventsList = new List<StringStringEntry>();
    private Dictionary<string, string> events = new Dictionary<string, string>();

    /// <summary>
    /// The GameObject containing the object pool script.
    /// </summary>
    [SerializeField] private GameObject pool;

    /// <summary>
    /// If true, data will be sent even if no other clients are connected.
    /// </summary>
    [SerializeField] public bool sendDataAlways;
    /// <summary>
    /// If true, outgoing network messages will be logged to the console.
    /// </summary>
    [SerializeField] public bool consoleSendMessage;
    /// <summary>
    /// If true, incoming network messages will be logged to the console.
    /// </summary>
    [SerializeField] public bool consoleReceiveMessage;
    /// <summary>
    /// The minimum interval in milliseconds between live state swaps for a NETGameObject.
    /// </summary>
    [SerializeField, Range(100, 2000)] private int networkLiveSwapInterval = 1500;
    /// <summary>
    /// The interval in milliseconds for sending network updates.
    /// </summary>
    [SerializeField, Range(100, 1000)] public int networkInterval = 250;

    [Header("Settings")]
    [SerializeField]
    private Settings settings;




    #endregion

    #region Constants / Enums
    public enum GAMEMODE
    {
        NOTSETYET = 0,
        SINGLEPLAYER = 1,
        MULTIPLAYER_HOST = 2,
        MULTIPLAYER_GUEST = 3
    }
    /// <summary>
    /// Defines the types of handshake messages used for establishing connection and state synchronization.
    /// </summary>
    private enum HANDSHAKES
    {
        REQUEST = 1,
        SENDING = 2,
        COMPLETE = 3
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // These constants are used as keys in the network message protocol.
    // Using full names in the editor makes debugging easier.
    public const string CLIENTGUID = "CLGID";
    public const string SESSIONGUID = "SGUD";
    public const string NAME = "NAM";
    public const string GUID = "GUD";
    public const string CLIENTGUIDLIST = "CGTL";
    public const string CLOSEDGUID = "CLSG";
    public const string YOURNEWGUID = "YNGUD";
    public const string REMOVEGUID = "RMGUD";
    public const string SENDINGCLIENTGUID = "SNDCL";
    public const string NEWCLIENTGUID = "NCGID";
    public const string POSITION = "POSIT";
    public const string VELOCITY = "VELCT";
    public const string SCALE = "SCALE";
    public const string ANGULARVELOCITY = "ANGLV";
    public const string ROTATION = "ROTAT";
    public const string SPEED = "SPED";
    public const string MAGNITUDE = "MAGNT";
    public const string ACTION = "ACTON";
    public const string ACTIONVALUE = "ACTVAL";
    public const string TARGETGUID = "TRGUD";
    public const string PARENTGUID = "PARGUD";
    public const string HANDSHAKE = "HANDSHAKE";

    public const string LIVESWAP = "LVSWP";
    public const string STARTGAME = "STRTG";
    public const string UPDATE = "UPDAT";
    public const string QUERY = "QRY";
    public const string QUERYRESPONSE = "QRYRESP";

    public const string INT1 = "INT1";
    public const string INT2 = "INT2";
    public const string INT3 = "INT3";
    public const string INT4 = "INT4";
    public const string INT5 = "INT5";

    public const string FLOAT1 = "FLT1";
    public const string FLOAT2 = "FLT2";
    public const string FLOAT3 = "FLT3";
    public const string FLOAT4 = "FLT4";
    public const string FLOAT5 = "FLT5";
    public const string FLOAT6 = "FLT6";
    public const string FLOAT7 = "FLT7";
    public const string FLOAT8 = "FLT8";
    public const string FLOAT9 = "FLT9";
    public const string FLOAT10 = "FLT10";

    public const string BOOL1 = "BOL1";
    public const string BOOL2 = "BOL2";
    public const string BOOL3 = "BOL3";
    public const string BOOL4 = "BOL4";
    public const string BOOL5 = "BOL5";

    public const string STRING1 = "STR1";
    public const string STRING2 = "STR2";
    public const string STRING3 = "STR3";
    public const string STRING4 = "STR4";
    public const string STRING5 = "STR5";

    public const string VECTOR2_1 = "VEC1";
    public const string VECTOR2_2 = "VEC2";
    public const string VECTOR2_3 = "VEC3";
    public const string VECTOR2_4 = "VEC4";
    public const string VECTOR2_5 = "VEC5";

    public const string GAMEMESSAGE = "GAMMSG";
    public const string GAMEVALUE = "GAMVAL";
    public const string CLIENTINDEX = "CIDX";
#else
    // In builds, shorter keys are used to reduce message size.
    public const string CLIENTGUID = "a";
    public const string SESSIONGUID = "b";
    public const string NAME = "c";
    public const string GUID = "d";
    public const string CLIENTGUIDLIST = "e";
    public const string CLOSEDGUID = "f";
    public const string YOURNEWGUID = "g";
    public const string REMOVEGUID = "h";
    public const string SENDINGCLIENTGUID = "i";
    public const string NEWCLIENTGUID = "j";
    public const string POSITION = "k";
    public const string VELOCITY = "l";
    public const string SCALE = "m";
    public const string ANGULARVELOCITY = "n";
    public const string ROTATION = "o";
    public const string SPEED = "p";
    public const string MAGNITUDE = "q";
    public const string ACTION = "r";
    public const string ACTIONVALUE = "s";
    public const string TARGETGUID = "t";
    public const string PARENTGUID = "v";
    public const string HANDSHAKE = "u";

    public const string LIVESWAP = "w";
    public const string STARTGAME = "x";
    public const string UPDATE = "y";
    public const string QUERY = "z";
    public const string QUERYRESPONSE = "A";

    public const string INT1 = "1";
    public const string INT2 = "2";
    public const string INT3 = "3";
    public const string INT4 = "4";
    public const string INT5 = "5";

    public const string FLOAT1 = "6";
    public const string FLOAT2 = "7";
    public const string FLOAT3 = "8";
    public const string FLOAT4 = "9";
    public const string FLOAT5 = "10";
    public const string FLOAT6 = "11";
    public const string FLOAT7 = "12";
    public const string FLOAT8 = "13";
    public const string FLOAT9 = "14";
    public const string FLOAT10 = "15";

    public const string BOOL1 = "B";
    public const string BOOL2 = "C";
    public const string BOOL3 = "D";
    public const string BOOL4 = "E";
    public const string BOOL5 = "F";

    public const string STRING1 = "G";
    public const string STRING2 = "H";
    public const string STRING3 = "I";
    public const string STRING4 = "J";
    public const string STRING5 = "K";

    public const string VECTOR2_1 = "L";
    public const string VECTOR2_2 = "M";
    public const string VECTOR2_3 = "N";
    public const string VECTOR2_4 = "O";
    public const string VECTOR2_5 = "P";

    public const string GAMEMESSAGE = "Q";
    public const string GAMEVALUE = "R";
    public const string CLIENTINDEX = "S";
#endif





    #endregion

    #region State Flags
    private bool isInitialized = false;
    private bool isClosing = false;
    private bool isConnected = false;
    private bool isSessionReady = false;
    private bool isPaused = false;
    private GAMEMODE gameMode = GAMEMODE.SINGLEPLAYER;

    /// <summary>
    /// Gets whether the UnifiedSyncMatrix has been initialized.
    /// </summary>
    public bool IsInitialized => isInitialized;

    /// <summary>
    /// Gets whether the UnifiedSyncMatrix is currently closing/shutting down.
    /// </summary>
    public bool IsClosing => isClosing;

    /// <summary>
    /// Gets whether the WebSocket connection has been established.
    /// </summary>
    public bool IsConnected => isConnected;

    /// <summary>
    /// Gets whether the session is ready (server handshake complete, can send game data).
    /// </summary>
    public bool IsSessionReady => isSessionReady;

    /// <summary>
    /// Gets whether the network updates are currently paused.
    /// </summary>
    public bool IsPaused => isPaused;

    /// <summary>
    /// Gets the current game mode.
    /// </summary>
    public GAMEMODE GameMode => gameMode;

    public string ClientGUID => _clientGUID;

    /// <summary>
    /// Gets the number of clients currently connected (including self).
    /// </summary>
    public int ConnectedClientCount => clientGUIDList != null ? clientGUIDList.Count : 0;

    /// <summary>
    /// Gets the index of the current client as assigned by the server (0-based).
    /// Returns -1 if not assigned yet.
    /// </summary>
    public int CurrentClientIndex => _clientIndex;

    #endregion

    #region Network Components
    private Settings serverSettings;
    private NETCoreWrapper.PLATFORMS platForm;
    private NETCoreWrapper netCoreWrapper;
    private List<string> clientGUIDList = new();
    private ConcurrentQueue<string> messages;
    private string _clientGUID;
    private string _sessionGUID;
    private int _clientIndex = -1;
    private Dictionary<string, GameObject> nodeList = new Dictionary<string, GameObject>();
    private int nextNodeIDCounter = 0; // Persistent counter for generating unique NodeIDs

    /// <summary>
    /// Gets a readonly list of all connected client GUIDs as provided by the server.
    /// This list is updated whenever the server sends a CLIENTGUIDLIST message.
    /// </summary>
    public IReadOnlyList<string> ConnectedClients => clientGUIDList;
    #endregion

    #region Message Parsing
    /// <summary>
    /// Dictionary-based message parser for improved performance and maintainability.
    /// Maps property keys to their respective parsing actions.
    /// </summary>
    private Dictionary<string, Action<NetworkMessage, string[]>> messageParsers;

    /// <summary>
    /// Dictionary-based message serializer for improved performance and maintainability.
    /// Maps property keys to functions that extract and format values from NetworkMessage.
    /// </summary>
    private Dictionary<string, Func<NetworkMessage, string>> messageSerializers;

    /// <summary>
    /// Cached StringBuilder for message serialization to avoid frequent allocations.
    /// </summary>
    private readonly StringBuilder messageBuilder = new();

    private void InitializeMessageParsers()
    {
        messageParsers = new Dictionary<string, Action<NetworkMessage, string[]>>
        {
            // GUIDS
            [CLIENTGUID] = (m, p) => m.ClientGUID = p[1],
            [SESSIONGUID] = (m, p) => m.SessionGUID = p[1],
            [NAME] = (m, p) => m.Name = p[1],
            [GUID] = (m, p) => m.GUID = p[1],
            [CLIENTGUIDLIST] = (m, p) => m.ClientGUIDList = new List<string>(p[1].Split(',')),
            [CLOSEDGUID] = (m, p) => m.ClosedGUID = p[1],
            [YOURNEWGUID] = (m, p) => m.YourNewClientGUID = p[1],
            [NEWCLIENTGUID] = (m, p) => m.NewClientGUID = p[1],
            [REMOVEGUID] = (m, p) => m.RemoveGUID = p[1],
            [SENDINGCLIENTGUID] = (m, p) => m.SendingClientGUID = p[1],
            [PARENTGUID] = (m, p) => m.ParentGUID = p[1],

            // Vectors
            [POSITION] = (m, p) => m.Position = new Vector2(float.Parse(p[1]), float.Parse(p[2])),
            [VELOCITY] = (m, p) => m.Velocity = new Vector2(float.Parse(p[1]), float.Parse(p[2])),
            [VECTOR2_1] = (m, p) => m.Vector2_1 = new Vector2(float.Parse(p[1]), float.Parse(p[2])),
            [VECTOR2_2] = (m, p) => m.Vector2_2 = new Vector2(float.Parse(p[1]), float.Parse(p[2])),
            [VECTOR2_3] = (m, p) => m.Vector2_3 = new Vector2(float.Parse(p[1]), float.Parse(p[2])),
            [VECTOR2_4] = (m, p) => m.Vector2_4 = new Vector2(float.Parse(p[1]), float.Parse(p[2])),
            [VECTOR2_5] = (m, p) => m.Vector2_5 = new Vector2(float.Parse(p[1]), float.Parse(p[2])),

            // Floats
            [ANGULARVELOCITY] = (m, p) => m.AngularVelocity = float.Parse(p[1]),
            [ROTATION] = (m, p) => m.Rotation = float.Parse(p[1]),
            [SPEED] = (m, p) => m.Speed = float.Parse(p[1]),
            [MAGNITUDE] = (m, p) => m.Magnitude = float.Parse(p[1]),
            [FLOAT1] = (m, p) => m.Float1 = float.Parse(p[1]),
            [FLOAT2] = (m, p) => m.Float2 = float.Parse(p[1]),
            [FLOAT3] = (m, p) => m.Float3 = float.Parse(p[1]),
            [FLOAT4] = (m, p) => m.Float4 = float.Parse(p[1]),
            [FLOAT5] = (m, p) => m.Float5 = float.Parse(p[1]),
            [FLOAT6] = (m, p) => m.Float6 = float.Parse(p[1]),
            [FLOAT7] = (m, p) => m.Float7 = float.Parse(p[1]),
            [FLOAT8] = (m, p) => m.Float8 = float.Parse(p[1]),
            [FLOAT9] = (m, p) => m.Float9 = float.Parse(p[1]),
            [FLOAT10] = (m, p) => m.Float10 = float.Parse(p[1]),

            // Integers
            [INT1] = (m, p) => m.Int1 = int.Parse(p[1]),
            [INT2] = (m, p) => m.Int2 = int.Parse(p[1]),
            [INT3] = (m, p) => m.Int3 = int.Parse(p[1]),
            [INT4] = (m, p) => m.Int4 = int.Parse(p[1]),
            [INT5] = (m, p) => m.Int5 = int.Parse(p[1]),

            // Booleans
            [BOOL1] = (m, p) => m.Bool1 = p[1] == "1",
            [BOOL2] = (m, p) => m.Bool2 = p[1] == "1",
            [BOOL3] = (m, p) => m.Bool3 = p[1] == "1",
            [BOOL4] = (m, p) => m.Bool4 = p[1] == "1",
            [BOOL5] = (m, p) => m.Bool5 = p[1] == "1",
            [LIVESWAP] = (m, p) => m.LiveSwap = p[1] == "1",
            [STARTGAME] = (m, p) => m.StartGame = p[1] == "1",
            [UPDATE] = (m, p) => m.Update = p[1] == "1",

            // Strings
            [ACTION] = (m, p) => m.Action = p[1],
            [ACTIONVALUE] = (m, p) => m.ActionValue = p[1],
            [QUERY] = (m, p) => m.Query = p[1],
            [QUERYRESPONSE] = (m, p) => m.QueryResponse = p[1],
            [STRING1] = (m, p) => m.String1 = p[1],
            [STRING2] = (m, p) => m.String2 = p[1],
            [STRING3] = (m, p) => m.String3 = p[1],
            [STRING4] = (m, p) => m.String4 = p[1],
            [STRING5] = (m, p) => m.String5 = p[1],
            [GAMEMESSAGE] = (m, p) => m.GameMessage = p[1],
            [GAMEVALUE] = (m, p) => m.GameValue = p[1],
            [CLIENTINDEX] = (m, p) => m.ClientIndex = int.Parse(p[1]),

            // Byte
            [HANDSHAKE] = (m, p) => m.Handshake = Convert.ToByte(p[1])
        };
    }

    private void InitializeMessageSerializers()
    {
        messageSerializers = new Dictionary<string, Func<NetworkMessage, string>>
        {
            // GUIDs - Simple string properties
            [NAME] = m => m.Name != null ? $"{NAME}:{m.Name}" : null,
            [GUID] = m => m.GUID != null ? $"{GUID}:{m.GUID}" : null,
            [TARGETGUID] = m => m.TargetGUID != null ? $"{TARGETGUID}:{m.TargetGUID}" : null,
            [NEWCLIENTGUID] = m => m.NewClientGUID != null ? $"{NEWCLIENTGUID}:{m.NewClientGUID}" : null,
            [CLOSEDGUID] = m => m.ClosedGUID != null ? $"{CLOSEDGUID}:{m.ClosedGUID}" : null,
            [YOURNEWGUID] = m => m.YourNewClientGUID != null ? $"{YOURNEWGUID}:{m.YourNewClientGUID}" : null,
            [CLIENTGUID] = m => m.ClientGUID != null ? $"{CLIENTGUID}:{m.ClientGUID}" : null,
            [SESSIONGUID] = m => m.SessionGUID != null ? $"{SESSIONGUID}:{m.SessionGUID}" : null,
            [REMOVEGUID] = m => m.RemoveGUID != null ? $"{REMOVEGUID}:{m.RemoveGUID}" : null,
            [SENDINGCLIENTGUID] = m => m.SendingClientGUID != null ? $"{SENDINGCLIENTGUID}:{m.SendingClientGUID}" : null,

            // Vectors - Use helper method
            [POSITION] = m => m.Position != null ? Vector2ToString((Vector2)m.Position, POSITION) : null,
            [VELOCITY] = m => m.Velocity != null ? Vector2ToString((Vector2)m.Velocity, VELOCITY) : null,
            [SCALE] = m => m.Scale != null ? Vector2ToString((Vector2)m.Scale, SCALE) : null,
            [VECTOR2_1] = m => m.Vector2_1 != null ? Vector2ToString((Vector2)m.Vector2_1, VECTOR2_1) : null,
            [VECTOR2_2] = m => m.Vector2_2 != null ? Vector2ToString((Vector2)m.Vector2_2, VECTOR2_2) : null,
            [VECTOR2_3] = m => m.Vector2_3 != null ? Vector2ToString((Vector2)m.Vector2_3, VECTOR2_3) : null,
            [VECTOR2_4] = m => m.Vector2_4 != null ? Vector2ToString((Vector2)m.Vector2_4, VECTOR2_4) : null,
            [VECTOR2_5] = m => m.Vector2_5 != null ? Vector2ToString((Vector2)m.Vector2_5, VECTOR2_5) : null,

            // Floats - Use helper method
            [ANGULARVELOCITY] = m => m.AngularVelocity != null ? FloatToString((float)m.AngularVelocity, ANGULARVELOCITY) : null,
            [ROTATION] = m => m.Rotation != null ? FloatToString((float)m.Rotation, ROTATION) : null,
            [SPEED] = m => m.Speed != null ? FloatToString((float)m.Speed, SPEED) : null,
            [MAGNITUDE] = m => m.Magnitude != null ? FloatToString((float)m.Magnitude, MAGNITUDE) : null,
            [FLOAT1] = m => m.Float1 != null ? FloatToString((float)m.Float1, FLOAT1) : null,
            [FLOAT2] = m => m.Float2 != null ? FloatToString((float)m.Float2, FLOAT2) : null,
            [FLOAT3] = m => m.Float3 != null ? FloatToString((float)m.Float3, FLOAT3) : null,
            [FLOAT4] = m => m.Float4 != null ? FloatToString((float)m.Float4, FLOAT4) : null,
            [FLOAT5] = m => m.Float5 != null ? FloatToString((float)m.Float5, FLOAT5) : null,
            [FLOAT6] = m => m.Float6 != null ? FloatToString((float)m.Float6, FLOAT6) : null,
            [FLOAT7] = m => m.Float7 != null ? FloatToString((float)m.Float7, FLOAT7) : null,
            [FLOAT8] = m => m.Float8 != null ? FloatToString((float)m.Float8, FLOAT8) : null,
            [FLOAT9] = m => m.Float9 != null ? FloatToString((float)m.Float9, FLOAT9) : null,
            [FLOAT10] = m => m.Float10 != null ? FloatToString((float)m.Float10, FLOAT10) : null,

            // Integers
            [INT1] = m => m.Int1 != null ? $"{INT1}:{m.Int1}" : null,
            [INT2] = m => m.Int2 != null ? $"{INT2}:{m.Int2}" : null,
            [INT3] = m => m.Int3 != null ? $"{INT3}:{m.Int3}" : null,
            [INT4] = m => m.Int4 != null ? $"{INT4}:{m.Int4}" : null,
            [INT5] = m => m.Int5 != null ? $"{INT5}:{m.Int5}" : null,

            // Booleans - Use helper method
            [LIVESWAP] = m => m.LiveSwap != null ? BooleanToString(m.LiveSwap, LIVESWAP) : null,
            [STARTGAME] = m => m.StartGame != null ? BooleanToString(m.StartGame, STARTGAME) : null,
            [UPDATE] = m => m.Update != null ? BooleanToString(m.Update, UPDATE) : null,
            [BOOL1] = m => m.Bool1 != null ? BooleanToString(m.Bool1, BOOL1) : null,
            [BOOL2] = m => m.Bool2 != null ? BooleanToString(m.Bool2, BOOL2) : null,
            [BOOL3] = m => m.Bool3 != null ? BooleanToString(m.Bool3, BOOL3) : null,
            [BOOL4] = m => m.Bool4 != null ? BooleanToString(m.Bool4, BOOL4) : null,
            [BOOL5] = m => m.Bool5 != null ? BooleanToString(m.Bool5, BOOL5) : null,

            // Strings
            [ACTION] = m => m.Action != null ? $"{ACTION}:{m.Action}" : null,
            [ACTIONVALUE] = m => m.ActionValue != null ? $"{ACTIONVALUE}:{m.ActionValue}" : null,
            [QUERY] = m => m.Query != null ? $"{QUERY}:{m.Query}" : null,
            [QUERYRESPONSE] = m => m.QueryResponse != null ? $"{QUERYRESPONSE}:{m.QueryResponse}" : null,
            [STRING1] = m => m.String1 != null ? $"{STRING1}:{m.String1}" : null,
            [STRING2] = m => m.String2 != null ? $"{STRING2}:{m.String2}" : null,
            [STRING3] = m => m.String3 != null ? $"{STRING3}:{m.String3}" : null,
            [STRING4] = m => m.String4 != null ? $"{STRING4}:{m.String4}" : null,
            [STRING5] = m => m.String5 != null ? $"{STRING5}:{m.String5}" : null,
            [GAMEMESSAGE] = m => m.GameMessage != null ? $"{GAMEMESSAGE}:{m.GameMessage}" : null,
            [GAMEVALUE] = m => m.GameValue != null ? $"{GAMEVALUE}:{m.GameValue}" : null,
            [CLIENTINDEX] = m => m.ClientIndex != null ? $"{CLIENTINDEX}:{m.ClientIndex}" : null,

            // Byte
            [HANDSHAKE] = m => m.Handshake != null ? $"{HANDSHAKE}:{m.Handshake}" : null
        };
    }
    #endregion

    #region Unity Lifecycle
    void Awake()
    {
        // Convert serialized list to dictionary on awake
        EventsInit();

        // Initialize message parsers and serializers
        InitializeMessageParsers();
        InitializeMessageSerializers();

        serverSettings = SettingsGet();


    }
    async void OnDestroy()
    {
        await Close();
    }
    public void Update()
    {
        if (!isInitialized) return;
        if (isClosing) return;
        if (!isConnected) return;
        if (isPaused) return;

        MessagesQueueConsume();
    }
    #endregion

    #region Initialization and Cleanup
    public void Init(GAMEMODE gameMode)
    {
        this.gameMode = gameMode;

        if (gameMode != GAMEMODE.SINGLEPLAYER)
        {
            NetworkInit();
        }
    }

    /// <summary>
    /// Sets the paused state for network synchronization.
    /// When paused, no outgoing sync data is sent and incoming messages are queued but not processed.
    /// </summary>
    /// <param name="paused">True to pause network sync, false to resume.</param>
    public void SetPaused(bool paused)
    {
        isPaused = paused;
        DL.Log($"Network sync {(paused ? "paused" : "resumed")}", "yellow");
    }

    private void EventsInit()
    {
        events.Clear();
        foreach (var entry in eventsList)
        {
            if (!string.IsNullOrEmpty(entry.senderID) && !events.ContainsKey(entry.senderID))
            {
                events[entry.senderID] = entry.receiverID;
            }
        }
    }
    private void NetworkInit()
    {
        #region Validation
        if (isInitialized)
        {
            DL.Log("UnifiedSyncMatrix already initialized", "red", true, true, 18);
            return;
        }
        #endregion

        #region Reset State Flags
        isClosing = false;
        isConnected = false;
        isSessionReady = false;
        isPaused = false;
        #endregion

        #region Logging
        DL.Log("NetworkInit", "magenta");
        #endregion

        #region Platform Detection
        platForm = DeterminePlatform();
        #endregion

        #region Initialize Collections
        messages = new ConcurrentQueue<string>();
        #endregion

        #region Initialize Network Core
        netCoreWrapper = new NETCoreWrapper();
        netCoreWrapper.OnMessageReceived -= OnMessageReceived;
        netCoreWrapper.OnMessageReceived += OnMessageReceived;
        netCoreWrapper.Init(serverSettings.GetUrl(), platForm);
        #endregion

        #region Initialize GUIDs
        _sessionGUID = "";
        _clientGUID = "";
        #endregion

        #region Finalize
        isInitialized = true;
        #endregion
    }
    public async Task Close()
    {
        if (isClosing) return;

        isClosing = true;
        isSessionReady = false;
        isConnected = false;

        messages?.Clear();
        messages = null;

        if (netCoreWrapper != null)
        {
            netCoreWrapper.OnMessageReceived -= OnMessageReceived;
            await netCoreWrapper.ShutDown();
            netCoreWrapper = null;
        }

        OnClientJoined = null;
        OnClientLeft = null;
        OnServerIsReady = null;

        isInitialized = false;

        DL.Log("Close======");
    }

    #endregion

    #region Configuration
    private Settings SettingsGet()
    {
        var configAsset = Resources.Load<TextAsset>("config");
        if (configAsset == null)
            throw new FileNotFoundException("Config file not found in Resources.");

        Settings settings = JsonUtility.FromJson<Settings>(configAsset.text);
        return settings;
    }

    private NETCoreWrapper.PLATFORMS DeterminePlatform()
    {
        DL.Log("Application.platform:" + Application.platform);

        return Application.platform switch
        {
            RuntimePlatform.WebGLPlayer => NETCoreWrapper.PLATFORMS.WEB,
            RuntimePlatform.OSXEditor => NETCoreWrapper.PLATFORMS.DESKTOP,
            _ => NETCoreWrapper.PLATFORMS.STEAM
        };
    }
    #endregion

    #region Connection
    public async Task GameConnect(string sessionGUID)
    {
        try
        {
            if (!isInitialized)
            {
                DL.Log("GameConnect Error: UnifiedSyncMatrix not initialized. Call Init() first.", "red", true, true, 18);
                throw new Exception("UnifiedSyncMatrix not initialized");
            }

            if (isConnected)
            {
                DL.Log("Game already connected", "red", true, true, 18);
                throw new Exception("Game already connected");
            }

            if (sessionGUID == "*NEW*")
            {
                gameMode = GAMEMODE.MULTIPLAYER_HOST;
            }

            await netCoreWrapper.Connect(sessionGUID);

            isConnected = true;
        }
        catch (Exception ex)
        {
            isSessionReady = false;
            isConnected = false;

            DL.Log("GameConnect Error:" + ex.Message, "red", true, true, 18);
        }
    }
    #endregion

    #region Network Message Handling
    private void OnMessageReceived(string message)
    {
        if (isClosing) return;

        messages.Enqueue(message);
    }
    private void MessagesQueueConsume()
    {
        if (messages.Count == 0) return;

        string message = messages.TryDequeue(out message) ? message : null;
        MessagesHandle(message);
    }

    private void MessagesHandle(string message)
    {
        if (isClosing) return;

        if (string.IsNullOrEmpty(message))
        {
            DL.Error("Message is null or empty - skipping");
            return;
        }

        if (!message.Contains(":"))
        {
            DL.Error($"Invalid message format (no colon separator): '{message}'");
            return;
        }

        var networkMessage = ParseNetworkMessage(message);

        if (consoleReceiveMessage)
            DL.Log("IN :" + message, "magenta", false, true);


        // CLIENTGUIDLIST update from server
        if (networkMessage.ClientGUIDList != null && networkMessage.ClientGUIDList.Count > 0)
        {
            clientGUIDList = networkMessage.ClientGUIDList;
            DL.Log($"Updated client list from server. Total clients: {clientGUIDList.Count}", "cyan");
        }

        // Your New Session and GUID
        if (!string.IsNullOrEmpty(networkMessage.YourNewClientGUID))
        {
            DL.Log("Server is ready:" + networkMessage.YourNewClientGUID, "magenta");
            isSessionReady = true;

            _clientGUID = networkMessage.YourNewClientGUID;
            _sessionGUID = networkMessage.SessionGUID;

            // Capture the client index from the server
            if (networkMessage.ClientIndex.HasValue)
            {
                _clientIndex = networkMessage.ClientIndex.Value;
                DL.Log($"Server assigned client index: {_clientIndex}", "cyan");
            }

            OnServerIsReady(networkMessage.YourNewClientGUID, _sessionGUID);


            // Scan and register static nodes for ALL clients (Host and Guest)
            // This ensures static objects like Levers get assigned consistent NodeIDs
            NodeScan();

            // If Guest, request the game state from Host
            if (gameMode == GAMEMODE.MULTIPLAYER_GUEST)
                SendHostARequestForGameState();

        }
        // New Client joined
        else if (!string.IsNullOrEmpty(networkMessage.NewClientGUID))
        {
            if (networkMessage.NewClientGUID != _clientGUID)
            {
                OnClientJoined(networkMessage.NewClientGUID);
            }
        }
        // Node removal notification
        else if (!string.IsNullOrEmpty(networkMessage.RemoveGUID))
        {
            if (networkMessage.ClientGUID != _clientGUID)
            {
                HandleRemoveNode(networkMessage.RemoveGUID);
            }
        }
        else if (!string.IsNullOrEmpty(networkMessage.ClosedGUID))
        {
            if (networkMessage.ClosedGUID != _clientGUID)
            {
                HandleClientClosed(networkMessage.ClosedGUID);
            }
        }
        // Standalone Game Messages (without node GUID)
        else if (!string.IsNullOrEmpty(networkMessage.GameMessage) &&
                 !string.IsNullOrEmpty(networkMessage.ClientGUID) &&
                 networkMessage.ClientGUID != _clientGUID)
        {
            string msgValue = networkMessage.GameValue ?? "null";
            DL.Log($"Processing standalone game message: {networkMessage.GameMessage} = {msgValue}", "cyan");

            // Fire game message event for standalone messages
            OnGameMessage?.Invoke(networkMessage.GameMessage, networkMessage.GameValue ?? string.Empty);

            NodeScan();
        }
        // Network Update for a specific node
        else if (!string.IsNullOrEmpty(networkMessage.GUID) &&
                 !string.IsNullOrEmpty(networkMessage.ClientGUID) &&
                 networkMessage.ClientGUID != _clientGUID)
        {
            ProcessIncomingSyncData(networkMessage);

            // Handle LiveSwap message - transfer ownership from this client to the sender
            if (networkMessage.LiveSwap == true)
            {
                HandleLiveSwap(networkMessage.GUID);
            }

            // Fire the network update event for all clients
            OnNetworkUpdate?.Invoke(networkMessage.GUID, networkMessage);

            // Also fire the receiver event if one exists (for action-based messages)
            if (!string.IsNullOrEmpty(networkMessage.Action) && !string.IsNullOrEmpty(networkMessage.ActionValue))
            {
                OnEventTriggered?.Invoke(networkMessage.ActionValue, networkMessage.Action);
            }

            // Fire game message event if present (for node-specific messages)
            if (!string.IsNullOrEmpty(networkMessage.GameMessage))
            {
                OnGameMessage?.Invoke(networkMessage.GameMessage, networkMessage.GameValue ?? string.Empty);
            }
        }
        // Global/Standalone Speed Update (without GUID and without GameMessage string)
        else if (networkMessage.Speed.HasValue &&
                 !string.IsNullOrEmpty(networkMessage.ClientGUID) &&
                 networkMessage.ClientGUID != _clientGUID)
        {
            // Fire OnGameMessage with SPEED constant as key and value as string
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            OnGameMessage?.Invoke(SPEED, networkMessage.Speed.Value.ToString());
#else
            // In build, SPEED is a short code (e.g. "p"), so we use that or mapping?
            // User likely expects "SPEED" or the value of the constant.
            // Converting to string to be safe.
            OnGameMessage?.Invoke("SPEED", networkMessage.Speed.Value.ToString());
#endif
        }
        // Handshaking Messages
        else if (networkMessage.Handshake != null)
        {
            HandleHandshakeMessage(networkMessage);
        }
    }

    private NetworkMessage ParseNetworkMessage(string data)
    {
        var networkMessage = new NetworkMessage();

        int dataLength = data.Length;
        int currentIndex = 0;

        while (currentIndex < dataLength)
        {
            // Find the next pipe delimiter
            int pipeIndex = data.IndexOf('|', currentIndex);
            if (pipeIndex == -1)
                pipeIndex = dataLength;

            // Skip empty segments
            if (pipeIndex == currentIndex)
            {
                currentIndex++;
                continue;
            }

            // Find the colon separator within this property
            int colonIndex = data.IndexOf(':', currentIndex);

            // Ensure colon is within the current property segment
            if (colonIndex == -1 || colonIndex >= pipeIndex)
            {
                currentIndex = pipeIndex + 1;
                continue;
            }

            // Extract property key using Substring (one allocation per property)
            string property = data.Substring(currentIndex, colonIndex - currentIndex);

            // Use dictionary lookup instead of switch statement
            if (messageParsers.TryGetValue(property, out var parser))
            {
                try
                {
                    // Create a string array compatible with existing parsers
                    // This maintains backward compatibility with the parser delegates
                    int secondColonIndex = data.IndexOf(':', colonIndex + 1);
                    string[] parse;

                    if (secondColonIndex != -1 && secondColonIndex < pipeIndex)
                    {
                        // Three-part property (e.g., "POSIT:1.5:2.3")
                        parse = new string[3];
                        parse[0] = property;
                        parse[1] = data.Substring(colonIndex + 1, secondColonIndex - colonIndex - 1);
                        parse[2] = data.Substring(secondColonIndex + 1, pipeIndex - secondColonIndex - 1);
                    }
                    else
                    {
                        // Two-part property (e.g., "NAM:value")
                        parse = new string[2];
                        parse[0] = property;
                        parse[1] = data.Substring(colonIndex + 1, pipeIndex - colonIndex - 1);
                    }

                    parser(networkMessage, parse);
                }
                catch (Exception ex)
                {
                    DL.Log($"Error parsing property '{property}': {ex.Message}", "red", true, true, 18);
                }
            }

            // Move to next property
            currentIndex = pipeIndex + 1;
        }

        return networkMessage;
    }

    #endregion

    #region Network State Management
    public void SendNetworkData(ref string message)
    {
        if (!isSessionReady) return;

        if (consoleSendMessage)
            DL.Log($"OUT:{message}", "magenta");

        netCoreWrapper.SendToAll(message);
    }
    public void SendNetworkData(ref NetworkMessage networkMessage)
    {
        if (!isSessionReady) return;

        // DEFENSIVE CHECK: Never send messages without ClientGUID
        if (string.IsNullOrEmpty(networkMessage.ClientGUID))
        {
            DL.Log($"SendNetworkData: Attempted to send message without ClientGUID (NodeID: {networkMessage.GUID}). Skipping.", "yellow");
            return;
        }

        string message = CreateStringFromNetworkMessage(ref networkMessage);

        if (consoleSendMessage)
            DL.Log($"OUT:{message}", "magenta");

        netCoreWrapper.SendToAll(message);
    }

    /// <summary>
    /// Sends a game-specific message to all connected clients.
    /// </summary>
    /// <param name="gameMessage">The game message identifier/type.</param>
    /// <param name="gameValue">Optional value associated with the game message.</param>
    public void SendGameMessage(string gameMessage, string gameValue = null)
    {
        if (!isSessionReady) return;

        if (string.IsNullOrEmpty(gameMessage))
        {
            DL.Log("SendGameMessage: gameMessage cannot be null or empty", "yellow");
            return;
        }

        var networkMessage = new NetworkMessage
        {
            ClientGUID = _clientGUID,
            GameMessage = gameMessage,
            GameValue = gameValue
        };

        SendNetworkData(ref networkMessage);
    }

    #endregion

    #region Event Dispatch
    public void Call(string senderID, object value = null)
    {
        if (events.TryGetValue(senderID, out string receiverID))
        {
            if (value == null) value = "";
            // Fire local event notification
            OnEventTriggered?.Invoke(receiverID, value?.ToString());

        }
        else
        {
            Debug.LogWarning($"Event '{senderID}' not found in UnifiedSyncMatrix.");
        }
    }
    #endregion

    #region Handshake Management

    #region SHARED - Handshake Coordination
    /// <summary>
    /// Handles all handshake messages and routes them to the appropriate handler.
    /// </summary>
    /// <param name="networkMessage">The network message containing handshake data.</param>
    private void HandleHandshakeMessage(NetworkMessage networkMessage)
    {
        var handshakeValue = (HANDSHAKES)networkMessage.Handshake;

        switch (handshakeValue)
        {
            case HANDSHAKES.REQUEST:
                if (gameMode == GAMEMODE.MULTIPLAYER_HOST)
                    HandleHandshakeRequest();
                break;

            case HANDSHAKES.COMPLETE:
                if (gameMode == GAMEMODE.MULTIPLAYER_GUEST)
                    HandleHandshakeComplete();
                break;
        }
    }
    #endregion

    #region HOST - Send Game State to Guest
    /// <summary>
    /// Handles handshake REQUEST message on the host.
    /// Sends the game state to the requesting guest.
    /// </summary>
    private void HandleHandshakeRequest()
    {
        HostSendGuestGameState();
    }

    /// <summary>
    /// Sends the current game state (all node sync data) to the guest client.
    /// Called by the host when a guest requests the game state.
    /// </summary>
    private void HostSendGuestGameState()
    {
        // Clean up any destroyed objects from nodeList before sending
        CleanupDestroyedNodes();

        // Get all nodes from the dictionary
        var nodes = GetAllNodes();

        if (nodes == null || nodes.Count == 0)
        {
            DL.Log("HostSendGuestGameState: No nodes found to sync. Make sure GameObjects have USMNode components attached!", "yellow");
            SendHandshakeComplete();
            return;
        }

        // Send individual sync data for each node
        int sentCount = 0;
        int skippedNull = 0;
        int skippedPlayer = 0;
        int skippedInvalidType = 0;

        foreach (var node in nodes)
        {
            GameObject nodeObject = node.Value;
            // Check if GameObject is null or has been destroyed (Unity's == operator checks for destroyed objects)
            if (nodeObject == null)
            {
                skippedNull++;
                DL.Log($"HostSendGuestGameState: Skipping null/destroyed node with ID '{node.Key}'", "yellow");
                continue;
            }

            USMNode usmNode = nodeObject.GetComponent<USMNode>();
            if (usmNode == null) continue;

            // RULE 1: Send ALL Players during handshake (both live and remote copies)
            // This ensures newly joining clients see all existing players in the session

            // RULE 2: Only send STATIC & ACTIVE gameobjects
            if (usmNode.NodeType != USMNode.NODETYPE.STATIC && usmNode.NodeType != USMNode.NODETYPE.ACTIVE)
            {
                skippedInvalidType++;
                DL.Log($"HostSendGuestGameState: Skipping '{nodeObject.name}' (ID: {node.Key}) - Invalid NodeType: {usmNode.NodeType}", "yellow");
                continue;
            }

            // Get the cached sync data from the node
            NetworkMessage syncData = usmNode.GetCachedSyncData();

            // Only send if there's actual data to sync
            if (syncData != null)
            {
                // Ensure the GUID, ClientGUID, and Name are set
                syncData.GUID = node.Key;
                syncData.ClientGUID = _clientGUID;
                syncData.Name = nodeObject.name; // Include GameObject name for guest instantiation
                syncData.Handshake = (int?)HANDSHAKES.SENDING;

                SendNetworkData(ref syncData);

                sentCount++;

                DL.Log($"HostSendGuestGameState: Sent sync data for '{nodeObject.name}' (Type: {usmNode.NodeType}, ID: {node.Key})", "cyan");
            }
        }

        DL.Log($"HostSendGuestGameState: Complete - Sent: {sentCount}, Skipped (null): {skippedNull}, Skipped (players): {skippedPlayer}, Skipped (invalid type): {skippedInvalidType}", "green");

        // Send completion message to signal all sync data has been sent
        SendHandshakeComplete();
    }

    /// <summary>
    /// Sends the handshake completion signal to notify the guest that all sync data has been sent.
    /// </summary>
    private void SendHandshakeComplete()
    {
        var completeMessage = new NetworkMessage
        {
            ClientGUID = _clientGUID,
            SessionGUID = _sessionGUID,
            Handshake = (byte)HANDSHAKES.COMPLETE
        };

        SendNetworkData(ref completeMessage);

        DL.Log("SendHandshakeComplete: Sent handshake completion signal", "magenta");
    }
    #endregion

    #region GUEST - Receive and Sync Game State

    /// <summary>
    /// Handles handshake COMPLETE message on the guest.
    /// Called when the host has finished sending all sync data.
    /// This marks the handshake as complete and finalizes scene synchronization.
    /// </summary>
    private void HandleHandshakeComplete()
    {
        DL.Log("GuestReceiveGameState: Handshake complete, all sync data received from host", "magenta");

        // Find all USMNode objects in the scene
        var allNodes = FindObjectsByType<USMNode>(FindObjectsSortMode.InstanceID);

        int destroyedCount = 0;
        int syncedCount = 0;
        int playerCount = 0;

        // DL.Log($"GuestReceiveGameState: Found {allNodes.Length} total USMNode objects in scene", "cyan");
        // DL.Log($"GuestReceiveGameState: nodeList has {nodeList.Count} nodes synced from host", "cyan");

        foreach (USMNode node in allNodes)
        {
            string nodeID = node.NodeID;

            if (string.IsNullOrEmpty(nodeID))
            {
                // Shouldn't happen after NodeScan, but handle it
                // DL.Log($"GuestReceiveGameState: WARNING - '{node.gameObject.name}' has no NodeID", "yellow");
                continue;
            }

            // Check if this NodeID is in the nodeList (received from host during handshake)
            if (nodeList.ContainsKey(nodeID))
            {
                // Host sent sync data for this NodeID - object exists on host
                syncedCount++;
                // DL.Log($"GuestReceiveGameState: '{node.gameObject.name}' (NodeID: '{nodeID}') synced with host", "green");
            }


            if (node.CompareTag("Player"))
            {
                // Keep guest player objects (they're local to this client)
                playerCount++;
                // DL.Log($"GuestReceiveGameState: Keeping guest Player '{node.gameObject.name}' (NodeID: '{nodeID}')", "cyan");
            }
            else if (!node.Scanned)
            {
                // STATIC level objects that host didn't send = destroyed on host
                // DL.Log($"GuestReceiveGameState: Destroying STATIC object '{node.gameObject.name}' (NodeID: '{nodeID}') - not present on host", "orange");

                // Important: Don't call UnregisterNode as that would send a network message
                // Just destroy locally
                Destroy(node.gameObject);
                destroyedCount++;
            }
            else
            {
                // Dynamic objects without sync data
                // DL.Log($"GuestReceiveGameState: '{node.gameObject.name}' (NodeID: '{nodeID}') not synced but keeping (type: {node.NodeType})", "yellow");
            }
        }

        // DL.Log($"GuestReceiveGameState: Scene sync complete - Synced: {syncedCount}, Players: {playerCount}, Destroyed: {destroyedCount}", "green");
        // DL.Log($"GuestReceiveGameState: Final nodeList count: {nodeList.Count}", "green");
    }

    /// <summary>
    /// Sends a request to the host to receive the current game state.
    /// Called by the guest when joining the session.
    /// </summary>
    private void SendHostARequestForGameState()
    {
        var networkMessage = new NetworkMessage
        {
            Handshake = (byte)HANDSHAKES.REQUEST,
            ClientGUID = _clientGUID,
            SessionGUID = _sessionGUID
        };

        SendNetworkData(ref networkMessage);
    }

    #endregion

    #region SHARED - Node Processing Helpers
    /// <summary>
    /// Processes incoming sync data from other clients.
    /// If the node exists locally (static objects), properties are applied via OnNetworkUpdate event.
    /// If the node doesn't exist (dynamic objects like flares/projectiles), instantiates it from Resources.
    /// </summary>
    /// <param name="networkMessage">The network message containing sync data.</param>
    private void ProcessIncomingSyncData(NetworkMessage networkMessage)
    {
        // Defensive check - never process our own messages
        if (networkMessage.ClientGUID == _clientGUID)
        {
            DL.Log($"ProcessIncomingSyncData: Ignoring own message for NodeID '{networkMessage.GUID}'", "cyan");
            return;
        }

        string nodeID = networkMessage.GUID;

        if (string.IsNullOrEmpty(nodeID))
        {
            DL.Log("ProcessIncomingSyncData: Received sync data with no NodeID", "red");
            return;
        }

        // If node already exists locally, OnNetworkUpdate event will handle property updates
        // For STATIC objects: Both host and guest have matching NodeIDs from NodeScan
        // For ACTIVE objects: Created dynamically and registered
        if (nodeList.ContainsKey(nodeID) && networkMessage.Handshake == (int?)HANDSHAKES.SENDING)
        {
            nodeList[nodeID].GetComponent<USMNode>().SetScanned(true);

            return;
        }

        // Node doesn't exist in our nodeList
        // This can happen for:
        // 1. Guest receiving initial sync during handshake for STATIC objects
        // 2. Dynamic objects (flares, projectiles) created by other clients

        // For guests during handshake: STATIC objects already have NodeIDs from NodeScan
        // but aren't in nodeList yet. Add them now.
        if (gameMode == GAMEMODE.MULTIPLAYER_GUEST && !string.IsNullOrEmpty(networkMessage.Name))
        {
            // Try to find a scene object with this exact NodeID
            var allNodes = FindObjectsByType<USMNode>(FindObjectsSortMode.InstanceID);
            foreach (USMNode node in allNodes)
            {
                if (node.NodeID == nodeID && node.gameObject.name == networkMessage.Name)
                {
                    // Found matching STATIC object - register it
                    nodeList[nodeID] = node.gameObject;
                    node.SetLive(false); // Guest copies are not live
                    node.SetScanned(true); // Mark as scanned during handshake
                    DL.Log($"ProcessIncomingSyncData: Registered STATIC object '{networkMessage.Name}' (NodeID: '{nodeID}')", "green");
                    return;
                }
            }
        }

        // No matching scene object found - instantiate it (dynamic objects like flares/projectiles)
        if (string.IsNullOrEmpty(networkMessage.Name))
        {
            // DL.Log($"ProcessIncomingSyncData: NodeID '{nodeID}' not found and no prefab name provided", "yellow");
            return;
        }

        InstantiateMissingNodeFromSync(nodeID, networkMessage.Name, networkMessage);
    }

    /// <summary>
    /// Instantiates a missing node from Resources during network sync and registers it with the specified NodeID.
    /// Called when receiving sync data for a node that doesn't exist locally (dynamic objects and remote players).
    /// Creates non-live copy objects that mirror remote objects.
    /// </summary>
    /// <param name="nodeID">The NodeID to assign to the new node.</param>
    /// <param name="gameObjectName">The name of the GameObject prefab to instantiate.</param>
    /// <param name="networkMessage">The network message containing position and other sync data.</param>
    private void InstantiateMissingNodeFromSync(string nodeID, string gameObjectName, NetworkMessage networkMessage)
    {
        // Remove "(Clone)" suffix if present
        string prefabName = gameObjectName.Replace("(Clone)", "").Trim();

        // Remove numeric suffixes (e.g., "CrateHitBoxes-1" -> "CrateHitBoxes")
        // This handles cases where Unity adds -1, -2, etc. to duplicate object names
        int lastDashIndex = prefabName.LastIndexOf('-');
        if (lastDashIndex > 0)
        {
            string suffix = prefabName[(lastDashIndex + 1)..];
            // Only remove if the suffix is purely numeric
            if (int.TryParse(suffix, out _))
            {
                prefabName = prefabName[..lastDashIndex];
            }
        }

        // Try to load the prefab from Resources
        GameObject prefab = LoadPrefabFromResources(prefabName);
        if (prefab == null)
        {
            DL.Log($"InstantiateMissingNodeFromSync: Prefab '{prefabName}' not found in Resources (original name: '{gameObjectName}')", "red");
            return;
        }

        DL.Log($"InstantiateMissingNodeFromSync: Creating copy of '{prefabName}' with NodeID '{nodeID}' from remote client", "cyan");

        // Create and register the new node from network (includes remote player copies)
        CreateAndRegisterNodeFromHost(prefab, gameObjectName, nodeID, networkMessage);
    }

    /// <summary>
    /// Creates a new GameObject instance from a prefab and registers it with a specific NodeID received from network.
    /// This creates a copy (non-live) object that mirrors an object created by another client.
    /// </summary>
    /// <param name="prefab">The prefab to instantiate.</param>
    /// <param name="gameObjectName">The name to assign to the new GameObject.</param>
    /// <param name="nodeID">The NodeID received from the network.</param>
    /// <param name="networkMessage">The network message containing position and other sync data.</param>
    private void CreateAndRegisterNodeFromHost(GameObject prefab, string gameObjectName, string nodeID, NetworkMessage networkMessage)
    {
        // Instantiate at origin first
        GameObject newObject = Instantiate(prefab, Vector3.zero, Quaternion.identity);
        newObject.name = gameObjectName.Replace("(Clone)", "").Trim();

        // Get or add USMNode component
        if (!newObject.TryGetComponent<USMNode>(out USMNode node))
        {
            // DL.Log($"CreateAndRegisterNodeFromHost: Object '{gameObjectName}' has no USMNode, adding one", "yellow");
            node = newObject.AddComponent<USMNode>();
        }

        bool isHostSending = networkMessage.Handshake == (byte)HANDSHAKES.SENDING;
        // Set the NodeID from network (but NOT live - this is a copy)
        node.SetClientGUID(networkMessage.ClientGUID);
        node.SetNodeID(nodeID);
        node.SetLive(false); // Explicitly mark as copy object
        node.SetScanned(isHostSending); // Mark as scanned if during handshake
        node.Init();
        // Set parent for copy objects BEFORE setting position
        if (newObject.CompareTag("Player") || node.NodeType != USMNode.NODETYPE.STATIC)
        {
            newObject.transform.SetParent(Folders.PLAYERS);
        }
        else
        {
            // Static/level objects go in the LEVEL folder
            newObject.transform.SetParent(Folders.LEVEL);
        }

        // Now set the position from network message (after parenting)
        // This ensures the position is in world space regardless of parent hierarchy
        if (networkMessage.Position.HasValue)
        {
            Vector3 worldPosition = new Vector3(networkMessage.Position.Value.x, networkMessage.Position.Value.y, 0);
            newObject.transform.localPosition = worldPosition;
        }

        // Register in nodeList
        nodeList[nodeID] = newObject;

        // string positionInfo = networkMessage.Position.HasValue
        //     ? $"at position {networkMessage.Position.Value}"
        //     : "at default position";
        // DL.Log($"CreateAndRegisterNodeFromHost: Created copy of '{gameObjectName}' with NodeID '{nodeID}' {positionInfo}", "green");
    }
    #endregion

    #endregion

    #region Node ID Management
    /// <summary>
    /// Generates a NodeID string based on the current counter value.
    /// Pattern: 0-9 (single digits), then a-z, A-Z (single characters), then 00-99, 0a-0z, 0A-0Z, etc. (two characters)
    /// Continues to 3+ characters as needed (000, 001, etc.)
    /// Optimized to use Append instead of Insert(0).
    /// </summary>
    /// <returns>A string representation of the NodeID.</returns>
    private string GenerateNodeID()
    {
        const string chars = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
        int counter = nextNodeIDCounter++;
        int baseCount = chars.Length; // 62

        // Single character range: 0-61 (0-9, a-z, A-Z)
        if (counter < baseCount)
        {
            return chars[counter].ToString();
        }

        // Multi-character range: 62+
        // Subtract the single character range
        counter -= baseCount;

        // Determine how many characters we need by finding which "block" we're in
        // Block 1 (2 chars): 0-3843 (62^2 = 3844 combinations: 00-ZZ)
        // Block 2 (3 chars): 3844-242171 (62^3 = 238328 combinations: 000-ZZZ)
        // etc.

        int numChars = 2;
        int blockSize = baseCount * baseCount; // Start with 2-char block size (3844)
        int runningTotal = 0;

        // Find which block (number of characters) we're in
        while (counter >= runningTotal + blockSize)
        {
            runningTotal += blockSize;
            numChars++;
            blockSize *= baseCount;
        }

        // Get position within the current block
        int positionInBlock = counter - runningTotal;

        // Build the result string from left to right using Append, then reverse
        StringBuilder result = new StringBuilder(numChars);
        for (int i = 0; i < numChars; i++)
        {
            result.Append(chars[positionInBlock % baseCount]);
            positionInBlock /= baseCount;
        }

        // Reverse the string to get correct order
        int length = result.Length;
        for (int i = 0; i < length / 2; i++)
        {
            int oppositeIndex = length - 1 - i;
            char temp = result[i];
            result[i] = result[oppositeIndex];
            result[oppositeIndex] = temp;
        }

        return result.ToString();
    }

    /// <summary>
    /// Scans all GameObjects in the scene for USMNode components and initializes them with unique IDs.
    /// Populates the nodeList dictionary with NodeID to GameObject mappings.
    /// Uses hierarchy-based sorting to ensure deterministic order that matches across host and guest.
    /// </summary>
    public void NodeScan()
    {
        // Reset the counter to ensure deterministic IDs starting from 0
        nextNodeIDCounter = 0;

        // Clear the existing node list
        nodeList.Clear();

        // Find all USMNode components in the scene
        var nodes = FindObjectsByType<USMNode>(FindObjectsSortMode.None);

        // Sort deterministically by hierarchy path and sibling index
        // This ensures the same order on both host and guest since they start with the same scene
        var sortedNodes = nodes.OrderBy(node => GetHierarchyPath(node.transform)).ToList();

        DL.Log($"NodeScan: Found {sortedNodes.Count} USMNode components in scene", "cyan");

        string nodeID = string.Empty;
        foreach (USMNode node in sortedNodes)
        {
            // Generate NodeID for STATIC nodes (non-Player) that don't have one
            if (!node.CompareTag("Player") && node.NodeType == USMNode.NODETYPE.STATIC)
            {
                nodeID = GenerateNodeID();
                node.SetNodeID(nodeID);
            }
            // For ACTIVE nodes or Players, use existing NodeID or generate one if empty
            else
            {
                nodeID = node.NodeID;

                // If the node doesn't have a NodeID yet, generate one
                // This prevents RegisterNode() from generating a duplicate ID later
                if (string.IsNullOrEmpty(nodeID))
                {
                    nodeID = GenerateNodeID();
                    node.SetNodeID(nodeID);
                }
            }


            if (gameMode == GAMEMODE.MULTIPLAYER_HOST && !node.CompareTag("Player"))
            {
                node.SetLive(true);
            }

            node.SetClientGUID(_clientGUID);


            // Add the node to the nodeList dictionary
            nodeList[nodeID] = node.gameObject;

            // Initialize the node to subscribe to network updates and register
            node.Init();
        }

        DL.Log($"NodeScan complete: {sortedNodes.Count} nodes initialized and registered", "magenta");
    }

    /// <summary>
    /// Generates a deterministic hierarchy path for sorting.
    /// Format: "SiblingIndex_ObjectName/SiblingIndex_ObjectName/..."
    /// This ensures consistent ordering across different clients loading the same scene.
    /// Optimized to use Append and then reverse instead of Insert(0).
    /// </summary>
    /// <param name="transform">The transform to generate a path for.</param>
    /// <returns>A deterministic string representing the object's position in the hierarchy.</returns>
    private string GetHierarchyPath(Transform transform)
    {
        List<string> pathParts = new List<string>();
        Transform current = transform;

        // Build the path from leaf to root (reversed order)
        while (current != null)
        {
            // Use sibling index padded to 8 digits to ensure correct string sorting
            string part = $"{current.GetSiblingIndex():D8}_{current.name}";
            pathParts.Add(part);
            current = current.parent;
        }

        // Reverse to get root-to-leaf order
        pathParts.Reverse();

        return string.Join("/", pathParts);
    }


    /// <summary>
    /// Gets all registered nodes in the nodeList dictionary.
    /// </summary>
    /// <returns>Dictionary mapping NodeID strings to GameObjects.</returns>
    public Dictionary<string, GameObject> GetAllNodes()
    {
        return nodeList;
    }

    /// <summary>
    /// Gets the total number of registered nodes.
    /// </summary>
    public int GetNodeCount()
    {
        return nodeList.Count;
    }

    /// <summary>
    /// Gets a read-only list of all event mappings (sender ID to receiver ID).
    /// </summary>
    /// <returns>Read-only list of StringStringEntry objects representing event mappings.</returns>
    public IReadOnlyList<StringStringEntry> GetEventsList()
    {
        return eventsList.AsReadOnly();
    }

    /// <summary>
    /// Gets the current client GUID.
    /// </summary>
    /// <returns>The client GUID string, or empty string if not connected.</returns>
    public string GetClientGUID()
    {
        return _clientGUID ?? string.Empty;
    }

    /// <summary>
    /// Gets the list of all connected client GUIDs from the server.
    /// </summary>
    /// <returns>A new list copy of client GUIDs, or an empty list if not available.</returns>
    public List<string> GetClientGUIDList()
    {
        return clientGUIDList != null ? new List<string>(clientGUIDList) : new List<string>();
    }

    /// <summary>
    /// Gets the current session GUID.
    /// </summary>
    /// <returns>The session GUID string, or empty string if not connected.</returns>
    public string GetSessionGUID()
    {
        return _sessionGUID ?? string.Empty;
    }

    /// <summary>
    /// Instantiates a GameObject from Resources and automatically registers it with the USM network system.
    /// NodeID and Live status are determined automatically based on game mode and context.
    /// </summary>
    /// <param name="prefabName">The name of the prefab in Resources to instantiate.</param>
    /// <param name="position">Optional spawn position. If null, spawns at Vector3.zero.</param>
    /// <param name="parent">Optional parent transform for the instantiated object.</param>
    /// <param name="forceIsLive">Optional override for Live status. If true, creates a live object regardless of game mode. Use this when guests need to spawn their own authoritative objects (flares, projectiles they fire, etc.). Default is false (auto-detect based on game mode).</param>
    /// <returns>The instantiated GameObject, or null if instantiation failed.</returns>
    public GameObject InstantiateNetworkedObject(string prefabName, Vector3? position = null, Transform parent = null, bool forceIsLive = false)
    {
        if (string.IsNullOrEmpty(prefabName))
        {
            DL.Log("InstantiateNetworkedObject: prefabName is null or empty", "red", true, true, 18);
            return null;
        }

        // Load the prefab from Resources
        GameObject prefab = Helpers.LoadGameObjectFromResources(prefabName);
        if (prefab == null)
        {
            DL.Log($"InstantiateNetworkedObject: Failed to load prefab '{prefabName}' from Resources", "red", true, true, 18);
            return null;
        }

        // Instantiate the GameObject
        // Use PLAYERS folder as default parent if no parent specified
        Transform targetParent = parent != null ? parent : Folders.PLAYERS;
        GameObject instance = Instantiate(prefab, position ?? Vector3.zero, Quaternion.identity, targetParent);
        instance.name = prefabName; // Remove "(Clone)" suffix

        // Get or add USMNode component
        USMNode node = instance.TryGetComponent<USMNode>(out var existingNode) ? existingNode : null;
        if (node == null)
        {
            DL.Log($"InstantiateNetworkedObject: Prefab '{prefabName}' has no USMNode component, adding one", "yellow");
            node = instance.AddComponent<USMNode>();
        }

        // Automatically determine NodeID and Live status
        // NodeID is always auto-generated for new instantiated objects
        string nodeID = Guid.NewGuid().ToString().ToUpper()[..4];
        node.SetNodeID(nodeID);

        // Live status logic:
        // 1. If forceIsLive is true, always create a live object (guest spawning their own flare/projectile)
        // 2. Otherwise, auto-detect based on game mode:
        //    - Hosts create LIVE objects (original authoritative versions)
        //    - Guests create COPY objects (replicated from network)
        //    - Single-player creates LIVE objects
        bool isLive = forceIsLive || gameMode == GAMEMODE.MULTIPLAYER_HOST || gameMode == GAMEMODE.SINGLEPLAYER;
        node.SetLive(isLive);

        // Set ClientGUID for the node
        node.SetClientGUID(_clientGUID);

        // Register the node in nodeList
        nodeList[nodeID] = instance;

        // Initialize the node to subscribe to network updates and register
        // This is necessary for both STATIC and ACTIVE nodes to receive network sync
        node.Init();

        DL.Log($"InstantiateNetworkedObject: Created '{prefabName}' with NodeID '{nodeID}' (Live: {isLive}, Forced: {forceIsLive})", "green");

        // If this is a live object and we're in multiplayer, send network update
        if (isLive && isSessionReady && gameMode != GAMEMODE.SINGLEPLAYER)
        {
            // Get initial sync data and broadcast it
            NetworkMessage syncData = node.GetCachedSyncData();
            if (syncData != null)
            {
                syncData.GUID = nodeID;
                syncData.ClientGUID = _clientGUID;
                syncData.Name = instance.name;
                SendNetworkData(ref syncData);
                DL.Log($"InstantiateNetworkedObject: Broadcasted creation of '{prefabName}' to network", "cyan");
            }
        }

        return instance;
    }

    /// <summary>
    /// Registers a GameObject with a USMNode component and assigns it a unique NodeID.
    /// </summary>
    /// <param name="gameObject">The GameObject to register.</param>
    /// <param name="nodeID">Optional specific NodeID to assign. If null, auto-increments the counter.</param>
    /// <returns>True if registration was successful, false if the GameObject has no USMNode component.</returns>
    public bool RegisterNode(GameObject gameObject)
    {
        if (gameObject == null)
        {
            DL.Log("RegisterNode: GameObject is null", "red", true, true, 18);
            return false;
        }

        USMNode node = gameObject.GetComponent<USMNode>();
        if (node == null)
        {
            // DL.Log($"RegisterNode: GameObject '{gameObject.name}' has no USMNode component", "yellow");
            return false;
        }
        string nodeID = node.NodeID;

        if (string.IsNullOrEmpty(nodeID))
        {
            // Generate a new NodeID
            nodeID = Guid.NewGuid().ToString().ToUpper()[..4]; // GenerateNodeID();

            // Set the NodeID on the USMNode component
            node.SetNodeID(nodeID);
        }

        // Add to the nodeList dictionary
        nodeList[nodeID] = gameObject;

        // DL.Log($"RegisterNode: Assigned NodeID '{nodeID}' to '{gameObject.name}'", "green");
        return true;
    }

    /// <summary>
    /// Unregisters a node from the nodeList and notifies other clients if appropriate.
    /// Called when a GameObject with a USMNode is being destroyed.
    /// Automatically detects if this is a network-initiated destruction to prevent loops.
    /// </summary>
    /// <param name="nodeID">The NodeID to unregister.</param>
    public void UnregisterNode(string nodeID, bool syncNetwork = false)
    {
        if (string.IsNullOrEmpty(nodeID))
        {
            DL.Log("UnregisterNode: NodeID is null or empty", "yellow");
            return;
        }

        // Check if the node is still in the list
        // If not, it was already removed by HandleRemoveNode, so we don't notify network
        bool shouldNotifyNetwork = nodeList.ContainsKey(nodeID);

        if (!shouldNotifyNetwork)
        {
            DL.Log($"UnregisterNode: NodeID '{nodeID}' already removed (network-initiated destruction)", "cyan");
            return;
        }

        GameObject obj = nodeList[nodeID];
        string objName = obj != null ? obj.name : "null";

        // Remove from the nodeList
        nodeList.Remove(nodeID);

        // DL.Log($"UnregisterNode: Removed NodeID '{nodeID}' ('{objName}') from nodeList", "cyan");

        // Notify other clients to destroy this object (only for local-initiated destruction)
        if (isSessionReady && gameMode != GAMEMODE.SINGLEPLAYER && syncNetwork)
        {
            var networkMessage = new NetworkMessage
            {
                GUID = nodeID,
                ClientGUID = _clientGUID,
                RemoveGUID = nodeID
            };

            SendNetworkData(ref networkMessage);
            // DL.Log($"UnregisterNode: Notified network to remove NodeID '{nodeID}'", "magenta");
        }
    }

    /// <summary>
    /// Handles incoming network messages to remove a node.
    /// Called when another client destroys a GameObject.
    /// </summary>
    /// <param name="nodeID">The NodeID to remove.</param>
    private void HandleRemoveNode(string nodeID)
    {
        if (string.IsNullOrEmpty(nodeID))
        {
            DL.Log("HandleRemoveNode: NodeID is null or empty", "yellow");
            return;
        }

        if (!nodeList.TryGetValue(nodeID, out GameObject obj))
        {
            // This is normal - object may have been destroyed locally already or never existed
            DL.Log($"HandleRemoveNode: NodeID '{nodeID}' already removed or doesn't exist (normal in race conditions)", "cyan");
            return;
        }

        if (obj == null)
        {
            // Object already destroyed, just clean up the dictionary
            nodeList.Remove(nodeID);
            DL.Log($"HandleRemoveNode: Cleaned up null reference for NodeID '{nodeID}'", "cyan");
            return;
        }

        string objName = obj.name;

        // Remove from nodeList FIRST - this prevents UnregisterNode from notifying network
        // When OnDestroy is called, UnregisterNode will see the node is already removed
        nodeList.Remove(nodeID);

        // Destroy the GameObject - this will trigger OnDestroy which calls UnregisterNode
        // but UnregisterNode will see the node is already removed and won't notify network
        Destroy(obj);

        DL.Log($"HandleRemoveNode: Destroyed GameObject '{objName}' (NodeID: {nodeID}) from network command", "orange");
    }

    /// <summary>
    /// Handles cleanup when a client disconnects from the session.
    /// Only destroys Player objects owned by the disconnected client.
    /// Other objects (projectiles, level objects, etc.) are kept in the scene.
    /// </summary>
    /// <param name="closedClientGUID">The ClientGUID of the disconnected client.</param>
    private void HandleClientClosed(string closedClientGUID)
    {
        if (string.IsNullOrEmpty(closedClientGUID))
        {
            DL.Log("HandleClientClosed: ClosedClientGUID is null or empty", "yellow");
            return;
        }

        DL.Log($"HandleClientClosed: Client '{closedClientGUID}' disconnected, cleaning up Player objects", "magenta");

        // Find all nodes in the scene
        var allNodes = FindObjectsByType<USMNode>(FindObjectsSortMode.InstanceID);
        int playersDestroyed = 0;
        List<string> nodesToRemove = new List<string>();

        foreach (USMNode node in allNodes)
        {
            if (node == null || node.gameObject == null)
                continue;

            // Check if this node belongs to the disconnected client
            // Note: We need to track which client created which node
            // For now, we'll destroy all Player-tagged objects that aren't the local player
            if (node.CompareTag("Player") && !node.LiveNode)
            {
                string nodeID = node.NodeID;

                if (!string.IsNullOrEmpty(nodeID))
                {
                    nodesToRemove.Add(nodeID);

                    DL.Log($"HandleClientClosed: Destroying Player '{node.gameObject.name}' (NodeID: {nodeID})", "orange");

                    // Remove from nodeList first to prevent UnregisterNode from sending network message
                    if (nodeList.ContainsKey(nodeID))
                    {
                        nodeList.Remove(nodeID);
                    }

                    Destroy(node.gameObject);
                    playersDestroyed++;
                }
            }
        }

        DL.Log($"HandleClientClosed: Destroyed {playersDestroyed} Player objects from disconnected client", "magenta");

        // If we're the host, reassign ownership of all objects from disconnected client
        if (gameMode == GAMEMODE.MULTIPLAYER_HOST)
        {
            ReassignOwnershipToHost(closedClientGUID);
        }

        // Fire OnClientLeft event
        OnClientLeft?.Invoke(closedClientGUID);
    }

    /// <summary>
    /// HOST ONLY: Reassigns ownership of all network objects from a disconnected client to the host.
    /// Scans all STATIC and ACTIVE nodes and updates their cached ClientGUID to the host's ClientGUID,
    /// then sets them to live (authoritative) status.
    /// </summary>
    /// <param name="disconnectedClientGUID">The ClientGUID of the disconnected client.</param>
    private void ReassignOwnershipToHost(string disconnectedClientGUID)
    {
        if (gameMode != GAMEMODE.MULTIPLAYER_HOST)
            return;

        if (string.IsNullOrEmpty(disconnectedClientGUID))
        {
            DL.Log("ReassignOwnershipToHost: disconnectedClientGUID is null or empty", "red");
            return;
        }

        DL.Log($"ReassignOwnershipToHost: Reassigning ownership from client '{disconnectedClientGUID}' to host '{_clientGUID}'", "orange");

        // Find all nodes in the scene
        var allNodes = FindObjectsByType<USMNode>(FindObjectsSortMode.InstanceID);
        int reassignedCount = 0;

        foreach (USMNode node in allNodes)
        {
            if (node == null || node.gameObject == null)
                continue;

            // Get the cached sync data to check the clientGUID
            NetworkMessage syncData = node.GetCachedSyncData();

            if (syncData != null && syncData.ClientGUID == disconnectedClientGUID)
            {
                // Transfer ownership to host
                node.SetLive(true);

                // Update the cached sync data's ClientGUID
                // The next network sync will automatically use the host's ClientGUID
                // because USMNode sets cachedSyncData.ClientGUID = unifiedSyncMatrix.ClientGUID

                reassignedCount++;
                DL.Log($"ReassignOwnershipToHost: Transferred ownership of '{node.gameObject.name}' (NodeID: {node.NodeID}, Type: {node.NodeType}) to host", "green");
            }
        }

        DL.Log($"ReassignOwnershipToHost: Reassigned ownership of {reassignedCount} objects to host", "magenta");
    }

    /// <summary>
    /// Removes any destroyed GameObjects from the nodeList dictionary.
    /// This is called before sending game state to joining clients to ensure
    /// we don't send sync data for objects that have been destroyed.
    /// </summary>
    private void CleanupDestroyedNodes()
    {
        var nodesToRemove = new List<string>();

        // DL.Log($"CleanupDestroyedNodes: Starting cleanup, nodeList has {nodeList.Count} entries", "cyan");

        // Find all destroyed nodes
        foreach (var node in nodeList)
        {
            // Unity's == operator returns true for destroyed objects
            if (node.Value == null)
            {
                nodesToRemove.Add(node.Key);
                DL.Log($"CleanupDestroyedNodes: Found destroyed node '{node.Key}'", "yellow");
            }
        }

        // Remove destroyed nodes from the dictionary
        foreach (string nodeID in nodesToRemove)
        {
            nodeList.Remove(nodeID);
            DL.Log($"CleanupDestroyedNodes: Removed destroyed node '{nodeID}' from nodeList", "orange");
        }

        if (nodesToRemove.Count > 0)
        {
            DL.Log($"CleanupDestroyedNodes: Cleaned up {nodesToRemove.Count} destroyed nodes. NodeList now has {nodeList.Count} entries", "green");
        }
        else
        {
            DL.Log($"CleanupDestroyedNodes: No destroyed nodes found. NodeList has {nodeList.Count} entries", "cyan");
        }
    }

    /// <summary>
    /// Handles incoming LiveSwap messages from other clients.
    /// When another client takes ownership of an object, this method finds the local live copy
    /// and transfers it to non-live (copy) status.
    /// </summary>
    /// <param name="nodeID">The NodeID of the object being swapped.</param>
    private void HandleLiveSwap(string nodeID)
    {
        if (string.IsNullOrEmpty(nodeID))
        {
            DL.Log("HandleLiveSwap: NodeID is null or empty", "yellow");
            return;
        }

        // Find the node in the nodeList
        if (!nodeList.TryGetValue(nodeID, out GameObject nodeObject))
        {
            DL.Log($"HandleLiveSwap: NodeID '{nodeID}' not found in nodeList", "yellow");
            return;
        }

        if (nodeObject == null)
        {
            DL.Log($"HandleLiveSwap: GameObject for NodeID '{nodeID}' is null", "yellow");
            return;
        }

        // Get the USMNode component
        USMNode node = nodeObject.GetComponent<USMNode>();
        if (node == null)
        {
            DL.Log($"HandleLiveSwap: GameObject '{nodeObject.name}' (NodeID: {nodeID}) has no USMNode component", "yellow");
            return;
        }

        // Only swap if this node is currently live (we're giving up ownership)
        if (!node.LiveNode)
        {
            DL.Log($"HandleLiveSwap: Node '{nodeObject.name}' (NodeID: {nodeID}) is already non-live, no swap needed", "cyan");
            return;
        }

        // Transfer ownership away - make this node non-live
        node.SetLive(false);

        DL.Log($"HandleLiveSwap: Transferred ownership of '{nodeObject.name}' (NodeID: {nodeID}) to remote client", "magenta");
    }
    #endregion

    #region Helper Functions
    /// <summary>
    /// Loads a GameObject prefab from Resources with recursive folder searching.
    /// Searches through all subfolders in the Resources directory.
    /// </summary>
    /// <param name="prefabName">The name of the prefab to load (without path or extension).</param>
    /// <returns>The loaded GameObject prefab, or null if not found.</returns>
    private GameObject LoadPrefabFromResources(string prefabName)
    {
        // First try direct load (most common case)
        GameObject prefab = Resources.Load<GameObject>(prefabName);
        if (prefab != null)
        {
            // DL.Log($"LoadPrefabFromResources: Found '{prefabName}' at root level", "cyan");
            return prefab;
        }

        // If not found, use Resources.LoadAll to search recursively
        GameObject[] allPrefabs = Resources.LoadAll<GameObject>("");

        foreach (GameObject obj in allPrefabs)
        {
            if (obj.name == prefabName)
            {
                // DL.Log($"LoadPrefabFromResources: Found '{prefabName}' in nested folder", "cyan");
                return obj;
            }
        }

        // DL.Log($"LoadPrefabFromResources: '{prefabName}' not found in any Resources folder", "yellow");
        return null;
    }

    /// <summary>
    /// Creates a serialized string from a NetworkMessage object using dictionary-based serialization.
    /// </summary>
    /// <param name="message">The message to serialize.</param>
    /// <returns>A pipe-delimited string representation of the message.</returns>
    public string CreateStringFromNetworkMessage(ref NetworkMessage message)
    {
        // Clear the cached StringBuilder for reuse
        messageBuilder.Clear();

        // Iterate through all serializers in the defined order
        foreach (var kvp in messageSerializers)
        {
            string result = kvp.Value(message);
            if (result != null)
            {
                messageBuilder.Append('|');
                messageBuilder.Append(result);
            }
        }

        // Remove leading pipe if present
        if (messageBuilder.Length > 0 && messageBuilder[0] == '|')
            messageBuilder.Remove(0, 1);

        return messageBuilder.ToString();
    }
    /// <summary>
    /// Converts a boolean to a "1" or "0" string.
    /// </summary>
    /// <param name="value">The boolean value.</param>
    /// <returns>"1" if true, "0" if false.</returns>
    public string BooleanToString(bool value)
    {
        return value ? "1" : "0";
    }
    /// <summary>
    /// Rounds the components of a Vector2 to one decimal place.
    /// </summary>
    /// <param name="vector">The vector to round.</param>
    /// <returns>The rounded vector, or null if the input was null.</returns>
    public Vector2? RoundVector2ToDecimal(Vector2? vector)
    {
        if (vector == null) return null;

        float roundedX = (float)Math.Round(vector.Value.x, 1);
        float roundedY = (float)Math.Round(vector.Value.y, 1);

        return new Vector2(roundedX, roundedY);
    }
    /// <summary>
    /// Rounds a float to one decimal place.
    /// </summary>
    /// <param name="value">The float to round.</param>
    /// <returns>The rounded float, or null if the input was null.</returns>
    public float? RoundToDecimal(float? value)
    {
        if (value == null) return null;

        float rounded = (float)Math.Round(value.Value, 1);

        return rounded;
    }
    /// <summary>
    /// Checks if an object's value is zero (if it's an integer).
    /// </summary>
    /// <param name="val">The object to check.</param>
    /// <returns>True if the object is an integer with a value of 0.</returns>
    public bool IsObjectZero(ref System.Object val)
    {
        if (val == null)
        {
            return false;
        }

        if (int.TryParse(val.ToString(), out int value))
        {
            return value == 0;
        }

        return false;

    }
    /// <summary>
    /// Converts a Vector2 to a string for network transmission.
    /// Optimized to reduce string allocations by using StringBuilder.
    /// </summary>
    /// <param name="vector2">The vector to convert.</param>
    /// <param name="propName">The network property name for this vector.</param>
    /// <returns>A formatted string for the network message.</returns>
    public string Vector2ToString(Vector2 vector2, string propName)
    {
        // Round the values
        float roundedX = (float)Math.Round(vector2.x, 1);
        float roundedY = (float)Math.Round(vector2.y, 1);

        // Handle -0 edge case
        if (roundedX == 0f) roundedX = 0f;
        if (roundedY == 0f) roundedY = 0f;

        // Use a temporary StringBuilder for this conversion
        // We don't reuse messageBuilder here to avoid conflicts with CreateStringFromNetworkMessage
        StringBuilder sb = new StringBuilder(32);
        sb.Append(propName);
        sb.Append(':');
        sb.Append(roundedX.ToString("0.0"));
        sb.Append(':');
        sb.Append(roundedY.ToString("0.0"));

        return sb.ToString();
    }
    /// <summary>
    /// Converts a float to a string for network transmission.
    /// Optimized to reduce string allocations by using StringBuilder.
    /// </summary>
    /// <param name="angularVelocity">The float to convert.</param>
    /// <param name="propName">The network property name for this float.</param>
    /// <returns>A formatted string for the network message.</returns>
    public string FloatToString(float angularVelocity, string propName)
    {
        float rounded = (float)Math.Round(angularVelocity, 2);

        StringBuilder sb = new StringBuilder(24);
        sb.Append(propName);
        sb.Append(':');
        sb.Append(rounded.ToString("0.00"));

        return sb.ToString();
    }
    /// <summary>
    /// Converts a nullable boolean to a string for network transmission.
    /// </summary>
    /// <param name="value">The boolean to convert.</param>
    /// <param name="propName">The network property name for this boolean.</param>
    /// <returns>A formatted string for the network message.</returns>
    public string BooleanToString(bool? value, string propName)
    {
        return $"{propName}:" + BooleanToString(value.Value);
    }

    #endregion


}

[Serializable]
public class Settings
{
    public string url;
    [SerializeField]
    private string urlDev;
    public bool MessageDebug;

    /// <summary>
    /// Gets the appropriate URL based on whether running in Unity Editor or production build.
    /// </summary>
    /// <returns>Development URL if in Unity Editor, otherwise production URL.</returns>
    public string GetUrl()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        return !string.IsNullOrEmpty(urlDev) ? urlDev : url;
#else
        return url;
#endif
    }
}
[Serializable]
public class StringStringEntry
{
    public string senderID;
    public string receiverID;
}

public class NetworkMessage
{
    // ID
    public string? Name { get; set; }
    public string? GUID { get; set; }
    public string? TargetGUID { get; set; }
    public string? NewClientGUID { get; set; }
    public string? ClosedGUID { get; set; }
    public string? YourNewClientGUID { get; set; }
    public string? ClientGUID { get; set; }
    public string? SessionGUID { get; set; }
    public string? RemoveGUID { get; set; }
    public string? SendingClientGUID { get; set; }
    public string? ParentGUID { get; set; }
    public List<string>? ClientGUIDList { get; set; }

    // Physical
    public Vector2? Position { get; set; }
    public Vector2? Velocity { get; set; }
    public Vector2? Scale { get; set; }
    public float? AngularVelocity { get; set; }
    public float? Rotation { get; set; }
    public double? Speed { get; set; }
    public float? Magnitude { get; set; }
    public string? Action { get; set; }
    public string? ActionValue { get; set; }
    public bool? LiveSwap { get; set; }
    public bool? Pause { get; set; }
    public int? Handshake { get; set; }
    public bool? StartGame { get; set; }
    public bool? Update { get; set; }
    public string? Query { get; set; }
    public string? QueryResponse { get; set; }

    // Extra Properties
    public int? Int1 { get; set; }
    public int? Int2 { get; set; }
    public int? Int3 { get; set; }
    public int? Int4 { get; set; }
    public int? Int5 { get; set; }

    public float? Float1 { get; set; }
    public float? Float2 { get; set; }
    public float? Float3 { get; set; }
    public float? Float4 { get; set; }
    public float? Float5 { get; set; }
    public float? Float6 { get; set; }
    public float? Float7 { get; set; }
    public float? Float8 { get; set; }
    public float? Float9 { get; set; }
    public float? Float10 { get; set; }

    public bool? Bool1 { get; set; }
    public bool? Bool2 { get; set; }
    public bool? Bool3 { get; set; }
    public bool? Bool4 { get; set; }
    public bool? Bool5 { get; set; }

    public string? String1 { get; set; }
    public string? String2 { get; set; }
    public string? String3 { get; set; }
    public string? String4 { get; set; }
    public string? String5 { get; set; }

    public Vector2? Vector2_1 { get; set; }
    public Vector2? Vector2_2 { get; set; }
    public Vector2? Vector2_3 { get; set; }
    public Vector2? Vector2_4 { get; set; }
    public Vector2? Vector2_5 { get; set; }

    public Vector3? Vector3_1 { get; set; }
    public Vector3? Vector3_2 { get; set; }
    public Vector3? Vector3_3 { get; set; }
    public Vector3? Vector3_4 { get; set; }
    public Vector3? Vector3_5 { get; set; }

    public string GameMessage { get; set; }
    public string GameValue { get; set; }

    public int? ClientIndex { get; set; }

    // Game States


    // Methods
    /// <summary>
    /// Creates a shallow copy of this NetworkMessage.
    /// This is a fast copy that only copies field references, not deep cloning objects.
    /// Since all fields are value types or immutable strings, this is safe and efficient.
    /// </summary>
    /// <returns>A new NetworkMessage instance with copied values.</returns>
    public NetworkMessage ShallowCopy()
    {
        return new NetworkMessage
        {
            // ID
            Name = this.Name,
            GUID = this.GUID,
            TargetGUID = this.TargetGUID,
            NewClientGUID = this.NewClientGUID,
            ClosedGUID = this.ClosedGUID,
            YourNewClientGUID = this.YourNewClientGUID,
            ClientGUID = this.ClientGUID,
            SessionGUID = this.SessionGUID,
            RemoveGUID = this.RemoveGUID,
            SendingClientGUID = this.SendingClientGUID,
            ParentGUID = this.ParentGUID,
            ClientGUIDList = this.ClientGUIDList != null ? new List<string>(this.ClientGUIDList) : null,

            // Physical
            Position = this.Position,
            Velocity = this.Velocity,
            Scale = this.Scale,
            AngularVelocity = this.AngularVelocity,
            Rotation = this.Rotation,
            Speed = this.Speed,
            Magnitude = this.Magnitude,
            Action = this.Action,
            ActionValue = this.ActionValue,
            LiveSwap = this.LiveSwap,
            Pause = this.Pause,
            Handshake = this.Handshake,
            StartGame = this.StartGame,
            Update = this.Update,
            Query = this.Query,
            QueryResponse = this.QueryResponse,

            // Extra Properties
            Int1 = this.Int1,
            Int2 = this.Int2,
            Int3 = this.Int3,
            Int4 = this.Int4,
            Int5 = this.Int5,

            Float1 = this.Float1,
            Float2 = this.Float2,
            Float3 = this.Float3,
            Float4 = this.Float4,
            Float5 = this.Float5,
            Float6 = this.Float6,
            Float7 = this.Float7,
            Float8 = this.Float8,
            Float9 = this.Float9,
            Float10 = this.Float10,

            Bool1 = this.Bool1,
            Bool2 = this.Bool2,
            Bool3 = this.Bool3,
            Bool4 = this.Bool4,
            Bool5 = this.Bool5,

            String1 = this.String1,
            String2 = this.String2,
            String3 = this.String3,
            String4 = this.String4,
            String5 = this.String5,

            Vector2_1 = this.Vector2_1,
            Vector2_2 = this.Vector2_2,
            Vector2_3 = this.Vector2_3,
            Vector2_4 = this.Vector2_4,
            Vector2_5 = this.Vector2_5,

            GameMessage = this.GameMessage,
            GameValue = this.GameValue,

            ClientIndex = this.ClientIndex
        };
    }

    /// <summary>
    /// Resets all properties to their default (null) values.
    /// Preserves GUID and ClientGUID by default since they are always reassigned after clearing.
    /// </summary>
    public void ClearValues()
    {
        // Preserve GUID and ClientGUID - these are typically set once and reused
        string preservedGUID = GUID;
        string preservedClientGUID = ClientGUID;

        // ID
        Name = null;
        GUID = null;
        TargetGUID = null;
        NewClientGUID = null;
        ClosedGUID = null;
        YourNewClientGUID = null;
        ClientGUID = null;
        SessionGUID = null;
        RemoveGUID = null;
        SendingClientGUID = null;
        ParentGUID = null;


        // Physical
        Position = null;
        Velocity = null;
        Scale = null;
        AngularVelocity = null;
        Rotation = null;
        Speed = null;
        Magnitude = null;
        Action = null;
        ActionValue = null;

        // Booleans
        LiveSwap = null;
        Pause = null;
        Handshake = null;
        StartGame = null;
        Update = null;
        Query = null;
        QueryResponse = null;

        // Ints
        Int1 = null;
        Int2 = null;
        Int3 = null;
        Int4 = null;
        Int5 = null;

        // Floats
        Float1 = null;
        Float2 = null;
        Float3 = null;
        Float4 = null;
        Float5 = null;
        Float6 = null;
        Float7 = null;
        Float8 = null;
        Float9 = null;
        Float10 = null;

        // Booleans
        Bool1 = null;
        Bool2 = null;
        Bool3 = null;
        Bool4 = null;
        Bool5 = null;

        // Strings
        String1 = null;
        String2 = null;
        String3 = null;
        String4 = null;
        String5 = null;

        // Vector2
        Vector2_1 = null;
        Vector2_2 = null;
        Vector2_3 = null;
        Vector2_4 = null;
        Vector2_5 = null;

        GameMessage = null;
        GameValue = null;

        ClientIndex = null;

        // Restore preserved values
        GUID = preservedGUID;
        ClientGUID = preservedClientGUID;
    }

}
