using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using HoloToolkit.Unity;

[RequireComponent(typeof(BlenderServiceDiscovery), typeof(BlenderService))]
public class BlenderServiceManager : Singleton<BlenderServiceManager> {

    #region delegates and events 

    public delegate void BlenderServiceStateChanged(ServiceStates state);
    public event BlenderServiceStateChanged OnBlenderServiceStateChanged = delegate { };

    public delegate void BlenderGameObjectCreated(BaseBlenderGameObject bgo);
    public event BlenderGameObjectCreated OnBlenderGameObjectCreated = delegate { };

    public delegate void BlenderGameObjectUpdated(BaseBlenderGameObject bgo);
    public event BlenderGameObjectUpdated OnBlenderGameObjectUpdated = delegate { };

    public delegate void BlenderGameObjectDestoryed(string name);
    public event BlenderGameObjectDestoryed OnBlenderGameObjectDestoryed = delegate { }; 

    #endregion

    #region types 

    public enum ServiceStates
    {
        Stopped, 
        DiscoveringService, 
        Discovered, 
        Connecting, 
        Connected, 
        Disconnected, 
        Failed 
    }

    public enum ObjectOperations : byte 
    {
        Rotate = 1, 
        Translate = 2, 
        Scale = 3, 
        SetRotation = 4, 
        SetPosition = 5, 
        SetScale = 6
    }

    #endregion

    #region properties and variables 

    public const byte TYPE_OBJECT_UPDATE = 1;
    public const byte TYPE_MATERIAL_TEXTURE_REQUEST = 2;
    public const byte TYPE_MATERIAL_TEXTURE = 3;
    public const byte TYPE_OBJECT_REMOVED = 4;
    public const byte TYPE_OBJECT_OPERATION = 5;
    public const byte TYPE_OBJECT_WORLDANCHOR = 6;
    public const byte TYPE_OBJECT_WORLDANCHOR_REQUEST = 7;

    public const string MODE_OBJECT = "OBJECT";
    public const string MODE_EDIT = "EDIT";

    public const string TAG_REFRESH = "REFRESH"; 

    public GameObject BlenderObjectsContainer;

    [Tooltip("Automatically start listening out for the BlenderLIVE service on Start")]
    public bool autoStartDiscovery = true; 

    Dictionary<string, BaseBlenderGameObject> blenderGameObjects = new Dictionary<string, BaseBlenderGameObject>();    

    Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();

    List<BlenderObject> blenderObjectsToBeProcessed = new List<BlenderObject>(); 

    bool readyToConnect = false;

    bool serviceStateChanged = false;
    ServiceStates serviceState = ServiceStates.Stopped;

    public ServiceStates ServiceState
    {
        get { return serviceState;  }
        private set
        {
            serviceState = value;
            serviceStateChanged = true; 
        }
    }

    BlenderService blenderService; 

    #endregion 

    void Start () {
        blenderService = BlenderService.Instance;

        blenderService.OnDataReceived += BlenderService_OnDataReceived;
        blenderService.OnConnectionStateChanged += BlenderService_OnConnectionStateChanged;

        BlenderServiceDiscovery.Instance.OnServiceDiscovered += BlenderServiceDiscovery_OnServiceDiscovered;

        if (autoStartDiscovery)
        {
            SearchAndConnectToService(); 
        }        
    }

    public void SearchAndConnectToService()
    {
        if(ServiceState != ServiceStates.Stopped && ServiceState != ServiceStates.Failed)
        {
            return; 
        }

        ServiceState = ServiceStates.DiscoveringService;

        BlenderServiceDiscovery.Instance.StartListening();
    }

    private void BlenderService_OnConnectionStateChanged(BlenderService.ConnectionState state)
    {
        switch (state)
        {
            case BlenderService.ConnectionState.Connecting:
                ServiceState = ServiceStates.Connecting;
                break;

            case BlenderService.ConnectionState.Connected:
                ServiceState = ServiceStates.Connected;
                break;

            case BlenderService.ConnectionState.Disconnected:
                ServiceState = ServiceStates.Disconnected;
                break;

            case BlenderService.ConnectionState.Failed:
                ServiceState = ServiceStates.Failed;
                break; 
        }
    }

    private void OnDestroy()
    {
        if(blenderService != null)
        {
            blenderService.OnDataReceived -= BlenderService_OnDataReceived;
            blenderService.OnConnectionStateChanged -= BlenderService_OnConnectionStateChanged;
        }        
    }

    void Update () {
        if (serviceStateChanged)
        {
            serviceStateChanged = false;
            OnBlenderServiceStateChanged(ServiceState);
        }

        if (readyToConnect)
        {
            readyToConnect = false;
            BlenderService.Instance.Connect();
        }        
	}

    public List<string> GetAllBlenderGameObjectNames()
    {
        return blenderGameObjects.Keys.Select((key) => { return key; }).ToList();
    }

    public BaseBlenderGameObject GetBlenderGameObjectWithName(string name)
    {
        return blenderGameObjects[name];
    }

    public bool HasTexture(string name)
    {
        return textures.ContainsKey(name); 
    }

    public Texture2D GetTexture(string name)
    {
        return textures[name]; 
    }

    public Texture2D PutTexture(string name, Texture2D texture)
    {
        return textures[name] = texture;
    }

    void ProcessData(byte[] data)
    {
        using (MemoryStream ms = new MemoryStream(data))
        {
            using (BinaryReader br = new BinaryReader(ms))
            {
                byte packetType = br.ReadByte();
                int packetSize = br.ReadInt32();

                if (packetType == TYPE_OBJECT_UPDATE)
                {
                    Debug.Log("TYPE_OBJECT_UPDATE");
                     
                    BlenderObject bo = null;

                    // metadata 
                    string tag = br.ReadString();                    
                    float timestamp = br.ReadSingle();
                    string updatedObject = br.ReadString();
                    string updatedMode = br.ReadString();
                    bool worldAnchorSet = br.ReadByte() > 0;                    

                    bo = BlenderObject.CreateFromByteStream(br);
                    bo.worldAnchorSet = worldAnchorSet;

                    // correct matrix 
                    bo.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(-90, 0, 0), new Vector3(.1f, .1f, .1f)) * bo.matrix;

                    if (bo != null)
                    {
                        ProcessBlenderObject(bo);
                    }
                }

                else if(packetType == TYPE_MATERIAL_TEXTURE)
                {
                    Debug.Log("TYPE_MATERIAL_TEXTURE");

                    int count = br.ReadByte(); 
                    for(int i=0; i<count; i++)
                    {
                        int width = br.ReadInt32();
                        int height = br.ReadInt32();

                        int textureLength = br.ReadInt32();
                        byte[] textureData = br.ReadBytes(textureLength);

                        string textureName = br.ReadString();

                        Debug.LogFormat("TYPE_MATERIAL_TEXTURE {0}", textureName);                                          

                        Texture2D tex = new Texture2D(width, height);
                        if (tex.LoadImage(textureData))
                        {
                            textures[textureName] = tex;
                        }
                        else
                        {
                            Debug.LogWarningFormat("Failed to load texture {0}", textureName); 
                        }
                    }

                    for(int i=blenderObjectsToBeProcessed.Count-1; i>=0; i--)
                    {
                        BlenderObject bo = blenderObjectsToBeProcessed[i];

                        if (CheckAndRequestTextures(bo) == 0)
                        {
                            blenderObjectsToBeProcessed.RemoveAt(i);
                            ProcessBlenderObject(bo);
                        }
                    }
                }

                else if (packetType == TYPE_OBJECT_REMOVED)
                {
                    Debug.Log("TYPE_OBJECT_REMOVED");

                    string boName = br.ReadString();

                    Debug.LogFormat("Removing BGO {0}", boName);

                    if (blenderGameObjects.ContainsKey(boName))
                    {
                        var bgo = blenderGameObjects[boName];
                        blenderGameObjects.Remove(boName);
                        Destroy(bgo.gameObject);

                        OnBlenderGameObjectDestoryed(boName); 
                    }
                }

                else if (packetType == TYPE_OBJECT_WORLDANCHOR)
                {
                    Debug.Log("TYPE_OBJECT_WORLDANCHOR");

                    string boName = br.ReadString();
                    int worldAnchorDataSize = br.ReadInt32(); 
                    byte[] worldAnchorData = br.ReadBytes(worldAnchorDataSize);

                    Debug.LogFormat("Setting world anchor for BGO {0}, count {1}", boName, worldAnchorDataSize);

                    // update existing Blender Object 
                    if (blenderGameObjects.ContainsKey(boName))
                    {
                        var bgo = blenderGameObjects[boName];
                        bgo.SetAnchor(worldAnchorData);

                        OnBlenderGameObjectUpdated(bgo);
                    }

                    // update pending BlenderObject 
                    for (int i = blenderObjectsToBeProcessed.Count - 1; i >= 0; i--)
                    {
                        BlenderObject bo = blenderObjectsToBeProcessed[i];

                        if (bo.name.Equals(boName, StringComparison.OrdinalIgnoreCase))
                        {
                            blenderObjectsToBeProcessed.RemoveAt(i);
                            bo.worldAnchor = worldAnchorData;
                            ProcessBlenderObject(bo);
                        }
                    }                    
                }

                else if (packetType == TYPE_OBJECT_OPERATION)
                {
                    Debug.Log("TYPE_OBJECT_OPERATION");
                    // TODO 
                }
            }
        }        
    }

    void ProcessBlenderObject(BlenderObject bo)
    {
        if (CheckAndRequestTextures(bo) > 0)
        {            
            blenderObjectsToBeProcessed.Add(bo); 
            return; 
        }

        if(CheckAndRequestWorldAnchor(bo) > 0)
        {
            blenderObjectsToBeProcessed.Add(bo);
            return;
        }

        BaseBlenderGameObject bgo;
        bool bgoCreated = false;     

        if (!blenderGameObjects.ContainsKey(bo.name))
        {
            GameObject go = new GameObject(bo.name);
            bgo = go.AddComponent<AnchoredBlenderGameObject>();
            go.transform.parent = BlenderObjectsContainer != null ? BlenderObjectsContainer.transform : transform;
            blenderGameObjects.Add(bo.name, bgo);
            bgoCreated = true; 
        }

        bgo = blenderGameObjects[bo.name];

        bgo.Bind(bo);

        if (bgoCreated)
        {
            OnBlenderGameObjectCreated(bgo);
        }
        else
        {
            OnBlenderGameObjectUpdated(bgo); 
        }
    }

    public void SendBlenderGameObjectWorldAnchor(BaseBlenderGameObject bgo, byte[] worldAnchor)
    {
        Debug.LogFormat("SendBlenderGameObjectWorldAnchor {0} {1}", bgo.name, worldAnchor.Length); 

        byte[] data = null;

        using (MemoryStream ms = new MemoryStream())
        {
            using (BinaryWriter br = new BinaryWriter(ms))
            {
                br.Write(TYPE_OBJECT_WORLDANCHOR);
                br.Write(0); // placeholder for packet size 
                br.Write(bgo.name);
                br.Write(worldAnchor.Length);
                br.Write(worldAnchor);                
            }

            data = ms.ToArray();
            UpdateSize(ref data, 1);
        }

        blenderService.SendData(data);
    }

    int CheckAndRequestTextures(BlenderObject bo)
    {
        List<string> boTextures = bo.Textures;

        Debug.LogFormat("{0} has {1} textures", bo.name, boTextures.Count); 

        if (boTextures.Count > 0)
        {
            for (int i = boTextures.Count - 1; i >= 0; i--)
            {
                if (HasTexture(boTextures[i]))
                {
                    boTextures.RemoveAt(i);
                }
            }

            if(boTextures.Count == 0)
            {
                return 0; 
            }

            // temporarily limit to 1 texture (simplify request handling) 
            while (boTextures.Count > 1)
            {
                boTextures.RemoveAt(boTextures.Count - 1);
            }

            byte[] data = null;

            // request textures 
            using (MemoryStream ms = new MemoryStream())
            {
                using (BinaryWriter br = new BinaryWriter(ms))
                {
                    br.Write(TYPE_MATERIAL_TEXTURE_REQUEST);
                    br.Write(0); // packet size placeholder 
                    br.Write(boTextures.Count);
                    foreach (var tex in boTextures)
                    {
                        Debug.LogFormat("Requesting image {0}", tex); 
                        br.Write(tex);
                    }
                }

                data = ms.ToArray();
                UpdateSize(ref data, 1);
            }

            Debug.Log("Sending TYPE_MATERIAL_TEXTURE_REQUEST packet"); 

            blenderService.SendData(data);            
            return boTextures.Count;
        }

        return 0; 
    }

    int CheckAndRequestWorldAnchor(BlenderObject bo)
    {
        if (!bo.worldAnchorSet)
        {
            return 0; 
        }

        if(bo.worldAnchor != null && bo.worldAnchor.Length > 0)
        {
            return 0; 
        }

        if (blenderGameObjects.ContainsKey(bo.name))
        {
            if (blenderGameObjects[bo.name].IsAnchored)
            {
                return 0; 
            }
        }

        byte[] data = null;

        // request textures 
        using (MemoryStream ms = new MemoryStream())
        {
            using (BinaryWriter br = new BinaryWriter(ms))
            {
                br.Write(TYPE_OBJECT_WORLDANCHOR_REQUEST);
                br.Write(0); // packet size placeholder 
                br.Write(bo.name);                
            }

            data = ms.ToArray();
            UpdateSize(ref data, 1);
        }

        Debug.Log("Sending TYPE_OBJECT_WORLDANCHOR_REQUEST packet");
        blenderService.SendData(data);

        return 1; 
    }

    public void SendOperation(BaseBlenderGameObject bgo, ObjectOperations op, Vector3 val)
    {
        byte[] data = null;

        using (MemoryStream ms = new MemoryStream())
        {
            using (BinaryWriter br = new BinaryWriter(ms))
            {
                br.Write(TYPE_OBJECT_OPERATION);
                br.Write(0); // placeholder for packet size 
                br.Write(bgo.name);
                br.Write((byte)op);
                br.Write(val.x);
                br.Write(val.y);
                br.Write(val.z);
            }

            data = ms.ToArray();
            UpdateSize(ref data, 1);
        }

        Debug.Log("Sending TYPE_OBJECT_OPERATION packet");
        blenderService.SendData(data);
    }

    private void BlenderService_OnDataReceived(byte[] data)
    {
        ProcessData(data);
    }

    #region discovery callback 

    void BlenderServiceDiscovery_OnServiceDiscovered(string serviceName, string ipAddress, int port)
    {
        Debug.Log("BlenderServiceDiscovery_OnServiceDiscovered"); 

        BlenderServiceDiscovery.Instance.StopListening();
        BlenderServiceDiscovery.Instance.OnServiceDiscovered -= BlenderServiceDiscovery_OnServiceDiscovered;

        blenderService.ServiceIPAddress = ipAddress;
        blenderService.ServicePort = port;

        readyToConnect = true;

        ServiceState = ServiceStates.Discovered;
    }

    #endregion

    #region misc 

    public static void UpdateSize(ref byte[] data, int insertAtIndex=0)
    {
        var sizeData = BitConverter.GetBytes(data.Length);
        int sizeIdx = 0; 
        for(int i=insertAtIndex; i< insertAtIndex+4; i++)
        {
            data[i] = sizeData[sizeIdx++]; 
        }
    }

    #endregion 
}
