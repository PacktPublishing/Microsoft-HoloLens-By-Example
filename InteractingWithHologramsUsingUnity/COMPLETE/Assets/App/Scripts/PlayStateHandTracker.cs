using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VR.WSA.Input;

public class PlayStateHandTracker : MonoBehaviour {

    #region nested classes 

    public class TrackedHand : MonoBehaviour
    {
        public uint handId;

        public bool isPressed = false; 

        public Vector3 translation = Vector3.zero;

        private Interactible trackedInteractible;        

        void Awake()
        {
            Collider collider = gameObject.AddComponent<SphereCollider>();
            collider.isTrigger = true;

            Rigidbody rigidBody = gameObject.AddComponent<Rigidbody>();
            rigidBody.isKinematic = true;
            rigidBody.useGravity = false;

            // uncomment to debug hand tracking 
            //GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            //go.transform.parent = transform; 

            transform.localScale = new Vector3(0.1f, 0.1f, 0.1f); 
        }
        

        public void UpdatePosition(Vector3 position)
        {
            translation = position - transform.position;
            this.transform.position = position;

            if(trackedInteractible != null)
            {
                PlayStateManager.Instance.Robot.MoveIKHandle(translation);
            }
        }

        void OnTriggerEnter(Collider other)
        {
            Interactible interactible = other.transform.GetComponent<Interactible>(); 
            if(interactible != null && interactible.interactionType == Interactible.InteractionTypes.Manipulation)
            {
                PlayStateManager.Instance.Robot.solverActive = true; 
                trackedInteractible = interactible;
            }
        }

        void OnTriggerExit(Collider other)
        {
            if(trackedInteractible != null)
            {
                PlayStateManager.Instance.Robot.solverActive = false;
            }

            trackedInteractible = null; 
        }
    }

    #endregion

    #region properties and variables

    public Dictionary<uint, TrackedHand> trackedHands = new Dictionary<uint, TrackedHand>();

    #endregion

    void Start () {
        RegisterForInteractionEvents(); 
    }
	
    void OnDestroy()
    {
        UnregisterFromInteractionEvents();
    }

    #region helper methods 

    TrackedHand TrackHand(InteractionSourceState state)
    {        
        if (state.source.kind != InteractionSourceKind.Hand)
        {
            return null;
        }

        Vector3 handPosition;
        if (!state.properties.location.TryGetPosition(out handPosition))
        {
            return null;
        }

        handPosition += state.headRay.direction * 0.1f;

        if (!trackedHands.ContainsKey(state.source.id))
        {
            GameObject go = new GameObject(string.Format("TrackedHand_{0}", state.source.id));
            TrackedHand trackedHand = go.AddComponent<TrackedHand>();
            trackedHand.handId = state.source.id;
            trackedHand.UpdatePosition(handPosition);

            trackedHands.Add(state.source.id, trackedHand);
        }

        trackedHands[state.source.id].UpdatePosition(handPosition);

        return trackedHands[state.source.id];
    }

    void LostTracking(InteractionSourceState state)
    {
        if (trackedHands.ContainsKey(state.source.id))
        {
            TrackedHand trackedHand = trackedHands[state.source.id];
            trackedHands.Remove(state.source.id);
            Destroy(trackedHand.gameObject);
        }
    }

    #endregion 

    #region event registeration 

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

    #endregion

    #region InteractionManager handlers 

    void InteractionManager_SourceDetected(InteractionSourceState state)
    {
        TrackHand(state);

    }

    void InteractionManager_SourcePressed(InteractionSourceState state)
    {
        TrackedHand trackedHand = TrackHand(state);
        trackedHand.isPressed = true;
    }

    void InteractionManager_SourceUpdated(InteractionSourceState state)
    {
        TrackHand(state);
    }

    void InteractionManager_SourceReleased(InteractionSourceState state)
    {
        TrackedHand trackedHand = TrackHand(state);
        trackedHand.isPressed = false;
    }

    void InteractionManager_SourceLost(InteractionSourceState state)
    {
        LostTracking(state);
    }

    #endregion 
}
