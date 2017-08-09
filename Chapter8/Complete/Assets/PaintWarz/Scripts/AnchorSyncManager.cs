using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.VR.WSA;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Networking.NetworkSystem;
using HoloToolkit.Unity;
using UnityEngine.VR.WSA.Sharing;

/// <summary>
/// Responsible for Exporting, Importing the WorldAnchors along with transmitting and 
/// receiving through across the network i.e. sharing.
/// </summary>
public class AnchorSyncManager : Singleton<AnchorSyncManager>
{
    public const short MsgRequestAnchor = 1103;

    public const short MsgAnchor = 1104;

    public delegate void StateChanged(States state);

    public event StateChanged OnStateChanged = delegate { }; 

    public enum States
    {
        Undefined,
        Exporting,                 
        RequestingAnchor,
        Downloading,
        Importing,
        Anchored
    }

    private PWNetworkManager networkManager;

    private PlaySpaceManager playSpaceManager;

    /// <summary>
    /// WorldAnchor serailised as bytes (used for exporting and receiving) 
    /// </summary>
    private List<byte> worldAnchorBuffer = new List<byte>();

    /// <summary>
    /// Collection to store all received packets which is then used to 
    /// re-build the WorldAnchor 
    /// </summary>
    private List<AnchorDataMessage> receivedAnchorDataMessages = new List<AnchorDataMessage>();

    /// <summary>
    /// Set after receiving the last packet from the server, once we have 
    /// equal number of packets, we attempt to re-build and import the 
    /// WorldAnchor 
    /// </summary>
    private int numberOfPacketsExcepting = -1;  

    private States _state = States.Undefined; 

    public States State
    {
        get { return _state; }
        set
        {
            if(_state == value)
            {
                return; 
            }

            _state = value;

            OnStateChanged(_state); 
        }
    }

    void Start()
    {
        networkManager = FindObjectOfType<PWNetworkManager>();

        playSpaceManager = PlaySpaceManager.Instance;

        playSpaceManager.OnPlaySpaceFinished += PlaySpaceManager_OnPlaySpaceFinished;
        networkManager.OnNetworkReady += NetworkManager_OnNetworkReady;
    }

    /// <summary>
    /// Callback when scanning has finished 
    /// </summary>
    private void PlaySpaceManager_OnPlaySpaceFinished(GameObject floorPlane)
    {
        InitAnchor();
    }

    /// <summary>
    /// Callback when the network is ready (connection established if client) 
    /// </summary>
    /// <param name="networkManager"></param>
    private void NetworkManager_OnNetworkReady(PWNetworkManager networkManager)
    {
        if(networkManager.IsServer)
        {
            // listen out for requests for the anchor from any connected client 
            NetworkServer.RegisterHandler(MsgRequestAnchor, OnRecievedRequestForAnchor);
        }
        else
        {
            // listen out for anchor packets from the server 
            networkManager.client.RegisterHandler(MsgAnchor, OnRecievedAnchorPacket);
        }

        InitAnchor();
    }

    void InitAnchor()
    {
        if(State != States.Undefined)
        {
            return; 
        }

        if (networkManager.IsReady && networkManager.IsServer && PlaySpaceManager.Instance.Finished)
        {
            CreateAnchor();
        }
        else if (networkManager.IsReady && !networkManager.IsServer && PlaySpaceManager.Instance.Finished)
        {
            RequestAnchorFromServer();
        }
    }

    #region Server methods 

    void OnRecievedRequestForAnchor(NetworkMessage netMsg)
    {
        StartCoroutine(SendAnchorToClient(netMsg.conn));
    }

    IEnumerator SendAnchorToClient(NetworkConnection conn)
    {
        const int maxPayloadSize = 2048;

        AnchorDataMessage netMsg = null;

        int messageNumber = 0;

        int si = 0;
        int messagesSent = 0;

        var data = new List<byte>(Compress(worldAnchorBuffer.ToArray()));

        while (si < data.Count)
        {
            int count = Mathf.Min(maxPayloadSize, (data.Count - si));

            byte[] payloadData = data.GetRange(si, count).ToArray();

            netMsg = new AnchorDataMessage();
            netMsg.packetNumber = messageNumber;
            netMsg.data = payloadData;

            conn.SendByChannel(MsgAnchor, netMsg, 2);

            messagesSent += 1;
            messageNumber += 1;
            si += payloadData.Length;

            // throttle task 
            if (messagesSent % 20 == 0)
            {
                yield return new WaitForSeconds(0.3f);
            }
            else
            {
                yield return null;
            }
        }

        // send empty packet 
        netMsg = new AnchorDataMessage();
        netMsg.packetNumber = messageNumber;
        netMsg.isEnd = true;
        netMsg.data = new byte[] { 0 };
        conn.SendByChannel(MsgAnchor, netMsg, 2);

        Debug.LogFormat("Sending connection {0} AnchorPackets, messagesSent {1}", 
            messageNumber, messagesSent); 
    }

    #endregion 

    #region Client methods 

    /// <summary>
    /// Asks the server for the WorldAnchor 
    /// </summary>
    void RequestAnchorFromServer()
    {
        receivedAnchorDataMessages.Clear(); 

        State = States.RequestingAnchor;

        networkManager.client.Send(MsgRequestAnchor, new IntegerMessage(MsgRequestAnchor));
    }

    /// <summary>
    /// Called when we receive an network message containing an anchor 
    /// </summary>
    /// <param name="netMsg"></param>
    void OnRecievedAnchorPacket(NetworkMessage netMsg)
    {                 
        State = States.Downloading;

        var anchorPacket = netMsg.ReadMessage<AnchorDataMessage>();   

        if (anchorPacket.isEnd)
        {
            numberOfPacketsExcepting = anchorPacket.packetNumber;
        }
        else
        {
            receivedAnchorDataMessages.Add(anchorPacket);
        }

        CheckForCompletedAnchor();
    }

    /// <summary>
    /// Check if we have received a complete anchor 
    /// </summary>
    void CheckForCompletedAnchor()
    {        
        // exit if we do not know how many packets we 
        // are expecting 
        if (numberOfPacketsExcepting == -1)
        {
            return;
        }

        if (receivedAnchorDataMessages.Count == numberOfPacketsExcepting)
        {
            // process 
            var data = receivedAnchorDataMessages
                .OrderBy((netMsg) => { return netMsg.packetNumber; })
                .Select((netMsg) => { return netMsg.data; })
                .SelectMany(netMsg => netMsg)
                .ToArray();

            // decompress 
            data = Decompress(data); 

            SetAnchor(data);
        }
    }

    #endregion 

    #region Importing the Anchor 

    bool SetAnchor(byte[] data)
    {
        State = States.Importing;

#if WINDOWS_UWP

        worldAnchorBuffer.Clear();
        worldAnchorBuffer.AddRange(data);

        WorldAnchorTransferBatch.ImportAsync(data, OnImportComplete);
#else 
        State = States.Anchored;
#endif

        return true; 
    }

    private void OnImportComplete(SerializationCompletionReason completionReason, WorldAnchorTransferBatch deserializedTransferBatch)
    {
        if (completionReason == SerializationCompletionReason.Succeeded)
        {
            if (PlaySpaceManager.Instance.Floor.GetComponent<WorldAnchor>())
            {
                Destroy(PlaySpaceManager.Instance.Floor.GetComponent<WorldAnchor>());
            }

            deserializedTransferBatch.LockObject(PlaySpaceManager.Instance.Floor.name, PlaySpaceManager.Instance.Floor);

            State = States.Anchored; 
        }
        else
        {
            Debug.LogErrorFormat("Failed to import Anchor; {0}", completionReason.ToString()); 
        }        
    }

    #endregion

    #region Exporting the Anchor 

    void CreateAnchor()
    {
        State = States.Exporting;

#if WINDOWS_UWP

        if (PlaySpaceManager.Instance.Floor.GetComponent<WorldAnchor>())
        {
            Destroy(PlaySpaceManager.Instance.Floor.GetComponent<WorldAnchor>());
        }

        var worldAnchor = PlaySpaceManager.Instance.Floor.AddComponent<WorldAnchor>();

        StartCoroutine(ExportFloorAnchor(worldAnchor));

#else        
        // The Editor has no WorldAnchor, so ignore and just proceed to the next state 
        State = States.Anchored; 
#endif
         
    }

    IEnumerator ExportFloorAnchor(WorldAnchor worldAnchor)
    {
        while (!worldAnchor.isLocated)
        {
            yield return new WaitForSeconds(0.5f);
        }

        WorldAnchorTransferBatch transferBatch = new WorldAnchorTransferBatch();
        transferBatch.AddWorldAnchor(gameObject.name, worldAnchor);
        WorldAnchorTransferBatch.ExportAsync(transferBatch, OnExportDataAvailable, OnExportComplete);
    }

    private void OnExportDataAvailable(byte[] data)
    {
        worldAnchorBuffer.AddRange(data);
    }

    private void OnExportComplete(SerializationCompletionReason completionReason)
    {
        if (completionReason == SerializationCompletionReason.Succeeded)
        {
            State = States.Anchored;
        }
        else
        {
            Debug.LogErrorFormat("Failed to export Anchor; {0}", completionReason.ToString()); 
        }
    }

    #endregion

    #region utils 

    public static byte[] Compress(byte[] data)
    {
        byte[] compressedData = null; 

        using (MemoryStream output = new MemoryStream())
        {
            using (DeflateStream dstream = new DeflateStream(output, CompressionMode.Compress))
            {
                dstream.Write(data, 0, data.Length);
            }
            compressedData = output.ToArray();
        }

        return compressedData; 
    }

    public static byte[] Decompress(byte[] data)
    {
        byte[] uncompressedData = null;

        using(MemoryStream output = new MemoryStream())
        {
            using (MemoryStream input = new MemoryStream(data))
            {
                using (DeflateStream dstream = new DeflateStream(input, CompressionMode.Decompress))
                {
                    dstream.CopyTo(output);
                }
            }

            output.Position = 0;
            uncompressedData = output.ToArray(); 
        }        
                    

        return uncompressedData; 
    }    

    #endregion 
}
