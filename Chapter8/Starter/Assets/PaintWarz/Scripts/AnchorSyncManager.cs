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
        // TODO
    }

    /// <summary>
    /// Callback when the network is ready (connection established if client) 
    /// </summary>
    /// <param name="networkManager"></param>
    private void NetworkManager_OnNetworkReady(PWNetworkManager networkManager)
    {
        // TODO
    }

    void InitAnchor()
    {
        // TODO
    }

    #region Server methods 

    void OnRecievedRequestForAnchor(NetworkMessage netMsg)
    {
        StartCoroutine(SendAnchorToClient(netMsg.conn));
    }

    IEnumerator SendAnchorToClient(NetworkConnection conn)
    {
        // TODO 

        yield return null;
    }

    #endregion 

    #region Client methods 

    /// <summary>
    /// Asks the server for the WorldAnchor 
    /// </summary>
    void RequestAnchorFromServer()
    {
        // TODO 
    }

    /// <summary>
    /// Called when we receive an network message containing an anchor 
    /// </summary>
    /// <param name="netMsg"></param>
    void OnRecievedAnchorPacket(NetworkMessage netMsg)
    {                 
        // TODO 
    }

    /// <summary>
    /// Check if we have received a complete anchor 
    /// </summary>
    void CheckForCompletedAnchor()
    {        
        // TODO 
    }

    #endregion 

    #region Importing the Anchor 

    bool SetAnchor(byte[] data)
    {
        State = States.Importing;

        return true; 
    }

    private void OnImportComplete(SerializationCompletionReason completionReason, WorldAnchorTransferBatch deserializedTransferBatch)
    {
        // TODO 
    }

    #endregion

    #region Exporting the Anchor 

    void CreateAnchor()
    {
        State = States.Exporting;

        // TODO 
         
    }

    IEnumerator ExportFloorAnchor(WorldAnchor worldAnchor)
    {
        // TODO 

        yield return null;
    }

    private void OnExportDataAvailable(byte[] data)
    {
        worldAnchorBuffer.AddRange(data);
    }

    private void OnExportComplete(SerializationCompletionReason completionReason)
    {
       // TODO 
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
