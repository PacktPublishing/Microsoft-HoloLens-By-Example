using UnityEngine;
using UnityEngine.VR.WSA.Input;
using HoloToolkit.Unity;

/// <summary>
/// Simple class to detect and broadcast the tap gesture/event
/// </summary>
public class InputManager : Singleton<InputManager> {

    public delegate void Tap(GameObject target, Vector3 hitPosition, Vector3 hitNormal);

    public event Tap OnTap = delegate { }; 

    private GameObject _focusedGameObject; 

    public GameObject FocusedGameObject
    {
        get { return _focusedGameObject; }
        set
        {
            if(_focusedGameObject == value)
            {
                return; 
            }
            else
            {
                _focusedGameObject = value; 

                if(_focusedGameObject != null)
                {
                    // As done in HoloToolkit, we will cancel and re-start the gesture recognizer so gestures are not carried across from one item to another 
                    gestureRecognizer.CancelGestures();                    
                    gestureRecognizer.StartCapturingGestures();
                }
            }
        }
    }

    GestureRecognizer gestureRecognizer;

    void Start () {
        // Create a new GestureRecognizer. Sign up for tapped events.
        // Will regester Taps for both Hand and Clicker.
        gestureRecognizer = new GestureRecognizer();
        gestureRecognizer.SetRecognizableGestures(GestureSettings.Tap);
        gestureRecognizer.TappedEvent += GestureRecognizer_TappedEvent;

        gestureRecognizer.StartCapturingGestures(); 
    }

    void OnDestroy()
    {
        gestureRecognizer.StopCapturingGestures();
        gestureRecognizer.TappedEvent -= GestureRecognizer_TappedEvent;
        gestureRecognizer = null; 
    }

    void GestureRecognizer_TappedEvent(InteractionSourceKind source, int tapCount, Ray headRay)
    {
        UpdateFocusedGameObject();

        // sent the message OnSelect of the FocusedGameObject
        if (FocusedGameObject != null)
        {
            FocusedGameObject.SendMessage("OnSelect", SendMessageOptions.DontRequireReceiver);
        }

        // also broadcast the tap event 
        OnTap(
            FocusedGameObject, 
            GazeManager.Instance.Hit ? GazeManager.Instance.HitInfo.point : Vector3.zero,
            GazeManager.Instance.Hit ? GazeManager.Instance.HitInfo.normal : Vector3.zero);
    }

    /// <summary>
    /// Updated the FocusedGameObject based on the current users Gaze (via GazeManager) 
    /// </summary>
    void UpdateFocusedGameObject()
    {
        GameObject newFocusedGameObject = null;

        if (GazeManager.Instance.Hit && GazeManager.Instance.HitInfo.collider != null)
        {
            newFocusedGameObject = GazeManager.Instance.HitInfo.collider.gameObject;
        }

        FocusedGameObject = newFocusedGameObject; 
    }
}
