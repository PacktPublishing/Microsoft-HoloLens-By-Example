using UnityEngine;
using System.Collections;
using UnityEngine.VR.WSA;

public class SceneController : MonoBehaviour
{

    public enum State
    {
        Scanning,
        Placing,
        Playing
    }

    public GameObject BinPrefab;

    public GameObject Bin
    {
        get;
        private set;
    }

    public float scanningTime = 10f;

    State _currentState = State.Scanning;

    public State CurrentState
    {
        get { return _currentState; }
        set
        {
            _currentState = value;

            Debug.LogFormat("CurrentState {0}", _currentState);

            OnStateChanged();
        }
    }

    void OnStateChanged()
    {
        StopAllCoroutines();

        switch (_currentState)
        {
            case State.Scanning:
                StartCoroutine(ScanningStateRoutine());
                break;
            case State.Placing:
                StartCoroutine(PlacingStateRoutine());
                break;
            case State.Playing:
                StartCoroutine(PlayingStateRoutine());                 
                break;
        }

    }

    void Start()
    {
        CurrentState = State.Scanning;
    }

    IEnumerator ScanningStateRoutine()
    {
        GazeController.SharedInstance.RaycastLayers = LayerMask.GetMask("SpatialSurface", "Hologram", "UI");

        SpatialMappingManager.SharedInstance.IsObserving = true;

        yield return new WaitForSeconds(scanningTime);

        CurrentState = State.Placing;
    }

    IEnumerator PlacingStateRoutine()
    {
        GazeController.SharedInstance.RaycastLayers = LayerMask.GetMask("SpatialSurface", "UI");

        SpatialMappingManager.SharedInstance.IsObserving = false;
        SpatialMappingManager.SharedInstance.SurfacesVisible = false;

        Bin = GameObject.Instantiate(
            BinPrefab,
            Camera.main.transform.position + (Camera.main.transform.forward * 2f) - (Vector3.up * 0.25f),
            Quaternion.identity);

        var placebale = Bin.AddComponent<Placeable>();

        while (!placebale.Placed)
        {
            yield return null;
        }

        GameObject.Destroy(placebale);
        Bin.AddComponent<WorldAnchor>();

        CurrentState = State.Playing;
    }

    IEnumerator PlayingStateRoutine()
    {
        GazeController.SharedInstance.RaycastLayers = LayerMask.GetMask("SpatialSurface", "Hologram", "UI");
        PlayerInputController.SharedInstance.RegisterForInteractionEvents();

        while (CurrentState == State.Playing)
        {
            yield return null;
        }        
    }
}
