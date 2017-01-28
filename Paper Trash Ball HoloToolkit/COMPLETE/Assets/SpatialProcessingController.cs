using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using HoloToolkit.Unity;
using System.Linq;

public class SpatialProcessingController : Singleton<SpatialProcessingController> {

    [Serializable]
    public struct PlaneCriteria
    {
        [SerializeField]
        string tag; 
        [SerializeField]
        PlaneTypes planeType;
        [SerializeField]
        float minArea;
        [SerializeField]
        int minCount;  

        public List<GameObject> GetPlanes(List<GameObject> planes)
        {
            var localThis = this; 

            var satisfactory = from plane in planes
                               where plane.GetComponent<SurfacePlane>() != null 
                                && (plane.GetComponent<SurfacePlane>().PlaneType & localThis.planeType) > 0 
                                && CalculateArea(plane) > localThis.minArea
                               select plane;                                

            return satisfactory.ToList(); 
        }

        public bool Evaluate(List<GameObject> planes)
        {
            return GetPlanes(planes).Count >= minCount;
        }

        public static float CalculateArea(GameObject plane)
        {
            Collider collider = plane.GetComponent<Collider>();
            PlaneTypes planeTypes = plane.GetComponent<SurfacePlane>().PlaneType;
            
            if((planeTypes & (PlaneTypes.Ceiling | PlaneTypes.Floor | PlaneTypes.Table)) > 0)
            {
                return collider.bounds.size.x * collider.bounds.size.z;
            }
            else if((planeTypes & (PlaneTypes.Wall)) > 0)
            {
                return collider.bounds.size.x * collider.bounds.size.y;
            }

            return 0; 
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
}
