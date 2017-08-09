using System;
using UnityEngine;
using UnityEngine.Networking;
using HoloToolkit.Unity;

/// <summary>
/// App coordinater, responsible for orchestrating the game. 
/// </summary>
public class AppManager : Singleton<AppManager> {

    public const short MsgTeamSpawnRequest = 5104;

    [Tooltip("Server player")]
    public GameObject playerSPrefab;

    [Tooltip("Client player")]
    public GameObject playerCPrefab;

    [Tooltip("Visual indiactor used to show where the user tapped")]
    public TapIndicator tapIndicator; 

    public string Team { private set; get; }

    private PWNetworkDiscovery networkDiscovery;

    private PWNetworkManager networkManager;

    private bool teamCreated = false; 

    bool IsReady
    {
        get
        {
            return (AnchorSyncManager.Instance.State == AnchorSyncManager.States.Anchored && networkManager.IsReady);
        }
    }

    void Start () {
        networkDiscovery = FindObjectOfType<PWNetworkDiscovery>();
        networkManager = FindObjectOfType<PWNetworkManager>();

        networkManager.OnNetworkReady += NetworkManager_OnNetworkReady;
        networkManager.OnConnectionOpen += NetworkManager_OnConnectionOpen;


        PlaySpaceManager.Instance.OnPlaySpaceFinished += PlaySpaceManager_OnPlaySpaceFinished;
        AnchorSyncManager.Instance.OnStateChanged += AnchorSyncManager_OnStateChanged;

        InputManager.Instance.OnTap += InputManager_OnTap;
    }

    void NetworkManager_OnNetworkReady(PWNetworkManager networkManager)
    {
        // TODO         
    }

    private void NetworkManager_OnConnectionOpen(PWNetworkManager networkManager, NetworkConnection connection)
    {
        if (IsReady)
        {
            CursorIndicator.Instance.IsChecking = true; 
        }
    }

    private void InputManager_OnTap(GameObject target, Vector3 hitPosition, Vector3 hitNormal)
    {
        // TODO          
    }

    private void PlaySpaceManager_OnPlaySpaceFinished(GameObject floorPlane)
    {
        // TODO 
    }

    private void AnchorSyncManager_OnStateChanged(AnchorSyncManager.States state)
    {
        if (IsReady)
        {
            CursorIndicator.Instance.IsChecking = true;
        }
    }

    void OnRecievedRequestToSpawnTeam(NetworkMessage netMsg)
    {
        // TODO 
    }

    #region Team Creation Methods 

    void InitTeam(Vector3 startingPosition)
    {
        CursorIndicator.Instance.IsChecking = false;
        teamCreated = true; 

        if (networkManager.IsServer)
        {
            Team = "TeamS";
            CreateTeam(null, startingPosition);
        }
        else
        {
            Team = "TeamC";
            networkManager.client.Send(MsgTeamSpawnRequest, new TeamSpawnRequestMessage(startingPosition));
        }
    }

    public void CreateTeam(NetworkConnection connection, Vector3 teamCenterPosition)
    {        
        // TODO 
    }

    #endregion 

    private void OnDestroy()
    {
        try
        {
            networkDiscovery.StopBroadcast();
        }
        catch (Exception) { }

        if (networkManager.IsServer)
        {
            networkManager.StopServer();
        }
        else
        {
            networkManager.StopClient();
        }

        networkManager.OnNetworkReady -= NetworkManager_OnNetworkReady;
        networkManager.OnConnectionOpen -= NetworkManager_OnConnectionOpen;

        AnchorSyncManager.Instance.OnStateChanged -= AnchorSyncManager_OnStateChanged;
        
        InputManager.Instance.OnTap -= InputManager_OnTap;
    }
}
