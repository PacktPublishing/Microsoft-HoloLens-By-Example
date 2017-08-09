using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Used to search for an existing service/game 
/// </summary>
[RequireComponent(typeof(PWNetworkManager))]
public class PWNetworkDiscovery : NetworkDiscovery {

    [Tooltip("How long before checking if we have discovered a peer, if not then we assume we are the server")]
    public float connectionCheckTime = 3.0f;

    /// <summary>
    /// Discovered network address 
    /// </summary>
    private string _networkAddress = null; 

    public string NetworkAddress
    {
        get
        {
            return _networkAddress;
        }
        set
        {
            _networkAddress = value;

            // TODO 
        }
    }

    public bool DiscoveredPeer
    {
        get
        {
            return _networkAddress != null; 
        }
    }

    private PWNetworkManager networkManager; 

	void Start () {
        networkManager = GetComponent<PWNetworkManager>();                         
    }

    public void StartScanning()
    {
        if (!Initialize())
        {
            Debug.LogWarning("Failed to initilize NetworkDiscovery"); 
        }

        StartAsClient();

        Invoke("CheckConnection", connectionCheckTime);
    }

    /// <summary>
    /// Called after a delay, if no peer is found within this 
    /// timeframe then we'll create a new session i.e. this instance 
    /// acts as the server. 
    /// </summary>
    void CheckConnection()
    {        
        // TODO        
    }

    public override void OnReceivedBroadcast(string fromAddress, string data)
    {
        if(NetworkAddress != null)
        {
            return; 
        }

        Debug.LogFormat("OnReceivedBroadcast; fromAddress: {0}, data: {1}", fromAddress, data);

        // TODO 
    }
}
