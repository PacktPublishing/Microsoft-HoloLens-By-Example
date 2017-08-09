using System;
using System.Linq;
using UnityEngine;
using HoloToolkit.Unity;

/// <summary>
/// Class is responsible for coordinating the scanning and creation of SurfacePlanes.
/// Once sufficently scanned (based on floor area), scanning will stop and notify the 
/// subscribers via the event OnPlaySpaceFinished. 
/// </summary>
[RequireComponent(typeof(SpatialMappingManager), typeof(SurfaceMeshesToPlanes))]
public class PlaySpaceManager : Singleton<PlaySpaceManager> {

    public delegate void PlaySpaceFinished(GameObject floor);

    public event PlaySpaceFinished OnPlaySpaceFinished = delegate { };     

    public bool Finished { get; private set; }

    private GameObject _floor;

    public GameObject Floor
    {
        get
        {
            return _floor;
        }
        private set
        {
            if (value == null)
            {
                _floor = null;
            }
            else
            {
                _floor = new GameObject("AnchoredFloor");
                _floor.transform.position = value.transform.position;
            }
        }
    }

    public float scanTime = 20f;

    float scanningStratTime = 0f;

    bool makingPlanes = false;

    void Start () {
        SurfaceMeshesToPlanes.Instance.MakePlanesComplete += SurfaceMeshesToPlanes_MakePlanesComplete;

        StartScanningEnvironment(); 
    }

    void StartScanningEnvironment()
    {
        scanningStratTime = Time.timeSinceLevelLoad;

        if (!SpatialMappingManager.Instance.IsObserverRunning())
        {
            SpatialMappingManager.Instance.StartObserver();
        }
    }

    void Update() {
        // ignore if Finished 
        if (Finished)
        {
            return;  
        }

        // If scanning and sufficent time has passed, then request SurfaceMeshesToPlanes
        // to make planes using the scanned surfaces. 
        if (!makingPlanes && Time.timeSinceLevelLoad - scanningStratTime >= scanTime)
        {
            if (SpatialMappingManager.Instance.IsObserverRunning())
            {
                SpatialMappingManager.Instance.StopObserver();
            }

            makingPlanes = true;
            SurfaceMeshesToPlanes.Instance.MakePlanes();
        }
    }

    /// <summary>
    /// Called when Plane creation has completed. Here we test if sufficent surface 
    /// area has been found, if not then resume scanning, otherwise flag as 
    /// finished and notify the listeners. 
    /// </summary>
    /// <param name="source"></param>
    /// <param name="args"></param>
    void SurfaceMeshesToPlanes_MakePlanesComplete(object source, EventArgs args)
    {
        makingPlanes = false;

        foreach(var plane in SurfaceMeshesToPlanes.Instance.ActivePlanes
            .Where((go) => { return go.GetComponent<SurfacePlane>() != null && go.GetComponent<SurfacePlane>().PlaneType == PlaneTypes.Floor; })
            .OrderBy((go) => { return go.GetComponent<SurfacePlane>().Plane.Bounds.Center.y; }))
        {
            var surfacePlane = plane.GetComponent<SurfacePlane>();
            if (surfacePlane)
            {                
                if (surfacePlane.Plane.Area >= 3)
                {
                    Floor = plane;

                    DisableCollidersOnActivePlanes();

                    Finished = true;

                    OnPlaySpaceFinished(Floor); 
                }
            }
        }

        if (!Finished)
        {
            StartScanningEnvironment(); 
        }
    }

    void DisableCollidersOnActivePlanes()
    {
        foreach(var planeGO in SurfaceMeshesToPlanes.Instance.ActivePlanes)
        {
            if (planeGO.GetComponent<Collider>())
            {
                planeGO.GetComponent<Collider>().enabled = false; 
            }
        }
    }

    /// <summary>
    /// Util method, to determine if a point is considered 'close' to the floor.
    /// </summary>
    /// <param name="point"></param>
    /// <returns></returns>
    public bool IsCloseToFloor(Vector3 point)
    {
        if(_floor == null)
        {
            return false; 
        }

        return Mathf.Abs(_floor.transform.position.y - point.y) <= 0.05f; 
    }
}
