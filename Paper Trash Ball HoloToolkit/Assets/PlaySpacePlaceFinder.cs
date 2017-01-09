using System;
using System.Collections.Generic;
using UnityEngine;
using HoloToolkit.Unity;
using System.Linq;

public class PlaySpacePlaceFinder : Singleton<PlaySpacePlaceFinder> {

    public enum States
    {
        None, 
        Processing, 
        Finished 
    }

    [Tooltip("Prefab we will be placing")]
    public GameObject prefab;

    [Tooltip("Rule defining how far we want the hologram placed away from the user")]
    public float distanceFromUser = 0.6f;

    public bool Finished { get; private set; }

    public States State { get; private set; }

    bool solverInitialized = false;

    List<SpatialUnderstandingDllObjectPlacement.ObjectPlacementResult> queryPlacementResults = new List<SpatialUnderstandingDllObjectPlacement.ObjectPlacementResult>();

    public void FindPlaces()
    {
        if (!InitializeSolver())
        {
            return;
        }

        Reset();

        Bounds bounds = GetBoundsForObject(prefab);
        Vector3 halfDims = new Vector3(bounds.size.x * 0.5f, bounds.size.y * 0.5f, bounds.size.z * 0.5f);

        SpatialUnderstandingDllObjectPlacement.ObjectPlacementDefinition placementDefinition = SpatialUnderstandingDllObjectPlacement.ObjectPlacementDefinition.Create_OnFloor(halfDims);
        List<SpatialUnderstandingDllObjectPlacement.ObjectPlacementRule> placementRules = new List<SpatialUnderstandingDllObjectPlacement.ObjectPlacementRule>() {
                                         SpatialUnderstandingDllObjectPlacement.ObjectPlacementRule.Create_AwayFromPosition(Camera.main.transform.position, distanceFromUser),
                                    };
        AsyncRunQuery(placementDefinition, placementRules);
    }

    public bool PlaceGameObjects()
    {
        if(queryPlacementResults.Count == 0)
        {
            return false; 
        }
        
        var objectPlacementResult = queryPlacementResults.First();
        GameObject go = Instantiate(prefab);
        go.transform.position = objectPlacementResult.Position;
        go.transform.up = objectPlacementResult.Up;

        return true; 
    }

    bool InitializeSolver()
    {
        if (solverInitialized || !SpatialUnderstanding.Instance.AllowSpatialUnderstanding)
        {
            return solverInitialized;
        }

        if (SpatialUnderstandingDllObjectPlacement.Solver_Init() > 0)
        {
            solverInitialized = true;
        }

        return solverInitialized;
    }

    void Reset()
    {
        queryPlacementResults.Clear();

        if (SpatialUnderstanding.Instance.AllowSpatialUnderstanding)
        {
            SpatialUnderstandingDllObjectPlacement.Solver_RemoveAllObjects();
        }
    }

    bool AsyncRunQuery(SpatialUnderstandingDllObjectPlacement.ObjectPlacementDefinition placementDefinition,
        List<SpatialUnderstandingDllObjectPlacement.ObjectPlacementRule> placementRules = null,
        List<SpatialUnderstandingDllObjectPlacement.ObjectPlacementConstraint> placementConstraints = null)
    {
#if UNITY_WSA && !UNITY_EDITOR
        System.Threading.Tasks.Task.Run(() =>
            {
                RunQuery(placementDefinition, placementRules, placementConstraints); 
            }
        );

        return true; 
#else
        return RunQuery(placementDefinition, placementRules, placementConstraints);
#endif
    }

    bool RunQuery(SpatialUnderstandingDllObjectPlacement.ObjectPlacementDefinition placementDefinition,
        List<SpatialUnderstandingDllObjectPlacement.ObjectPlacementRule> placementRules = null,
        List<SpatialUnderstandingDllObjectPlacement.ObjectPlacementConstraint> placementConstraints = null)
    {
        if (SpatialUnderstandingDllObjectPlacement.Solver_PlaceObject(
                this.name,
                SpatialUnderstanding.Instance.UnderstandingDLL.PinObject(placementDefinition),
                (placementRules != null) ? placementRules.Count : 0,
                ((placementRules != null) && (placementRules.Count > 0)) ? SpatialUnderstanding.Instance.UnderstandingDLL.PinObject(placementRules.ToArray()) : IntPtr.Zero,
                (placementConstraints != null) ? placementConstraints.Count : 0,
                ((placementConstraints != null) && (placementConstraints.Count > 0)) ? SpatialUnderstanding.Instance.UnderstandingDLL.PinObject(placementConstraints.ToArray()) : IntPtr.Zero,
                SpatialUnderstanding.Instance.UnderstandingDLL.GetStaticObjectPlacementResultPtr()) > 0)
        {
            SpatialUnderstandingDllObjectPlacement.ObjectPlacementResult placementResult = SpatialUnderstanding.Instance.UnderstandingDLL.GetStaticObjectPlacementResult();

            queryPlacementResults.Add(placementResult.Clone() as SpatialUnderstandingDllObjectPlacement.ObjectPlacementResult);

            return true;
        }

        State = States.Finished;

        return true; 
    }

    Bounds GetBoundsForObject(GameObject prefab)
    {
        Bounds? bounds = null;

        if (prefab.GetComponent<Collider>() != null)
        {
            bounds = prefab.GetComponent<Collider>().bounds;
        }
        else
        {
            Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>();
            foreach (Renderer renderer in renderers)
            {
                if (bounds.HasValue)
                {
                    bounds.Value.Encapsulate(renderer.bounds);
                }
                else
                {
                    bounds = renderer.bounds;
                }
            }
        }

        return new Bounds(bounds.HasValue ? Vector3.zero : bounds.Value.center, bounds.HasValue ? bounds.Value.size : Vector3.zero);
    }
}
