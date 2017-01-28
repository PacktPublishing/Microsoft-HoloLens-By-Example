using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using HoloToolkit.Unity;
using UnityEngine.VR.WSA.Input;

public class PlaySpaceScanner : Singleton<PlaySpaceScanner> {

    public enum State
    {
        Undefined, 
        Scanning,
        ReadyToFinish,
        Finalizing,
        Finished
    }

    [Tooltip("Min area to be available for the scan to be considered complete")]
    public float minAreaForComplete = 50f;

    [Tooltip("Min horizontal area to be considered complete")]
    public float minHorizontalAreaForComplete = 10f;

    [Tooltip("Min vertical area to be considered complete")]
    public float minVerticalAreaForComplete = 10f;

    [Tooltip("Material applied to the SpatialUnderstandingCustomMesh surface mesh when NOT scanning")]
    public Material defaultSurfaceMaterial;

    [Tooltip("Material applied to the SpatialUnderstandingCustomMesh surface mesh when scanning")]
    public Material scanningSurfaceMaterial;

    private Material _surfaceMaterial; 

    public Material SurfaceMaterial
    {
        get
        {
            return _surfaceMaterial; 
        }
        set
        {
            _surfaceMaterial = value;

            SpatialUnderstandingCustomMesh.MeshMaterial = _surfaceMaterial;
            
            foreach(var surfaceObject in SpatialUnderstandingCustomMesh.SurfaceObjects)
            {
                surfaceObject.Renderer.material = _surfaceMaterial;
            } 
        }
    }

    public State CurrentState
    {
        get;
        private set;
    }

    private SpatialMappingObserver _mappingObserver;

    public SpatialMappingObserver MappingObserver
    {
        get
        {
            if(_mappingObserver == null)
            {
                _mappingObserver = FindObjectOfType<SpatialMappingObserver>(); 
            }

            return _mappingObserver;
        }
    }

    private SpatialUnderstandingCustomMesh _spatialUnderstandingCustomMesh;

    public SpatialUnderstandingCustomMesh SpatialUnderstandingCustomMesh
    {
        get
        {
            if (_spatialUnderstandingCustomMesh == null)
            {
                _spatialUnderstandingCustomMesh = FindObjectOfType<SpatialUnderstandingCustomMesh>();
            }

            return _spatialUnderstandingCustomMesh;
        }
    }

    void Start()
    {
        MappingObserver.SetObserverOrigin(Camera.main.transform.position);
        SpatialUnderstanding.Instance.ScanStateChanged += Instance_ScanStateChanged;

        SpatialUnderstandingCustomMesh.DrawProcessedMesh = true;
        SurfaceMaterial = defaultSurfaceMaterial;
    }

    void Instance_ScanStateChanged()
    {
        switch (SpatialUnderstanding.Instance.ScanState)
        {
            case SpatialUnderstanding.ScanStates.Scanning:
                InteractionManager.SourcePressed += OnAirTap;
                SurfaceMaterial = scanningSurfaceMaterial;
                CurrentState = State.Scanning;
                break;
            case SpatialUnderstanding.ScanStates.Finishing:
                InteractionManager.SourcePressed -= OnAirTap;
                CurrentState = State.Finalizing;
                break;
            case SpatialUnderstanding.ScanStates.Done:
                SurfaceMaterial = defaultSurfaceMaterial;
                CurrentState = State.Finished;
                break;
        }
    }

    public void Scan()
    {
        CurrentState = State.Scanning;

        if (!SpatialMappingManager.Instance.IsObserverRunning())
        {
            SpatialMappingManager.Instance.StartObserver();
        }

        if (SpatialUnderstanding.Instance.AllowSpatialUnderstanding && SpatialUnderstanding.Instance.ScanState == SpatialUnderstanding.ScanStates.None)
        {
            SpatialUnderstanding.Instance.RequestBeginScanning();
        }
    }

    void Update()
    {
        if(CurrentState == State.Scanning)
        {
            // Query the current playspace stats
            IntPtr statsPtr = SpatialUnderstanding.Instance.UnderstandingDLL.GetStaticPlayspaceStatsPtr();
            if (SpatialUnderstandingDll.Imports.QueryPlayspaceStats(statsPtr) > 0)
            {
                // Check our preset requirements
                SpatialUnderstandingDll.Imports.PlayspaceStats stats = SpatialUnderstanding.Instance.UnderstandingDLL.GetStaticPlayspaceStats();

                if ((stats.TotalSurfaceArea > minAreaForComplete) ||
                    (stats.HorizSurfaceArea > minHorizontalAreaForComplete) ||
                    (stats.WallSurfaceArea > minVerticalAreaForComplete))
                {
                    CurrentState = State.ReadyToFinish;
                }
            }
        }        
    }

    void OnAirTap(InteractionSourceState state)
    {
        if(CurrentState != State.ReadyToFinish || SpatialUnderstanding.Instance.ScanStatsReportStillWorking)
        {
            return; 
        }

        SpatialUnderstanding.Instance.RequestFinishScan();
    }
}
