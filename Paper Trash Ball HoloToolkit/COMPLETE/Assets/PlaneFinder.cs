using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using HoloToolkit.Unity;
using System.Linq;

public class PlaneFinder : Singleton<PlaneFinder> {

    [Serializable]
    public struct PlaneCriteria
    {
        public string tag;         
        public PlaneTypes planeType;
        public float minX;
        public float minY; 
        public int minCount;  

        public List<GameObject> GetPlanes(List<GameObject> planes)
        {
            var localThis = this; 

            var satisfactory = planes.Where(plane =>
            {
                return plane.GetComponent<SurfacePlane>() != null
                    && (plane.GetComponent<SurfacePlane>().PlaneType & localThis.planeType) > 0
                    && plane.transform.localScale.x >= localThis.minX
                    && plane.transform.localScale.y >= localThis.minY;
            });

            return satisfactory.ToList(); 
        }

        public bool Evaluate(List<GameObject> planes)
        {
            return GetPlanes(planes).Count >= minCount;
        }
    }

    public PlaneCriteria[] spatialCriteria;

    public float scanTime = 20f;

    public bool Finished { get; private set; }

    public Material scanningMaterial;

    public Material defaultMaterial;    

    float scanningStratTime = 0f;

    bool makingPlanes = false;     

	void Start () {
        SurfaceMeshesToPlanes.Instance.MakePlanesComplete += Instance_MakePlanesComplete;

        StartScanningEnvironment();
    }    

    void StartScanningEnvironment()
    {
        SpatialMappingManager.Instance.SurfaceMaterial = scanningMaterial;
        SpatialMappingManager.Instance.DrawVisualMeshes = scanningMaterial != null;

        scanningStratTime = Time.time;

        if (!SpatialMappingManager.Instance.IsObserverRunning())
        {
            SpatialMappingManager.Instance.StartObserver();
        }
    }

    void Update () {

        if (Finished)
        {
            return; 
        }

        if(!makingPlanes && Time.time - scanningStratTime >= scanTime)
        {
            if (SpatialMappingManager.Instance.IsObserverRunning())
            {
                SpatialMappingManager.Instance.StopObserver(); 
            }

            makingPlanes = true;
            SurfaceMeshesToPlanes.Instance.MakePlanes();
        }        
    }

    void Instance_MakePlanesComplete(object source, System.EventArgs args)
    {
        makingPlanes = false;

        int satisfiedCriteriaCount = spatialCriteria.Where(criteria => criteria.Evaluate(SurfaceMeshesToPlanes.Instance.ActivePlanes)).Count();

        if (satisfiedCriteriaCount == spatialCriteria.Length)
        {
            Finished = true;

            SpatialMappingManager.Instance.SurfaceMaterial = defaultMaterial;
            SpatialMappingManager.Instance.DrawVisualMeshes = defaultMaterial != null;
        }
        else
        {
            StartScanningEnvironment(); 
        }
    }

    public List<GameObject> GetPlanesForTag(string tag)
    {
        var planes = new List<GameObject>();
        
        var selectedCriteria = spatialCriteria.Where(criteria => criteria.tag.Equals(tag, StringComparison.OrdinalIgnoreCase)).ToList();        
        selectedCriteria.ForEach((critiera) =>
        {
            planes.AddRange(critiera.GetPlanes(SurfaceMeshesToPlanes.Instance.ActivePlanes).Where(plane => !planes.Contains(plane)));
        });

        return planes; 
    }
}
