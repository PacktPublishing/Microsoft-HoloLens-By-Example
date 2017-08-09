using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using HoloToolkit.Unity;
using UnityEngine.VR.WSA; 
using UnityEngine.VR.WSA.Input;

public class PlacementManager : Singleton<PlacementManager> {

    #region delegate and events 

    public delegate void ObjectPlaced(BaseBlenderGameObject bgo);
    public event ObjectPlaced OnObjectPlaced = delegate {};

    #endregion

    #region properties and variables 

    public Material placeableBoundsMaterial;

    private Queue<BaseBlenderGameObject> blenderGameObjectsReadyForPlacement = new Queue<BaseBlenderGameObject>();

    private GestureRecognizer tapRecognizer;

    // Threshold (the closer to 1, the stricter the standard) used to determine if a surface is vertical.
    private float upNormalThreshold = 0.85f;

    // Speed (1.0 being fastest) at which the object settles to the surface upon placement.
    private float placementVelocity = 0.06f;

    // last hit position 
    private float lastDistance = 2.0f;

    // virtual element used to assist in placement of hologram 
    private GameObject placeableBounds = null;

    public bool IsPlacing
    {
        get
        {
            return blenderGameObjectsReadyForPlacement.Count > 0; 
        }
    }

    #endregion 

    void Start () {
        // Create the object that will be used to indicate the bounds of the GameObject.
        placeableBounds = GameObject.CreatePrimitive(PrimitiveType.Cube);
        placeableBounds.GetComponent<Renderer>().sharedMaterial = placeableBoundsMaterial;
        placeableBounds.AddComponent<BoxCollider>(); 
        placeableBounds.layer = LayerMask.NameToLayer("Ignore Raycast");
        placeableBounds.SetActive(false);
    }

    private void OnDestroy()
    {
        StopTapRecognizer(); 
    }
    
    private void StartTapRecognizer()
    {
        if(tapRecognizer != null)
        {
            return; 
        }

        tapRecognizer = new GestureRecognizer();
        tapRecognizer.SetRecognizableGestures(GestureSettings.Tap);
        tapRecognizer.TappedEvent += TapRecognizer_TappedEvent;
        tapRecognizer.StartCapturingGestures(); 
    }

    private void StopTapRecognizer()
    {
        if (tapRecognizer == null)
        {
            return;
        }

        tapRecognizer.TappedEvent -= TapRecognizer_TappedEvent;
        tapRecognizer.StopCapturingGestures();
        tapRecognizer = null; 
    }

    private void TapRecognizer_TappedEvent(InteractionSourceKind source, int tapCount, Ray headRay)
    {
        if (IsPlacing)
        {
            // place object 
            BaseBlenderGameObject bgo = blenderGameObjectsReadyForPlacement.Dequeue();

            bgo.gameObject.SetActive(true);
            bgo.transform.position = placeableBounds.transform.position - (Vector3.up * placeableBounds.transform.localScale.y/2f);            

            OnObjectPlaced(bgo); 

            if(blenderGameObjectsReadyForPlacement.Count > 0)
            {
                UpdateTracking(); 
            }
            else
            {
                placeableBounds.SetActive(false);
            }
        }
    }

    void Update () {
        if (IsPlacing)
        {            
            UpdateTracking();
        }
	}

    public void AddObjectForPlacement(BaseBlenderGameObject bgo)
    {
        blenderGameObjectsReadyForPlacement.Enqueue(bgo);         

        if (blenderGameObjectsReadyForPlacement.Count == 1)
        {
            StartTapRecognizer();

            UpdateTracking(); 
        }

        bgo.gameObject.SetActive(false);
    }

    public BaseBlenderGameObject RemoveObjectForPlacement(string name)
    {
        Queue<BaseBlenderGameObject> tmpQueue = new Queue<BaseBlenderGameObject>();

        BaseBlenderGameObject removedBGO = null; 

        while (blenderGameObjectsReadyForPlacement.Count > 0)
        {
            var bgo = blenderGameObjectsReadyForPlacement.Dequeue(); 

            if(bgo.name.Equals(name, System.StringComparison.OrdinalIgnoreCase))
            {
                removedBGO = bgo; 
            }
            else
            {
                tmpQueue.Enqueue(bgo); 
            }
        }

        blenderGameObjectsReadyForPlacement = tmpQueue; 

        if(blenderGameObjectsReadyForPlacement.Count == 0)
        {
            placeableBounds.SetActive(false);
        }

        if (removedBGO)
        {
            removedBGO.gameObject.SetActive(true);
        }        

        return removedBGO; 
    }

    private void UpdateTracking()
    {
        if (WorldManager.state != PositionalLocatorState.Active)
        {
            placeableBounds.SetActive(false);
            return; 
        }

        Bounds bounds = blenderGameObjectsReadyForPlacement.Peek().transform.GetRenderingBounds();

        placeableBounds.transform.localScale = bounds.size;

        placeableBounds.SetActive(true); 

        Vector3 targetPosition = placeableBounds.transform.position;
        Vector3 surfaceNormal = Vector3.zero;
        RaycastHit hitInfo;

        bool hit = Physics.Raycast(Camera.main.transform.position,
                                Camera.main.transform.forward,
                                out hitInfo,
                                20f,
                                SpatialMappingManager.Instance.LayerMask);

        if (hit)
        {
            lastDistance = hitInfo.distance;
            surfaceNormal = hitInfo.normal;

            if (Mathf.Abs(surfaceNormal.y) <= (1 - upNormalThreshold)) // vertical surface 
            {
                targetPosition = hitInfo.point + ((Mathf.Abs(surfaceNormal.x) > Mathf.Abs(surfaceNormal.z) ? placeableBounds.transform.localScale.x : placeableBounds.transform.localScale.z) / 2.0f * hitInfo.normal);
                placeableBounds.transform.rotation = Quaternion.LookRotation(-surfaceNormal, Vector3.up);
            }
            else // horizontal surface 
            {
                targetPosition = hitInfo.point + (placeableBounds.transform.localScale.y / 2.0f * hitInfo.normal);
            }                            
        }
        else
        {
            // The raycast failed to hit a surface.  In this case, keep the object at the distance of the last
            // intersected surface.
            targetPosition = Camera.main.transform.position + (Camera.main.transform.forward * lastDistance);            
        }

        // Follow the user's gaze.
        float dist = Mathf.Abs((placeableBounds.transform.position - targetPosition).magnitude);
        placeableBounds.transform.position = Vector3.Lerp(placeableBounds.transform.position, targetPosition, placementVelocity / dist);
    }
}
