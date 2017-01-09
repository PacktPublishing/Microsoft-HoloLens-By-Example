using System; 
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using HoloToolkit.Unity;

public class SceneManager : Singleton<SceneManager> {

    public enum States
    {
        Scanning, 
        PlaceSearch,
        PlaceSelect,  
        Playing 
    }

    [Serializable]
    public class HologramFriendlyName
    {
        public string gameObjectName;
        public string friendlyName;

        public override string ToString()
        {
            return string.Format("{0} -> {1}", gameObjectName, friendlyName); 
        }
    }

    #region properties and variables 

    public RobotController robotController;

    public float robotMinDistanceFromUser = 1.0f;

    public float robotMaxDistanceFromUser = 3.0f;

    public TextMesh statusText;

    [Tooltip("List containing object name and friendly name, displayed via the text component")]
    public List<HologramFriendlyName> friendlyNameLookup = new List<HologramFriendlyName>();

    private States _state = States.Scanning;

    public States State
    {
        get { return _state; }
        set
        {
            _state = value;

            OnStateChanged(); 
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
        State = States.Scanning;	
	}	

    void OnStateChanged()
    {
        Debug.LogFormat("OnStateChanged {0}", _state);

        switch (_state)
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

    #region GameObject / FriendlyName Lookup 

    public string GetFriendlyNameForGameObject(string gameObjectName)
    {
        foreach (var hfn in friendlyNameLookup)
        {
            if (hfn.gameObjectName.Equals(gameObjectName, StringComparison.OrdinalIgnoreCase))
            {
                return hfn.friendlyName;
            }
        }

        return string.Empty;
    }

    public string GetGameObjectNameForFriendlyName(string friendlyName)
    {
        foreach (var hfn in friendlyNameLookup)
        {
            if (hfn.friendlyName.Equals(friendlyName, StringComparison.OrdinalIgnoreCase))
            {
                return hfn.gameObjectName;
            }
        }

        return string.Empty;
    }

    #endregion 
}
