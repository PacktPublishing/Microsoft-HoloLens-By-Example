using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using HoloToolkit.Unity;
using UnityEngine.VR.WSA;
using UnityEngine.VR.WSA.Sharing;

[RequireComponent(typeof(PlacementManager))]
public class SceneManager : Singleton<SceneManager> {

    #region constants 

    public const float LONG = 180f;
    public const float MEDIUM = 90f;
    public const float SHORT = 40f; 

    #endregion 

    #region properties and varaibles 

    public ARUIDial rotationUIDial;

    public ARUIDial translateXUIDial;

    public GameObject placementSoundEffect; 

    [Tooltip("How many meters we move the object per degree changed from the translateXUIDial")]
    public float metersPerDegree = 0.00138f;

    [Tooltip("How many degrees we rotate the object per degree changed from the rotationUIDial")]
    public float rotationPerDegree = 1.0f;

    [Tooltip("Elapsed scan time before trying to discovery service (used to ensure that an 'sufficent' area has been scanned)")]
    public float initialScanningTime = 5.0f; 

    private BlenderServiceManager blenderServiceManager;
    private PlacementManager placementManager;     

    #endregion 

    void Start () {
        if (rotationUIDial)
        {
            rotationUIDial.OnARUIDialChanged += RotationUIDial_OnARUIDialChanged;
        }

        if (translateXUIDial)
        {
            translateXUIDial.OnARUIDialChanged += TranslateXUIDial_OnARUIDialChanged;
        }

        blenderServiceManager = BlenderServiceManager.Instance;
        blenderServiceManager.OnBlenderServiceStateChanged += BlenderServiceManager_OnBlenderServiceStateChanged;
        blenderServiceManager.OnBlenderGameObjectCreated += BlenderServiceManager_OnBlenderGameObjectCreated;
        blenderServiceManager.OnBlenderGameObjectUpdated += BlenderServiceManager_OnBlenderGameObjectUpdated;
        blenderServiceManager.OnBlenderGameObjectDestoryed += BlenderServiceManager_OnBlenderGameObjectDestoryed;

        placementManager = PlacementManager.Instance;
        placementManager.OnObjectPlaced += PlacementManager_OnObjectPlaced;

        SceneStatus.Instance.SetText("Scanning room", 3.0f);
    }    

    private void OnDestroy()
    {
        if (blenderServiceManager)
        {
            blenderServiceManager.OnBlenderServiceStateChanged -= BlenderServiceManager_OnBlenderServiceStateChanged;
            blenderServiceManager.OnBlenderGameObjectCreated -= BlenderServiceManager_OnBlenderGameObjectCreated;
            blenderServiceManager.OnBlenderGameObjectUpdated -= BlenderServiceManager_OnBlenderGameObjectUpdated;
            blenderServiceManager.OnBlenderGameObjectDestoryed -= BlenderServiceManager_OnBlenderGameObjectDestoryed;
        }

        if (placementManager)
        {
            placementManager.OnObjectPlaced -= PlacementManager_OnObjectPlaced;
        }
    }

    void Update () {
        if(blenderServiceManager.ServiceState == BlenderServiceManager.ServiceStates.Stopped)
        {
            if(Time.time - SpatialMappingManager.Instance.StartTime > initialScanningTime)
            {
                SceneStatus.Instance.SetText("Searching for BlenderLIVE Service");

                blenderServiceManager.SearchAndConnectToService(); 
            }
        }        
    }

    private void TranslateXUIDial_OnARUIDialChanged(ARUIDial dial, float change)
    {
        float displacement = metersPerDegree * change;
        // for this example we are limited to one object (ie take the first one), you can easily extend this 
        // to allow for many but require some logic for inferring the object the user is wanting to manipulate
        // (or have buttons assigned to each object)
        var bgoNames = BlenderServiceManager.Instance.GetAllBlenderGameObjectNames();

        if (bgoNames.Count == 0)
        {
            return; 
        }

        var bgo = BlenderServiceManager.Instance.GetBlenderGameObjectWithName(bgoNames[0]);
        BlenderServiceManager.Instance.SendOperation(bgo, BlenderServiceManager.ObjectOperations.Translate, new Vector3(0, displacement, 0)); 
    }

    private void RotationUIDial_OnARUIDialChanged(ARUIDial dial, float change)
    {
        float rotation = rotationPerDegree * change;
        // for this example we are limited to one object (ie take the first one), you can easily extend this 
        // to allow for many but require some logic for inferring the object the user is wanting to manipulate
        // (or have buttons assigned to each object)
        var bgoNames = BlenderServiceManager.Instance.GetAllBlenderGameObjectNames();

        if (bgoNames.Count == 0)
        {
            return;
        }

        var bgo = BlenderServiceManager.Instance.GetBlenderGameObjectWithName(bgoNames[0]);
        BlenderServiceManager.Instance.SendOperation(bgo, BlenderServiceManager.ObjectOperations.Rotate, new Vector3(0, 0, rotation));
    }

    private void PlayPlacementSoundEffect(Vector3 position)
    {
        if (placementSoundEffect)
        {
            placementSoundEffect.transform.position = position;
            placementSoundEffect.GetComponent<AudioSource>().Play();
        }   
    }

    #region PlacementManager event handlers 

    private void PlacementManager_OnObjectPlaced(BaseBlenderGameObject bgo)
    {
        SceneStatus.Instance.SetText("");

        BaseBlenderGameObject.Anchored onAnchoredHandler = null;
        onAnchoredHandler = (BaseBlenderGameObject caller, byte[] data) =>
        {
            Debug.Log("PlacementManager_OnObjectPlaced.BaseBlenderGameObject.Anchored");

            caller.OnAnchored -= onAnchoredHandler;
            BlenderServiceManager.Instance.SendBlenderGameObjectWorldAnchor(caller, data);
        };

        Debug.Log("PlacementManager_OnObjectPlaced");

        bgo.OnAnchored += onAnchoredHandler;
        bgo.AnchorAtPosition(bgo.transform.position);

        PlayPlacementSoundEffect(bgo.transform.position);
    }

    #endregion 

    #region BlenderServiceManager event handlers 

    private void BlenderServiceManager_OnBlenderGameObjectDestoryed(string name)
    {
        SceneStatus.Instance.SetText(string.Format("{0} removed", name), MEDIUM);
    }

    private void BlenderServiceManager_OnBlenderGameObjectCreated(BaseBlenderGameObject bgo)
    {
        if (bgo.IsAnchored)
        {
            SceneStatus.Instance.SetText("");
            if (bgo.GetComponent<WorldAnchor>())
            {
                if (PlacementManager.Instance.RemoveObjectForPlacement(bgo.name))
                {                    
                    SceneStatus.Instance.SetText("Blender object placed", MEDIUM);
                    PlayPlacementSoundEffect(bgo.transform.position);
                }
            }
        }
        else
        {
            SceneStatus.Instance.SetText("Blender object ready for placement\nAir-tap on a suitable surface to place", MEDIUM);
            PlacementManager.Instance.AddObjectForPlacement(bgo);
        }        
    }

    private void BlenderServiceManager_OnBlenderGameObjectUpdated(BaseBlenderGameObject bgo)
    {
        SceneStatus.Instance.SetText("");
        if (bgo.IsAnchored)
        {
            if (PlacementManager.Instance.RemoveObjectForPlacement(bgo.name))
            {
                SceneStatus.Instance.SetText("Blender object placed", MEDIUM);
                PlayPlacementSoundEffect(bgo.transform.position);
            }            
        }
    }

    private void BlenderServiceManager_OnBlenderServiceStateChanged(BlenderServiceManager.ServiceStates state)
    {
        switch (state)
        {
            case BlenderServiceManager.ServiceStates.Discovered:
                SceneStatus.Instance.SetText("Found BlenderLIVE service", SHORT);
                break;
            case BlenderServiceManager.ServiceStates.Connecting:
                SceneStatus.Instance.SetText("Connecting to BlenderLIVE service", SHORT);
                break;
            case BlenderServiceManager.ServiceStates.Connected:
                SceneStatus.Instance.SetText("Connected to BlenderLIVE service", SHORT);
                break;
            case BlenderServiceManager.ServiceStates.Disconnected:
                SceneStatus.Instance.SetText("Disconnected from BlenderLIVE service", LONG);
                break;
            case BlenderServiceManager.ServiceStates.Failed:
                SceneStatus.Instance.SetText("BlenderLIVE connection failed\nPlease restart everything and try again.", LONG);
                break;
        }        
    }

    #endregion 
}
