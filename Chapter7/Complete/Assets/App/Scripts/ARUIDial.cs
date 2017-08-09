using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Vuforia;

public class ARUIDial : MonoBehaviour, ITrackableEventHandler
{
    #region delegates and events 

    public delegate void ARUIDialChanged(ARUIDial dial, float change);
    public event ARUIDialChanged OnARUIDialChanged = delegate { };

    #endregion 

    #region properties and variables 

    [Tooltip("Child GameObject that is used to determine the 'control' position from when the marker was detected (when initilised)")]
    public GameObject lockedGameObject; 

    private TrackableBehaviour trackableBehaviour;

    private bool initilised = false;

    private Vector3 previousUp = Vector3.zero; 
    private Vector3 previousForward = Vector3.zero;

    private Quaternion lockedGameObectsRotation = Quaternion.identity;     

    public bool IsTracking
    {
        get
        {
            if (trackableBehaviour)
            {
                return trackableBehaviour.CurrentStatus == TrackableBehaviour.Status.DETECTED || 
                    trackableBehaviour.CurrentStatus == TrackableBehaviour.Status.TRACKED || 
                    trackableBehaviour.CurrentStatus == TrackableBehaviour.Status.EXTENDED_TRACKED;
            }

            return false; 
        }
    }

    #endregion 

    void Start()
    {
        trackableBehaviour = GetComponent<TrackableBehaviour>();

        if (trackableBehaviour)
        {
            trackableBehaviour.RegisterTrackableEventHandler(this);
        }
    }

    void Update()
    {
        if (IsTracking)
        {
            if (initilised)
            {
                // detect displacement by checking the current up with the previous 
                if (Vector3.Dot(previousUp, transform.up) < 0.8f)
                {
                    initilised = false;
                }
                else
                {
                    float change = Vector3.Angle(transform.forward, previousForward);
                    Vector3 cross = Vector3.Cross(transform.forward, previousForward);
                    if (cross.y < 0) change = -change;

                    if (Mathf.Abs(change) > 5f)
                    {
                        OnARUIDialChanged(this, change);
                        previousForward = transform.forward;
                    }                    
                }
            }
            else
            {
                initilised = true;
                previousUp = transform.up;
                previousForward = transform.forward;

                if (lockedGameObject)
                {
                    lockedGameObectsRotation = lockedGameObject.transform.rotation;
                }                 
            }
        }
    }

    private void LateUpdate()
    {
        if (IsTracking)
        {
            if (initilised)
            {
                if (lockedGameObject)
                {
                    lockedGameObject.transform.rotation = lockedGameObectsRotation; 
                }
            }
        }
    }

    public void OnTrackableStateChanged(TrackableBehaviour.Status previousStatus, TrackableBehaviour.Status newStatus)
    {
        if(IsStatusApproxTracking(previousStatus) == IsStatusApproxTracking(newStatus))
        {
            return; // no substantial change, so ignore 
        } 

        if (IsStatusApproxTracking(newStatus))
        {
            OnTrackingFound();
        }
        else
        {
            OnTrackingLost();
        }
    }

    private bool IsStatusApproxTracking(TrackableBehaviour.Status status)
    {
        return status == TrackableBehaviour.Status.DETECTED || 
            status == TrackableBehaviour.Status.TRACKED || 
            status == TrackableBehaviour.Status.EXTENDED_TRACKED;
    }

    private void OnTrackingFound()
    {
        Renderer[] rendererComponents = GetComponentsInChildren<Renderer>(true);
        Collider[] colliderComponents = GetComponentsInChildren<Collider>(true);

        // Enable rendering:
        foreach (Renderer component in rendererComponents)
        {
            component.enabled = true;
        }

        // Enable colliders:
        foreach (Collider component in colliderComponents)
        {
            component.enabled = true;
        }

        Debug.Log("Trackable " + trackableBehaviour.TrackableName + " found");
    }

    private void OnTrackingLost()
    {
        Renderer[] rendererComponents = GetComponentsInChildren<Renderer>(true);
        Collider[] colliderComponents = GetComponentsInChildren<Collider>(true);

        // Disable rendering:
        foreach (Renderer component in rendererComponents)
        {
            component.enabled = false;
        }

        // Disable colliders:
        foreach (Collider component in colliderComponents)
        {
            component.enabled = false;
        }

        Debug.Log("Trackable " + trackableBehaviour.TrackableName + " lost");
    }
}
