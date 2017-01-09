using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VR.WSA;
using HoloToolkit.Unity;

public class PlayerInputController : Singleton<PlayerInputController> {

    [Tooltip("Prefab created and thrown by the user")]
    public GameObject PaperPrefab;

    [Tooltip("Offset when prefab instance is being dragged")]
    public Vector3 FingertipsOffset = new Vector3(0, 0.053f, 0.01f);

    /// <summary>
    /// Reference to the instantiated prefab 
    /// </summary>
    GameObject trackedGameObject;

    /// <summary>
    /// Set when we start tracking and used to determine overall velocity and direction of throw 
    /// </summary>
    Vector3 trackingStartPosition = Vector3.zero;

    /// <summary>
    /// In a valid state to track 
    /// </summary>
    public bool CanTrack
    {
        get
        {
            return SceneController.Instance.CurrentState == SceneController.State.Playing; 
        }
    }

    /// <summary>
    /// Currently tracking 
    /// </summary>
    public bool IsTracking
    {
        get
        {
            return CanTrack && GestureManager.Instance.ManipulationInProgress;
        }
    }

    void Start () {
        GestureManager.Instance.OnManipulationStarted += Instance_OnManipulationStarted;
        GestureManager.Instance.OnManipulationCompleted += Instance_OnManipulationCompleted;
        GestureManager.Instance.OnManipulationCanceled += Instance_OnManipulationCanceled;
	}        

    void OnDestroy()
    {
        GestureManager.Instance.OnManipulationStarted -= Instance_OnManipulationStarted;
        GestureManager.Instance.OnManipulationCompleted -= Instance_OnManipulationCompleted;
        GestureManager.Instance.OnManipulationCanceled -= Instance_OnManipulationCanceled;
    }

    void Update () {
        if (IsTracking)
        {
            UpdateTrackedGameObject(); 
        }
	}

    #region Tracking methods 

    void CreateAndStartTrackingGameObject()
    {
        if (trackedGameObject == null)
        {
            trackedGameObject = Instantiate(PaperPrefab);
            trackedGameObject.GetComponent<Rigidbody>().useGravity = false;
            trackedGameObject.GetComponent<Collider>().enabled = false; 
            trackedGameObject.transform.position = GestureManager.Instance.ManipulationPosition + Camera.main.transform.TransformVector(FingertipsOffset);

            trackingStartPosition = trackedGameObject.transform.position;
        }
    }

    void UpdateTrackedGameObject()
    {
        if (trackedGameObject != null)
        {
            trackedGameObject.transform.position = GestureManager.Instance.ManipulationPosition + Camera.main.transform.TransformVector(FingertipsOffset);            
        }
    }

    void ThrowTrackedGameObject()
    {

        const float minVelocity = 0.09f;
        const float maxVelocity = 0.3f;

        Vector3 displacement = trackedGameObject.transform.position - trackingStartPosition;
        Vector3 direction = displacement.normalized;

        // apply fallback velocity
        Vector3 velocity = Camera.main.transform.forward * 0.2f; 

        if(displacement.magnitude > minVelocity)
        {
            // apply default velocity 
            velocity += Camera.main.transform.forward * 2.5f;

            // apply additional velocity 
            velocity += Mathf.Min(maxVelocity, displacement.magnitude / maxVelocity) * direction * 4.0f;
        }        

        trackedGameObject.GetComponent<Rigidbody>().useGravity = true;        
        trackedGameObject.GetComponent<Collider>().enabled = true;
        trackedGameObject.GetComponent<Rigidbody>().velocity = velocity;

        trackedGameObject = null;
    }

    void DestoryTrackedGameObject()
    {
        GameObject.Destroy(trackedGameObject);
        trackedGameObject = null; 
    }

    #endregion 

    #region GestureManager events 

    void Instance_OnManipulationStarted(UnityEngine.VR.WSA.Input.InteractionSourceKind sourceKind)
    {
        if(CanTrack)
        {
            CreateAndStartTrackingGameObject();
        }         
    }

    void Instance_OnManipulationCompleted(UnityEngine.VR.WSA.Input.InteractionSourceKind sourceKind)
    {
        ThrowTrackedGameObject(); 
    }

    void Instance_OnManipulationCanceled(UnityEngine.VR.WSA.Input.InteractionSourceKind sourceKind)
    {
        DestoryTrackedGameObject(); 
    }

    #endregion 
}
