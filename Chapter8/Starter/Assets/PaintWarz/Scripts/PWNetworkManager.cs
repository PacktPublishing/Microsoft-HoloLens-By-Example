using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Extension of the NetworkManager to add some configuration 
/// changes and make status more vebose by broadcasting them to 
/// interested parites. 
/// </summary>
[RequireComponent(typeof(PWNetworkDiscovery))]
public class PWNetworkManager : NetworkManager {    

    #region Delegate and Events 

    public delegate void NetworkReady(PWNetworkManager networkManager);
    public event NetworkReady OnNetworkReady = delegate { };

    public delegate void ConnectionOpen(PWNetworkManager networkManager, NetworkConnection connection);
    public event ConnectionOpen OnConnectionOpen = delegate { };

    #endregion

    private bool _isReady = false; 

    public bool IsReady
    {
        get
        {
            return _isReady; 
        }
        private set
        {
            _isReady = value;

            OnNetworkReady(this);
        }
    }

    private bool _isServer = true; 

    public bool IsServer
    {
        get { return _isServer; }
        private set
        {
            _isServer = value;
        }
    }    

    private void Start()
    {
        globalConfig.MaxPacketSize = 4000; 

        customConfig = true; 
        connectionConfig.MaxSentMessageQueueSize = 512;
        connectionConfig.MaxCombinedReliableMessageCount = 30;
        connectionConfig.MaxCombinedReliableMessageSize = 500;
        connectionConfig.PacketSize = 1500;
        connectionConfig.FragmentSize = 1000;

        GetComponent<PWNetworkDiscovery>().StartScanning();
    }
    

    #region client callbacks 

    /// <summary>
    /// Called on the client when connected to a server.
    /// </summary>
    /// <param name="conn"></param>
    public override void OnClientConnect(NetworkConnection conn)
    {
        IsServer = false;        

        base.OnClientConnect(conn);

        IsReady = true;

        OnConnectionOpen(this, conn); 
    }

    #endregion

    #region server callbacks 

    public override void OnStartServer()
    {
        IsReady = true;
    }

    public override void OnServerConnect(NetworkConnection conn)
    {
        base.OnServerConnect(conn);

        OnConnectionOpen(this, conn);
    }

    #endregion 
}
