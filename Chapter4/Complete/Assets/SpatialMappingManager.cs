using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VR.WSA;

public class SpatialMappingManager : MonoBehaviour {

    static SpatialMappingManager _sharedInstance; 

    public static SpatialMappingManager SharedInstance
    {
        get
        {
            if(_sharedInstance == null)
            {
                _sharedInstance = GameObject.FindObjectOfType<SpatialMappingManager>(); 
            }

            if(_sharedInstance == null)
            {
                GameObject instanceGameObject = new GameObject(typeof(SpatialMappingManager).Name);
                _sharedInstance = instanceGameObject.AddComponent<SpatialMappingManager>(); 
            }

            return _sharedInstance; 
        }
    }

    public Material SurfaceMaterial;
    
    public float timeBetweenUpdates = 3.0f;

    public float removalDelay = 10.0f; 

    SurfaceObserver surfaceObserver;

    Dictionary<int, GameObject> cachedSurfaces = new Dictionary<int, GameObject>();
    Dictionary<int, float> surfacesToBeRemoved = new Dictionary<int, float>(); 

    bool _surfacesVisible = true; 

    public bool SurfacesVisible
    {
        get { return _surfacesVisible; }
        set
        {
            _surfacesVisible = value; 

            foreach (KeyValuePair<int, GameObject> entry in cachedSurfaces)
            {
                MeshRenderer renderer = entry.Value.GetComponent<MeshRenderer>();
                renderer.enabled = _surfacesVisible; 
            }
        }
    }

    bool _observing; 

    public bool IsObserving
    {
        get { return _observing; }
        set
        {
            _observing = value;

            StopAllCoroutines();

            if (_observing)
            {
                StartCoroutine(Observe());
            }
        }
    }         

    void Awake() {
        surfaceObserver = new SurfaceObserver();
        surfaceObserver.SetVolumeAsSphere(Vector3.zero, 2.0f);
    }

    void Start()
    {
        
    }

    IEnumerator Observe()
    {
        var wait = new WaitForSeconds(timeBetweenUpdates);
        while (IsObserving)
        {
            surfaceObserver.Update(OnSurfaceChanged);
            yield return wait;
        }
    }

    void Update()
    {
        var surfaceIds = surfacesToBeRemoved.Keys; 
        foreach(int surfaceId in surfaceIds)
        {
            if (surfacesToBeRemoved[surfaceId] >= Time.time)
            {
                surfacesToBeRemoved.Remove(surfaceId);

                GameObject surface;
                if (cachedSurfaces.TryGetValue(surfaceId, out surface))
                {
                    cachedSurfaces.Remove(key: surfaceId);
                    GameObject.Destroy(surface);
                }
            }
        }
    }

    void OnSurfaceChanged(SurfaceId surfaceId, SurfaceChange changeType, Bounds bounds, DateTime updateTime)
    {
        switch (changeType)
        {
            case SurfaceChange.Added:
            case SurfaceChange.Updated:
                {
                    if(surfacesToBeRemoved.ContainsKey(surfaceId.handle))
                    {
                        surfacesToBeRemoved.Remove(surfaceId.handle);
                    }

                    GameObject surface;
                    if (!cachedSurfaces.TryGetValue(surfaceId.handle, out surface))
                    {
                        surface = new GameObject();
                        surface.name = string.Format("surface_{0}", surfaceId.handle);
                        surface.layer = LayerMask.NameToLayer("SpatialSurface");
                        surface.transform.parent = transform;
                        surface.AddComponent<MeshRenderer>();
                        surface.AddComponent<MeshFilter>();
                        surface.AddComponent<WorldAnchor>();
                        surface.AddComponent<MeshCollider>();
                        cachedSurfaces.Add(surfaceId.handle, surface);
                    }

                    SurfaceData surfaceData;
                    surfaceData.id.handle = surfaceId.handle;
                    surfaceData.outputMesh = surface.GetComponent<MeshFilter>() ?? surface.AddComponent<MeshFilter>();
                    surfaceData.outputAnchor = surface.GetComponent<WorldAnchor>() ?? surface.AddComponent<WorldAnchor>();
                    surfaceData.outputCollider = surface.GetComponent<MeshCollider>() ?? surface.AddComponent<MeshCollider>();
                    surfaceData.trianglesPerCubicMeter = 1000;
                    surfaceData.bakeCollider = true;

                    if (!surfaceObserver.RequestMeshAsync(surfaceData, OnDataReady))
                    {
                        Debug.LogWarningFormat("Is {0} not a valid surface", surfaceData.id);
                    }
                    break;
                }
            case SurfaceChange.Removed:
                {
                    GameObject surface;
                    if (cachedSurfaces.TryGetValue(surfaceId.handle, out surface))
                    {
                        surfacesToBeRemoved.Add(surfaceId.handle, Time.time + removalDelay);                        
                    }
                    break;
                }
        }
    }

    void OnDataReady(SurfaceData bakedData, bool outputWritten, float elapsedBakeTimeSecond)
    {
        if (!outputWritten)
            return;

        GameObject surface;
        if (cachedSurfaces.TryGetValue(bakedData.id.handle, out surface))
        {            
            MeshRenderer renderer = surface.GetComponent<MeshRenderer>();
            if (SurfaceMaterial != null)
            {
                renderer.sharedMaterial = SurfaceMaterial;
            }
            renderer.enabled = SurfacesVisible;
        }
    }
}
