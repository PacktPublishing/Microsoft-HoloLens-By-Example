using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using HoloToolkit.Unity;

public class SceneController : Singleton<SceneController> {

    public enum State
    {
        Scanning,
        Placing,
        Playing
    }

    State _currentState = State.Scanning;

    public State CurrentState
    {
        get { return _currentState; }
        set
        {
            _currentState = value;
            OnStateChanged();
        }
    }

    void OnStateChanged()
    {
        StopAllCoroutines();

        Debug.LogFormat("Changed State to {0}", _currentState); 

        switch (_currentState)
        {
            case State.Scanning:
                StartCoroutine(ScanningStateRoutine());
                break;
            case State.Placing:
                StartCoroutine(PlacingStateRoutine());
                break;
            case State.Playing:
                
                break;
        }

    }

    void Start () {
        CurrentState = State.Scanning;
    }

    #region Placement using a brute force search (Requires the Scene GameObject SpatialProcessing to be active and all of it's attached Components)

    IEnumerator ScanningStateRoutine()
    {
        StatusText.Instance.Text = "Look around to scan your play area";

        while (!PlaneFinder.Instance.Finished) yield return null;

        StatusText.Instance.Text = "";

        CurrentState = State.Placing;
    }

    IEnumerator PlacingStateRoutine()
    {
        PlaceFinder.Instance.FindPlaces();

        while (!PlaceFinder.Instance.Finished) yield return null;

        PlaceFinder.Instance.PlaceGameObjects();

        CurrentState = State.Playing;
    }

    #endregion 

    #region Placement using SpatialUnderstanding (Requires the Scene GameObject SpatialUnderstanding to be active and all of it's attached Components) 

    //IEnumerator ScanningStateRoutine()
    //{
    //    PlaySpaceScanner.Instance.Scan(); 

    //    while (PlaySpaceScanner.Instance.CurrentState != PlaySpaceScanner.State.Finished)
    //    {
    //        if(PlaySpaceScanner.Instance.CurrentState == PlaySpaceScanner.State.Scanning)
    //        {
    //            StatusText.Instance.Text = "Look around to scan your play area";
    //        }
    //        else if (PlaySpaceScanner.Instance.CurrentState == PlaySpaceScanner.State.ReadyToFinish)
    //        {
    //            StatusText.Instance.Text = "Air tap when ready";
    //        }
    //        else if (PlaySpaceScanner.Instance.CurrentState == PlaySpaceScanner.State.Finalizing)
    //        {
    //            StatusText.Instance.Text = "Finalizing scan (please wait)";
    //        }

    //        yield return null;
    //    }

    //    StatusText.Instance.Text = "";

    //    CurrentState = State.Placing;
    //}

    //IEnumerator PlacingStateRoutine()
    //{
    //    PlaySpacePlaceFinder.Instance.FindPlaces();

    //    while (!PlaySpacePlaceFinder.Instance.Finished) yield return null;

    //    PlaySpacePlaceFinder.Instance.PlaceGameObjects();

    //    CurrentState = State.Playing;
    //}

    #endregion
}
