using System; 
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using HoloToolkit.Unity;
using UnityEngine.VR.WSA.Input;

public class PlayStateManager : Singleton<PlayStateManager> {

    #region delegates and events 
    public delegate void CurrentInteractibleChanged();
    public event CurrentInteractibleChanged OnCurrentInteractibleChanged = delegate { };

    public delegate void FocusedObjectChanged(GameObject previousObject, GameObject newObject);
    public event FocusedObjectChanged OnFocusedObjectChanged = delegate { };

    #endregion 

    #region properties and variables 

    public PlayStateVoiceHandler voiceHandler; 

    private Interactible _currentInteractible;

    public Interactible CurrentInteractible
    {
        get { return _currentInteractible; }
        set
        {
            if (_currentInteractible != null)
            {
                _currentInteractible.GazeExited();
            }

            _currentInteractible = value;

            if (_currentInteractible != null)
            {
                _currentInteractible.GazeEntered();
            }

            OnCurrentInteractibleChanged(); 
        }
    }

    private bool _currentInteractibleSelected = false; 

    public bool IsCurrentInteractibleSelected
    {
        get { return _currentInteractibleSelected && _currentInteractible != null; }
        private set
        {
            _currentInteractibleSelected = value; 
        } 
    }

    private GameObject _foucsedObject = null;

    public GameObject FoucsedObject
    {
        get { return _foucsedObject; }
        set
        {
            if (_foucsedObject == value)
            {
                return;
            }

            OnFocusedObjectChanged(_foucsedObject, value);

            _foucsedObject = value;
        }
    }

    public RobotController Robot
    {
        get
        {
            return SceneManager.Instance.robotController; 
        }
    }

    private bool _stateActive = false; 

    public bool IsStateActive
    {
        get { return _stateActive; }
        set
        {
            _stateActive = value;

            OnStateActiveChanged(); 
        }
    }

    private Vector3 previousCumulativeDelta = Vector3.zero; 

    private GestureRecognizer navigationRecognizer; 

    private GestureRecognizer manipulationRecognizer;

    private GestureRecognizer _activeRecognizer; 

    public GestureRecognizer ActiveRecognizer
    {
        get { return _activeRecognizer; }
        private set
        {
            if(_activeRecognizer == value) { return; }

            if(_activeRecognizer != null)
            {
                _activeRecognizer.StopCapturingGestures(); 
            }

            _activeRecognizer = value; 

            if(_activeRecognizer != null)
            {
                Debug.LogFormat("_activeRecognizer {0}", _activeRecognizer); 
                _activeRecognizer.StartCapturingGestures(); 
            }
        }
    }

    #endregion 

    void Start () {
        SceneManager.Instance.OnStateChanged += SceneManager_OnStateChanged;

        OnFocusedObjectChanged += PlayStateManager_OnFocusedObjectChanged;
        OnCurrentInteractibleChanged += PlayStateManager_OnCurrentInteractibleChanged;

        #region Init GestureRecognizers 
        navigationRecognizer = new GestureRecognizer(); 
        navigationRecognizer.SetRecognizableGestures(GestureSettings.NavigationX | GestureSettings.NavigationY);
        navigationRecognizer.NavigationStartedEvent += NavigationRecognizer_NavigationStartedEvent;
        navigationRecognizer.NavigationUpdatedEvent += NavigationRecognizer_NavigationUpdatedEvent;
        navigationRecognizer.NavigationCompletedEvent += NavigationRecognizer_NavigationCompletedEvent;
        navigationRecognizer.NavigationCanceledEvent += NavigationRecognizer_NavigationCanceledEvent;

        manipulationRecognizer = new GestureRecognizer();
        manipulationRecognizer.SetRecognizableGestures(GestureSettings.ManipulationTranslate);
        manipulationRecognizer.ManipulationStartedEvent += ManipulationRecognizer_ManipulationStartedEvent;
        manipulationRecognizer.ManipulationUpdatedEvent += ManipulationRecognizer_ManipulationUpdatedEvent;
        manipulationRecognizer.ManipulationCompletedEvent += ManipulationRecognizer_ManipulationCompletedEvent;
        manipulationRecognizer.ManipulationCanceledEvent += ManipulationRecognizer_ManipulationCanceledEvent;
        #endregion 
    }

    #region ManipulationRecognizer events 

    void ManipulationRecognizer_ManipulationStartedEvent(InteractionSourceKind source, Vector3 cumulativeDelta, Ray headRay)
    {
        SelectCurrentInteractible();
        Robot.solverActive = true;
        previousCumulativeDelta = cumulativeDelta;
    }

    void ManipulationRecognizer_ManipulationUpdatedEvent(InteractionSourceKind source, Vector3 cumulativeDelta, Ray headRay)
    {
        Vector3 delta = new Vector3(cumulativeDelta.x - previousCumulativeDelta.x, (cumulativeDelta.y - previousCumulativeDelta.y), cumulativeDelta.z - previousCumulativeDelta.z);         
        Robot.MoveIKHandle(delta);
        previousCumulativeDelta = cumulativeDelta;
    }

    void ManipulationRecognizer_ManipulationCompletedEvent(InteractionSourceKind source, Vector3 cumulativeDelta, Ray headRay)
    {
        DeselectCurrentInteractible();
        Robot.solverActive = false;
    }

    void ManipulationRecognizer_ManipulationCanceledEvent(InteractionSourceKind source, Vector3 cumulativeDelta, Ray headRay)
    {
        DeselectCurrentInteractible();
        Robot.solverActive = false;
    }

    #endregion 

    #region NavigationRecognizer events     

    void NavigationRecognizer_NavigationStartedEvent(InteractionSourceKind source, Vector3 normalizedOffset, Ray headRay)
    {
        SelectCurrentInteractible(); 
    }

    void NavigationRecognizer_NavigationUpdatedEvent(InteractionSourceKind source, Vector3 normalizedOffset, Ray headRay)
    {
        if(Mathf.Abs(CurrentInteractible.interactionAxis.x) > 0)
        {
            Robot.Rotate(CurrentInteractible.name, CurrentInteractible.interactionAxis * -normalizedOffset.y);
        }
        else
        {
            Robot.Rotate(CurrentInteractible.name, CurrentInteractible.interactionAxis * -normalizedOffset.x);
        }
    }

    void NavigationRecognizer_NavigationCompletedEvent(InteractionSourceKind source, Vector3 normalizedOffset, Ray headRay)
    {
        DeselectCurrentInteractible(); 
    }

    void NavigationRecognizer_NavigationCanceledEvent(InteractionSourceKind source, Vector3 normalizedOffset, Ray headRay)
    {
        DeselectCurrentInteractible();
    }

    #endregion 

    void Update () {
        // ignore the update if we are not in the play state 
		if(!IsStateActive)
        {
            return; 
        }
        
	}

    void LateUpdate()
    {
        FoucsedObject = GazeManager.Instance.FocusedObject;
    }

    private void OnDestroy()
    {
        SceneManager.Instance.OnStateChanged -= SceneManager_OnStateChanged;

        #region tidy up GestureRecognizers 

        navigationRecognizer.NavigationStartedEvent -= NavigationRecognizer_NavigationStartedEvent;
        navigationRecognizer.NavigationUpdatedEvent -= NavigationRecognizer_NavigationUpdatedEvent;
        navigationRecognizer.NavigationCompletedEvent -= NavigationRecognizer_NavigationCompletedEvent;
        navigationRecognizer.NavigationCanceledEvent -= NavigationRecognizer_NavigationCanceledEvent;

        manipulationRecognizer.ManipulationStartedEvent -= ManipulationRecognizer_ManipulationStartedEvent;
        manipulationRecognizer.ManipulationUpdatedEvent -= ManipulationRecognizer_ManipulationUpdatedEvent;
        manipulationRecognizer.ManipulationCompletedEvent -= ManipulationRecognizer_ManipulationCompletedEvent;
        manipulationRecognizer.ManipulationCanceledEvent -= ManipulationRecognizer_ManipulationCanceledEvent;

        #endregion 
    }

    bool SelectCurrentInteractible()
    {
        Debug.Log("SelectCurrentInteractible");

        if(_currentInteractible == null)
        {
            return false; 
        }

        if (IsCurrentInteractibleSelected)
        {
            return true; 
        }

        IsCurrentInteractibleSelected = true;
        _currentInteractible.IsSelected = true;

        return true;       
    }

    bool DeselectCurrentInteractible()
    {
        if (_currentInteractible == null || !IsCurrentInteractibleSelected)
        {
            return false;
        }

        IsCurrentInteractibleSelected = false;
        _currentInteractible.IsSelected = false;

        GameObject currentHitObject = GazeManager.Instance.FocusedObject;

        if (currentHitObject == null)
        {
            CurrentInteractible = null;
        }
        else
        {
            var interactible = currentHitObject.GetComponent<Interactible>();
            CurrentInteractible = interactible;
        }

        return true; 
    }

    void SceneManager_OnStateChanged(SceneManager.States state)
    {
        IsStateActive = (state == SceneManager.States.Playing);
    }

    void PlayStateManager_OnFocusedObjectChanged(GameObject previousObject, GameObject newObject)
    {
        if (IsCurrentInteractibleSelected)
        {
            return;
        }

        if (newObject == null)
        {
            CurrentInteractible = null;
        }
        else
        {
            var interactible = newObject.GetComponent<Interactible>();
            CurrentInteractible = interactible;
        }
    }

    void OnStateActiveChanged()
    {
        if(SceneManager.Instance.State == SceneManager.States.Playing)
        {
            if(voiceHandler != null)
            {
                voiceHandler.StartHandler(); 
            }
        }
        else
        {
            if (voiceHandler != null)
            {
                voiceHandler.StopHandler();
            }
        }
    }

    void PlayStateManager_OnCurrentInteractibleChanged()
    {
        if(CurrentInteractible == null)
        {
            ActiveRecognizer = null;
            return; 
        }

        if(CurrentInteractible.interactionType == Interactible.InteractionTypes.Manipulation)
        {
            ActiveRecognizer = manipulationRecognizer;
        }
        else
        {
            ActiveRecognizer = navigationRecognizer; 
        }
    }
}
