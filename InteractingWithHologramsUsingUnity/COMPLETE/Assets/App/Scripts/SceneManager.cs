using System; 
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using HoloToolkit.Unity;

public class SceneManager : Singleton<SceneManager> {

    public delegate void StateChanged(States state);
    public StateChanged OnStateChanged = delegate { };

    public enum States
    {
        Scanning, 
        PlaceSearch,
        PlaceSelect,  
        Playing 
    }    

    #region properties and variables 

    public RobotController robotController;

    public float robotMinDistanceFromUser = 1.0f;

    public float robotMaxDistanceFromUser = 3.0f;

    public TextMesh statusText;

    private States _state = States.Scanning;

    public States State
    {
        get { return _state; }
        set
        {
            _state = value;

            OnStateChanged(_state); 
        }
    }

    public string StatusText
    {
        get
        {
            return statusText.text;
        }
        set
        {
            statusText.text = value;

            if (statusText.text != null && statusText.text.Length > 0)
            {
                statusText.gameObject.SetActive(true);
            }
        }
    }

    #endregion 

    void Start () {
        OnStateChanged += SceneManager_OnStateChanged;

        State = States.Scanning;        
    }

    void SceneManager_OnStateChanged(States state)
    {
        Debug.LogFormat("OnStateChanged {0}", state);

        switch (state)
        {
            case States.Scanning:
                StartCoroutine(ScanningLoop());
                break;
            case States.PlaceSearch:
                StartCoroutine(PlaceSearchLoop());
                break;
            case States.PlaceSelect:
                StartCoroutine(PlaceSelectLoop());
                break;
            case States.Playing:
                StartCoroutine(PlayingLoop());
                break;
        }
    }

    #region state methods 

    IEnumerator ScanningLoop()
    {
        if (robotController.gameObject.activeSelf)
        {
            robotController.gameObject.SetActive(false);
        }

        StatusText = "Look around to scan your environment";

        ScanningStateManager.Instance.StartScanning(); 

        while (ScanningStateManager.Instance.IsScanning)
        {
            yield return null;
        }

        State = States.PlaceSearch;
    }

    IEnumerator PlaceSearchLoop()
    {
        Instance.StatusText = "Searching for place for hologram";        

        PlaceFinderStateManager.Instance.FindPlacesForBounds(robotController.GetBounds(RobotController.BoundsType.Placement), 
            robotMinDistanceFromUser, 
            robotMaxDistanceFromUser, 
            HoloToolkit.Unity.PlaneTypes.Floor);

        while (PlaceFinderStateManager.Instance.IsSearching)
        {
            yield return null;
        }

        State = States.PlaceSelect;        
    }

    IEnumerator PlaceSelectLoop()
    {
        if (PlaceFinderStateManager.Instance.foundPlaces.Count == 0)
        {
            StatusText = "Unable to find suitable place\nTap where you would like to place the robot";
        }
        else
        {
            StatusText = "Select where you would like to place the robot";
        }

        bool robotPlaced = false;

        PlaceSelectorStateManager.PlaceSelectComplete PlaceSelectCompleteHandler = null;
        PlaceSelectCompleteHandler = (position, normal) => {

            PlaceSelectorStateManager.Instance.OnPlaceSelectComplete -= PlaceSelectCompleteHandler;

            robotController.transform.position = position;
            robotController.OnPlaced(); 
            robotController.gameObject.SetActive(true);

            robotPlaced = true;
        };

        PlaceSelectorStateManager.Instance.OnPlaceSelectComplete += PlaceSelectCompleteHandler;

        PlaceSelectorStateManager.Instance.SelectPlace(PlaceFinderStateManager.Instance.foundPlaces);

        while (!robotPlaced)
        {
            yield return null;
        }

        State = States.Playing;
    }

    IEnumerator PlayingLoop()
    {
        //RobotInputManager.Instance.Robot = robot;

        StatusText = "Hold and drag to control the robot";
        HideStatusText(10f);

        while (State == States.Playing)
        {
            yield return null;
        }

        //RobotInputManager.Instance.Robot = null;
    }

    #endregion 

    #region Status Text 

    public void HideStatusText(float delay)
    {
        Invoke("HideStatusText", delay);
    }

    void HideStatusText()
    {
        statusText.gameObject.SetActive(false);
    }

    #endregion    
}
