using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using HoloToolkit.Unity;
using UnityEngine.VR.WSA.Input;

public class PlaceSelectorStateManager : Singleton<PlaceSelectorStateManager> {

    public delegate void PlaceSelectComplete(Vector3 position, Vector3 normal);
    public event PlaceSelectComplete OnPlaceSelectComplete = delegate { };

    public GameObject placeMarkerPrefab;

    List<PlaceFinderStateManager.Place> foundPlaces;

    List<GameObject> placedMarkers = new List<GameObject>();

    GestureRecognizer recognizer;

    internal bool _placing = false;

    public bool IsPlacing
    {
        get { return _placing; }
        private set
        {
            if (_placing == value)
            {
                return; // ignore if no change 
            }

            Debug.LogFormat("IsPlacing {0}", _placing); 

            _placing = value;

            OnPlacingStateChanged();
        }
    }

    public bool HasMarkers
    {
        get
        {
            return placedMarkers.Count > 0;
        }
    }

    public GameObject SelectedMarker
    {
        get
        {
            foreach (var marker in placedMarkers)
            {
                if (marker.GetComponent<PlaceMarker>().Selected)
                {
                    return marker;
                }
            }

            return null;
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

    protected void Awake()
    {
        recognizer = new GestureRecognizer();
    }

    void LateUpdate()
    {
        FoucsedObject = GazeManager.Instance.FocusedObject;
    }

    public void SelectPlace(List<PlaceFinderStateManager.Place> foundPlaces)
    {
        this.foundPlaces = foundPlaces;

        AddPlaceMarkers();

        IsPlacing = true;
    }

    void AddPlaceMarkers()
    {
        if (foundPlaces.Count > 0)
        {
            foreach (var place in foundPlaces)
            {
                GameObject go = Instantiate(placeMarkerPrefab);
                go.name = string.Format("{0}_{1}", placeMarkerPrefab.name, foundPlaces.Count);
                go.transform.position = place.position;
                go.transform.forward = -place.normal;

                float edgeLength = Mathf.Max(place.bounds.size.x, place.bounds.size.z);

                go.transform.localScale = new Vector3(edgeLength, edgeLength, go.transform.localScale.z);

                go.transform.parent = transform;

                placedMarkers.Add(go);
            }
        }
    }

    void RemovePlaceMarkers()
    {
        while (placedMarkers.Count > 0)
        {
            var placeMarkerGameObject = placedMarkers.Last();
            placedMarkers.RemoveAt(placedMarkers.Count - 1);

            GameObject.Destroy(placeMarkerGameObject);
        }
    }

    void OnPlacingStateChanged()
    {
        if (IsPlacing)
        {
            recognizer.SetRecognizableGestures(GestureSettings.Tap);

            recognizer.TappedEvent += Recognizer_TappedEvent;
            recognizer.StartCapturingGestures();

        }
        else
        {
            recognizer.CancelGestures();
            recognizer.StopCapturingGestures();
            recognizer.TappedEvent -= Recognizer_TappedEvent;

            RemovePlaceMarkers();
        }
    }

    void OnFocusedObjectChanged(GameObject previousObject, GameObject newObject)
    {
        if (previousObject != null && previousObject.GetComponent<PlaceMarker>() != null)
        {
            previousObject.GetComponent<PlaceMarker>().Selected = false;
        }

        if (newObject != null && newObject.GetComponent<PlaceMarker>() != null)
        {
            newObject.GetComponent<PlaceMarker>().Selected = true;
        }
    }

    void Recognizer_TappedEvent(InteractionSourceKind source, int tapCount, Ray headRay)
    {
        HandleTapEvent();
    }

    private void GestureManager_OnTapped(GameObject foucsedGameObject)
    {
        HandleTapEvent();
    }

    void HandleTapEvent()
    {
        Debug.LogFormat("Recognizer_TappedEvent {0} {1}", HasMarkers, (SelectedMarker != null));

        if (HasMarkers)
        {
            if (SelectedMarker != null)
            {
                var marker = SelectedMarker.GetComponent<PlaceMarker>();
                Vector3 position = marker.transform.position;
                Vector3 normal = -marker.transform.forward;

                IsPlacing = false;

                // get hit point from gaze and send the message back 
                OnPlaceSelectComplete(position, normal);
            }
        }
        else
        {
            IsPlacing = false;

            // get hit point from gaze and send the message back 
            OnPlaceSelectComplete(GazeManager.Instance.Position, Vector3.up);
        }
    }
}
