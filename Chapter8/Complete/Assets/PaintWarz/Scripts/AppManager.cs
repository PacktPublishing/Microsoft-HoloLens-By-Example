using System;
using UnityEngine;
using UnityEngine.Networking;
using HoloToolkit.Unity;

/// <summary>
/// App coordinater, responsible for orchestrating the game. 
/// </summary>
public class AppManager : Singleton<AppManager> {

    public enum NetworkingMode
    {
        None, 
        LocalDiscovery,
        Server,
        Client
    }

    public const short MsgTeamSpawnRequest = 5104;

    [Tooltip("Connecting discovery and role mode - LocalDiscovery is first in first to serve")]
    public NetworkingMode mode = NetworkingMode.LocalDiscovery;

    [Tooltip("Server player")]
    public GameObject playerSPrefab;

    [Tooltip("Client player")]
    public GameObject playerCPrefab;

    [Tooltip("Visual indiactor used to show where the user tapped")]
    public TapIndicator tapIndicator; 

    /// <summary>
    /// Name of Team, either TeamS (Server) or TeamC (Client) 
    /// </summary>
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

    #region NetworkManager Event Handlers 

    void NetworkManager_OnNetworkReady(PWNetworkManager networkManager)
    {
        if (networkManager.IsServer)
        {
            NetworkServer.RegisterHandler(MsgTeamSpawnRequest, OnRecievedRequestToSpawnTeam);
        }
    }

    private void NetworkManager_OnConnectionOpen(PWNetworkManager networkManager, NetworkConnection connection)
    {
        if (IsReady)
        {
            CursorIndicator.Instance.IsChecking = true; 
        }
    }

    #endregion 

    private void InputManager_OnTap(GameObject target, Vector3 hitPosition, Vector3 hitNormal)
    {
        if (!teamCreated && PlaySpaceManager.Instance.IsCloseToFloor(hitPosition))
        {
            InitTeam(hitPosition);
        }
        else
        {
            if (!string.IsNullOrEmpty(Team) && PlaySpaceManager.Instance.IsCloseToFloor(hitPosition))
            {
                tapIndicator.Show(hitPosition, hitNormal);
                TeamsManager.Instance.SetTeamsTarget(Team, hitPosition);
            }
        }                
    }

    #region PlaySpaceManager Event Handler 

    private void PlaySpaceManager_OnPlaySpaceFinished(GameObject floorPlane)
    {
        var players = FindObjectsOfType<Player>(); 
        foreach(var player in players)
        {
            player.transform.parent = floorPlane == null ? null : floorPlane.transform; 
        }
    }

    #endregion

    #region AnchorSyncManager Event Handler 

    private void AnchorSyncManager_OnStateChanged(AnchorSyncManager.States state)
    {
        if (IsReady)
        {
            CursorIndicator.Instance.IsChecking = true;
        }
    }

    #endregion     

    void OnRecievedRequestToSpawnTeam(NetworkMessage netMsg)
    {
        var teamSpawnRequestMessage = netMsg.ReadMessage<TeamSpawnRequestMessage>();

        CreateTeam(netMsg.conn, teamSpawnRequestMessage.position);
    }

    #region Team Creation Methods 

    void InitTeam(Vector3 startingPosition)
    {
        CursorIndicator.Instance.IsChecking = false;
        teamCreated = true; 

        if (mode == NetworkingMode.None || networkManager.IsServer)
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
        const int teamSize = 3;

        string team = connection == null ? "TeamS" : "TeamC";  

        for (int i = 0; i < teamSize; i++)
        {
            // create a new instance of the player prefab   
            GameObject player = null;

            if(connection == null)
            {
                player = Instantiate(playerSPrefab);
            }
            else
            {
                player = Instantiate(playerCPrefab);
            }
            // set team, name, and texture 
            player.GetComponent<Player>().Init(team, string.Format("Player_{0}_{1}", team, i));

            // set position 
            var playerOffset = UnityEngine.Random.insideUnitSphere * 0.5f;
            playerOffset.y = 0;
            player.transform.position = teamCenterPosition + playerOffset;

            // Spawn the bullet on the Clients
            if (connection == null)
            {
                if (mode != NetworkingMode.None)
                { 
                    player.GetComponent<NetworkIdentity>().localPlayerAuthority = false;
                    NetworkServer.Spawn(player);
                }
            }
            else
            {
                player.GetComponent<NetworkIdentity>().localPlayerAuthority = true;
                if (mode != NetworkingMode.None)
                {
                    NetworkServer.SpawnWithClientAuthority(player, connection);
                }
            }              
        }
    }

    #endregion 

    private void OnDestroy()
    {
        if(mode != NetworkingMode.None)
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
        }        

        networkManager.OnNetworkReady -= NetworkManager_OnNetworkReady;
        networkManager.OnConnectionOpen -= NetworkManager_OnConnectionOpen;

        AnchorSyncManager.Instance.OnStateChanged -= AnchorSyncManager_OnStateChanged;
        
        InputManager.Instance.OnTap -= InputManager_OnTap;
    }
}
