using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using HoloToolkit.Unity;

public class ScanningStateManager : Singleton<ScanningStateManager> {

    public delegate void ScanningComplete(ScanningStateManager manager);
    public event ScanningComplete OnScanningComplete = delegate { };

    [Tooltip("Default material assigned to the observed surfaces")]
    public Material defaultMaterial;

    [Tooltip("Material assigned to the observed surfaces when scanning")]
    public Material scanningMaterial;

    [Tooltip("How much time (in seconds) that the SurfaceObserver will run after being started; used when 'Limit Scanning By Time' is checked.")]
    public float scanTime = 30.0f;

    [Tooltip("Minimum number of floor planes required in order to exit scanning/processing mode.")]
    public uint minimumFloors = 1;

    [Tooltip("Minimum number of wall planes required in order to exit scanning/processing mode.")]
    public uint minimumWalls = 1;

    /// <summary>
    /// Indicates if processing of the surface meshes is complete
    /// </summary>
    private bool meshesProcessed = false;

    internal bool _scanning = false;

    internal float _scanStartTimestamp = 0;

    /// <summary>
    /// Current scanning (Update method being processed) 
    /// </summary>
    public bool IsScanning
    {
        get
        {
            return _scanning;
        }
        private set
        {
            _scanning = value;

            _scanStartTimestamp = Time.time;

            OnScanningStateChanged();
        }
    }

    void Start()
    {

        // want to continously scan the environment to account for moving objects 
        if (!SpatialMappingManager.Instance.IsObserverRunning())
        {
            SpatialMappingManager.Instance.StartObserver();
        }

        SpatialMappingManager.Instance.DrawVisualMeshes = true;
        SpatialMappingManager.Instance.CastShadows = false;

        SurfaceMeshesToPlanes.Instance.MakePlanesComplete += SurfaceMeshesToPlanes_MakePlanesComplete;

        IsScanning = IsScanning;
    }

    void OnDestroy()
    {
        if (SurfaceMeshesToPlanes.Instance != null)
        {
            SurfaceMeshesToPlanes.Instance.MakePlanesComplete -= SurfaceMeshesToPlanes_MakePlanesComplete;
        }
    }

    void Update()
    {
        if (!IsScanning)
        {
            return;
        }

        if (!meshesProcessed)
        {
            if ((Time.time - SpatialMappingManager.Instance.StartTime) >= scanTime)
            {
                if (SpatialMappingManager.Instance.IsObserverRunning())
                {
                    SpatialMappingManager.Instance.StopObserver();
                }

                CreatePlanes();

                meshesProcessed = true;
            }
        }

    }    

    /// <summary>
    /// Creates planes from the spatial mapping surfaces.
    /// </summary>
    private void CreatePlanes()
    {
        // Generate planes based on the spatial map.
        SurfaceMeshesToPlanes surfaceToPlanes = SurfaceMeshesToPlanes.Instance;

        if (surfaceToPlanes != null && surfaceToPlanes.enabled)
        {
            surfaceToPlanes.MakePlanes();
        }
    }

    public void StartScanning()
    {
        if (IsScanning)
        {
            return;
        }

        IsScanning = true;
    }

    public void StopScanning()
    {
        IsScanning = false;
    }

    void OnScanningStateChanged()
    {
        if (IsScanning)
        {
            SpatialMappingManager.Instance.SurfaceMaterial = scanningMaterial;
        }
        else
        {
            SpatialMappingManager.Instance.SurfaceMaterial = defaultMaterial;
        }
    }

    #region SurfaceMeshesToPlanes

    void SurfaceMeshesToPlanes_MakePlanesComplete(object source, System.EventArgs args)
    {
        // Collection of floor and table planes that we can use to set horizontal items on.
        List<GameObject> horizontal = new List<GameObject>();

        // Collection of wall planes that we can use to set vertical items on.
        List<GameObject> vertical = new List<GameObject>();

        // Assign the result to the 'horizontal' list.
        horizontal = SurfaceMeshesToPlanes.Instance.GetActivePlanes(PlaneTypes.Table | PlaneTypes.Floor);

        // Assign the result to the 'vertical' list.
        vertical = SurfaceMeshesToPlanes.Instance.GetActivePlanes(PlaneTypes.Wall);

        // Check to see if we have enough horizontal planes (minimumFloors)
        // and vertical planes (minimumWalls), to set holograms on in the world.
        if (horizontal.Count >= minimumFloors && vertical.Count >= minimumWalls)
        {
            IsScanning = false;

            OnScanningComplete(this);
        }
        else
        {
            // We do NOT have enough floors/walls to place our holograms on...
            // re-setting meshesProcessed to false.
            meshesProcessed = false;
        }

        // restart the observer 
        SpatialMappingManager.Instance.StartObserver();
    }

    #endregion 
}
