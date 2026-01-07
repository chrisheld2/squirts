using System;
using System.Threading.Tasks;

public class NETCoreWrapper
{
    #region Private Variables
    private bool isInitialized = false;
    private PLATFORMS _platForm;
    private NETCoreDesktop netCoreDesktop;
    private NETCoreWebBrowser netCoreWebBrowser;
    #endregion

    #region Enums
    public enum PLATFORMS
    {
        DESKTOP,
        WEB,
        STEAM
    }
    #endregion

    #region Delegates and Events
    public delegate void Delegate_MessageReceived(string data);

    public event Delegate_MessageReceived OnMessageReceived;
    #endregion

    #region Public Properties
    public PLATFORMS platForm => _platForm;
    #endregion

    #region Public Methods

    public void Init(string serverURL, PLATFORMS platform)
    {
        if (isInitialized)
        {
            throw new Exception("NETCoreWrapper already initialized.");
        }

        isInitialized = true;

        try
        {
            _platForm = platform;

            switch (platform)
            {
                case PLATFORMS.DESKTOP:
                    netCoreDesktop = new NETCoreDesktop();

                    netCoreDesktop.OnMessageReceived += HandleMessageReceived;
                    netCoreDesktop.Init(serverURL);
                    break;

                case PLATFORMS.WEB:
                    netCoreWebBrowser = new NETCoreWebBrowser();

                    netCoreWebBrowser.OnMessageReceived += HandleMessageReceived;
                    netCoreWebBrowser.Init(serverURL);
                    break;

                case PLATFORMS.STEAM:
                    throw new Exception("Steam not implemented yet.");
            }
        }
        catch (Exception ex)
        {
            throw new Exception("Init Error:" + ex.Message);
        }
    }

    public async Task Connect(string sessionGUID)
    {
        try
        {
            switch (_platForm)
            {
                case PLATFORMS.DESKTOP:
                    if (netCoreDesktop == null) return;
                    await netCoreDesktop.WebSocketInit(sessionGUID);
                    break;

                case PLATFORMS.WEB:
                    if (netCoreWebBrowser == null) return;
                    await netCoreWebBrowser.WebSocketInit(sessionGUID);
                    break;

                case PLATFORMS.STEAM:
                    throw new Exception("Steam not implemented yet.");
            }
        }
        catch (Exception ex)
        {
            throw new Exception("Connect Error:" + ex.Message);
        }
    }

    public async Task SendToAll(string data)
    {
        switch (_platForm)
        {
            case PLATFORMS.DESKTOP:
                if (netCoreDesktop != null)
                    await netCoreDesktop.SendToAll(data);
                break;

            case PLATFORMS.WEB:
                if (netCoreWebBrowser != null)
                    await netCoreWebBrowser.SendToAll(data);
                break;

            case PLATFORMS.STEAM:
                throw new NotImplementedException("Steam platform not implemented yet.");
            default:
                throw new ArgumentOutOfRangeException(nameof(_platForm), _platForm, "Invalid platform");
        }
    }

    public async Task ShutDown()
    {
        switch (_platForm)
        {
            case PLATFORMS.DESKTOP:
                if (netCoreDesktop == null) return;

                netCoreDesktop.OnMessageReceived -= HandleMessageReceived;
                await netCoreDesktop.ShutDown();

                break;

            case PLATFORMS.WEB:
                if (netCoreWebBrowser == null) return;

                netCoreWebBrowser.OnMessageReceived -= HandleMessageReceived;
                await netCoreWebBrowser.ShutDown();

                break;

            case PLATFORMS.STEAM:
                throw new NotImplementedException("Steam platform not implemented yet.");
            default:
                throw new ArgumentOutOfRangeException(nameof(_platForm), _platForm, "Invalid platform");
        }

        isInitialized = false;
    }

    #endregion

    #region Private Methods

    private void HandleMessageReceived(string data)
    {
        OnMessageReceived?.Invoke(data);
    }

    #endregion
}
