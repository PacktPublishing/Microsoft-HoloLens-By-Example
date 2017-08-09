using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VR.WSA.Input;

public class PlayerInputController : MonoBehaviour {

    public class HandState
    {
        public uint Id;

        private bool _isPressed = false; 

        public bool IsPressed
        {
            get { return _isPressed; }
            set
            {
                _isPressed = value; 

                if(_isPressed)
                {
                    PressedTimestamp = Time.time;
                }
            }
        }

        public float PressedTimestamp
        {
            get; private set; 
        }

        public Vector3 Position = Vector3.zero;
        public Vector3 AccumulativeVelocity = Vector3.zero;

        public void UpdatePosition(Vector3 position)
        {
            if (IsPressed)
            {
                Vector3 displacement = position - this.Position;
                this.AccumulativeVelocity += displacement;

                this.Position = position; 
            }
            else
            {
                this.Position = position;
                this.AccumulativeVelocity = Vector3.zero;
            }
        }
    }

    private static PlayerInputController _sharedInstance;

    public static PlayerInputController SharedInstance
    {
        get
        {
            if (_sharedInstance == null)
            {
                _sharedInstance = GameObject.FindObjectOfType<PlayerInputController>();
            }

            if (_sharedInstance == null)
            {
                GameObject instanceGameObject = new GameObject(typeof(PlayerInputController).Name);
                _sharedInstance = instanceGameObject.AddComponent<PlayerInputController>();
            }

            return _sharedInstance;
        }
    }

    public GameObject PaperPrefab;    

    public float ThrowForceMultiplier = 5.0f;    

    public Vector3 FingertipsOffset = new Vector3(0, 0.053f, 0.01f);

    GameObject trackedGameObject;

    Dictionary<uint, HandState> trackedHands = new Dictionary<uint, HandState>(); 

    public void RegisterForInteractionEvents()
    {
        InteractionManager.SourceDetected += InteractionManager_SourceDetected;
        InteractionManager.SourcePressed += InteractionManager_SourcePressed;
        InteractionManager.SourceReleased += InteractionManager_SourceReleased;
        InteractionManager.SourceUpdated += InteractionManager_SourceUpdated;
        InteractionManager.SourceLost += InteractionManager_SourceLost;
    }

    public void UnregisterFromInteractionEvents()
    {
        InteractionManager.SourceDetected -= InteractionManager_SourceDetected;
        InteractionManager.SourcePressed -= InteractionManager_SourcePressed;
        InteractionManager.SourceReleased -= InteractionManager_SourceReleased;
        InteractionManager.SourceUpdated -= InteractionManager_SourceUpdated;
        InteractionManager.SourceLost -= InteractionManager_SourceLost;
    }

    void OnDestroy()
    {
        UnregisterFromInteractionEvents(); 
    }

    HandState GetHandState(InteractionSourceState state)
    {
        if (state.source.kind != InteractionSourceKind.Hand)
        {
            return null;
        }       

        if (!trackedHands.ContainsKey(state.source.id))
        {
            Vector3 handPosition;
            if (!state.properties.location.TryGetPosition(out handPosition))
            {
                return null;
            }

            trackedHands.Add(state.source.id, new HandState
            {
                Id = state.source.id,
                Position = handPosition,
            });
        }

        return trackedHands[state.source.id];
    }

    void RemoveHandState(InteractionSourceState state)
    {
        if (trackedHands.ContainsKey(state.source.id))
        {
            trackedHands.Remove(state.source.id);
        }

        if(trackedGameObject != null)
        {
            GameObject.Destroy(trackedGameObject);
            trackedGameObject = null; 
        }
    }

    void InteractionManager_SourceDetected(InteractionSourceState state)
    {
        GetHandState(state);
    }

    void InteractionManager_SourcePressed(InteractionSourceState state)
    {
        HandState handState = GetHandState(state);
        handState.IsPressed = true;

        Vector3 handPosition;
        if (!state.properties.location.TryGetPosition(out handPosition))
        {
            RemoveHandState(state);
            return;
        }

        handState.UpdatePosition(handPosition);

        if (trackedGameObject == null)
        {
            trackedGameObject = Instantiate(PaperPrefab);
            trackedGameObject.GetComponent<Rigidbody>().useGravity = false;
            trackedGameObject.GetComponent<Collider>().enabled = false;
            trackedGameObject.transform.position = handState.Position + Camera.main.transform.TransformVector(FingertipsOffset);
        } 
    }

    void InteractionManager_SourceUpdated(InteractionSourceState state)
    {
        Vector3 handPosition;
        if (!state.properties.location.TryGetPosition(out handPosition))
        {
            RemoveHandState(state);
            return;
        }

        HandState handState = GetHandState(state);
        handState.UpdatePosition(handPosition);

        if (trackedGameObject != null)
        {
            trackedGameObject.transform.position = handState.Position + Camera.main.transform.TransformVector(FingertipsOffset);
        }
    }

    void InteractionManager_SourceReleased(InteractionSourceState state)
    {
        HandState handState = GetHandState(state);

        if (handState == null || !handState.IsPressed)
        {
            return;
        }

        Vector3 handPosition;
        if (!state.properties.location.TryGetPosition(out handPosition))
        {
            RemoveHandState(state);
            return;
        }        

        handState.UpdatePosition(handPosition);

        handState.IsPressed = false;

        const float minAcceleration = 0.009f;
        const float maxAcceleration = 0.3f;

        Vector3 acceleration = handState.AccumulativeVelocity / (Time.time - handState.PressedTimestamp);

        if(acceleration.magnitude > minAcceleration)
        {
            acceleration = acceleration.normalized * ((acceleration.magnitude / maxAcceleration) * ThrowForceMultiplier);
        }                    

        if (trackedGameObject != null)
        {
            trackedGameObject.GetComponent<Rigidbody>().useGravity = true;
            trackedGameObject.GetComponent<Collider>().enabled = true;
            trackedGameObject.GetComponent<Rigidbody>().velocity = acceleration; 
            trackedGameObject = null; 
        }
    }

    void InteractionManager_SourceLost(InteractionSourceState state)
    {
        RemoveHandState(state);

        if (trackedGameObject != null)
        {
            GameObject.Destroy(trackedGameObject);
            trackedGameObject = null;
        }
    }
}
