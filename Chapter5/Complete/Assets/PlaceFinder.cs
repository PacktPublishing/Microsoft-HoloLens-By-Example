using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using HoloToolkit.Unity;

public class PlaceFinder : Singleton<PlaceFinder>
{
    [Serializable]
    public struct PlacementCriteria
    {
        public string tag;
        public GameObject prefab;
        public int count;
        public float minDistance;
        public float maxDistance;
        
        public Bounds Bounds
        {
            get
            {
                Bounds? bounds = null;

                if(prefab.GetComponent<Collider>() != null)
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
    }

    public struct Place
    {
        public string tag;
        public Vector3 position;
        public Vector3 normal; 
    }

    public PlacementCriteria[] placmentCriteria;

    public List<Place> foundPlaces = new List<Place>();

    public LayerMask CollisionLayers = (1 << 31) | (1 << 30);

    public bool Finished { get; private set; }

    public bool drawGizmos = false; 

    public void PlaceGameObjects(GameObject container = null)
    {
        Debug.LogFormat("PlaceGameObjects - found {0} places", foundPlaces.Count); 

        foreach (Place place in foundPlaces)
        {
            PlacementCriteria placmentCriteria = this.placmentCriteria.Where(pc => pc.tag.Equals(place.tag)).First();

            GameObject go = Instantiate(placmentCriteria.prefab);
            go.transform.position = place.position;
            //go.transform.up = place.normal; 
            if (container != null)
            {
                go.transform.parent = container.transform;
            }
        }
    }

    public void FindPlaces()
    {
        Finished = false;

        StartCoroutine(SearchForPlaces()); 
    }

    IEnumerator SearchForPlaces()
    {    
        foundPlaces.Clear();

        foreach (PlacementCriteria placementCriteria in placmentCriteria)
        {
            List<GameObject> planes = PlaneFinder.Instance.GetPlanesForTag(placementCriteria.tag);
            foreach (GameObject plane in planes)
            {
                FindPlacesOnPlane(placementCriteria, plane, foundPlaces);
            }

            yield return null;
        }

        Finished = true; 
    }

    void FindPlacesOnPlane(PlacementCriteria placementCriteria, GameObject plane, List<Place> places)
    {
        var boundingRadius = Mathf.Max(placementCriteria.Bounds.size.x, placementCriteria.Bounds.size.y, placementCriteria.Bounds.size.z) * 0.5f;

        var allPositions = GetSurfacePositionsForPlane(plane, placementCriteria.Bounds, 1.5f);
        var filteredPositions = FilterOutInvalidSurfacePositions(allPositions, -plane.transform.forward * boundingRadius, boundingRadius);
        var filteredAndSortedPositions = SortSurfacePosition(placementCriteria, filteredPositions);

        for (int i = 0; i < Mathf.Min(placementCriteria.count, filteredAndSortedPositions.Count); i++)
        {
            places.Add(new Place
            {
                tag = placementCriteria.tag,
                position = filteredAndSortedPositions[i],
                normal = -plane.transform.forward
            });
        }
    }

    List<Vector3> GetSurfacePositionsForPlane(GameObject plane, Bounds boundingBox, float overlap=1.0f)
    {
        overlap = 1.0f / overlap; 

        Vector3 center = plane.GetComponent<BoxCollider>().center;
        Vector3 size = plane.GetComponent<BoxCollider>().size;                        

        float sx = -size.x;
        float ex = size.x;
        float sy = -size.y;
        float ey = size.y;

        float divX = plane.transform.localScale.x / (boundingBox.size.x * overlap);
        float divY = plane.transform.localScale.y / (boundingBox.size.z * overlap);

        float stepX = (size.x * 2f) / divX;
        float stepY = (size.y * 2f) / divY;

        float countX = (size.x * 2f - (stepX * 2f)) / stepX;
        float countY = (size.y * 2f - (stepY * 2f)) / stepY;

        if(countX < 0 || countY < 0)
        {
            return new List<Vector3>();
        }         

        float marginX = ((countX - (int)countX) / (int)countX) * 0.5f;
        float marginY = ((countY - (int)countY) / (int)countY) * 0.5f;

        List<Vector3> surfacePositions = new List<Vector3>();

        for (float x = sx + stepX + marginX; x <= ex - stepX - marginX; x += stepX)
        {
            for (float y = sy + stepY + marginY; y <= ey - stepY - marginY; y += stepY)
            {
                surfacePositions.Add(plane.transform.TransformPoint(center + new Vector3(x, y, -size.z) * 0.5f) + -plane.transform.forward * 0.001f);
            }
        }

        return surfacePositions; 
    }

    List<Vector3> FilterOutInvalidSurfacePositions(List<Vector3> surfacePositions, Vector3 positionOffset, float boundingRadius)
    {
        return surfacePositions.Where(surfacePosition => !CollidesWithEnvironment(surfacePosition + positionOffset, boundingRadius) && !IsObstructedFromUser(surfacePosition + positionOffset)).ToList(); 
    }

    bool CollidesWithEnvironment(Vector3 position, float boundingRadius)
    {
        return Physics.OverlapSphere(position, boundingRadius, ~CollisionLayers, QueryTriggerInteraction.Ignore).Length > 0;
    }

    bool IsObstructedFromUser(Vector3 position)
    {
        Vector3 direction = (Camera.main.transform.position - position);
        float maxDistance = direction.magnitude;
        direction.Normalize(); 

        RaycastHit hitInfo; 
        if(Physics.Raycast(position, direction, out hitInfo, maxDistance))
        {
            return hitInfo.transform != Camera.main.transform; 
        }

        return false; 
    }

    List<Vector3> SortSurfacePosition(PlacementCriteria pc, List<Vector3> surfacePositions)
    {
        var costs = surfacePositions.Select((position, index) =>
        {
            Vector3 cameraPos = new Vector3(Camera.main.transform.position.x, 0, Camera.main.transform.position.z);
            Vector3 pos = new Vector3(position.x, 0, position.z);

            Vector3 direction = (pos - cameraPos);
            float distance = direction.magnitude;
            direction.Normalize();

            float dot = Vector3.Dot(Camera.main.transform.forward, direction);
            float dotCost = Mathf.Pow(1f - dot, 2f);

            float minDistanceCost = 0; 
            if(distance < pc.minDistance)
            {
                minDistanceCost = pc.minDistance/distance;
            }

            float maxDistanceCost = 0f; 
            if(distance > pc.maxDistance)
            {
                maxDistanceCost = distance/pc.maxDistance; 
            }

            float totalCost = dotCost + minDistanceCost + maxDistanceCost; 

            return new { index = index, cost = totalCost };
        }).ToList();

        costs.Sort((ca, cb) =>
        {
            return ca.cost.CompareTo(cb.cost);
        });

        return costs.Select(cost =>
        {
            return surfacePositions[cost.index];
        }).ToList();
    }

    void OnDrawGizmos()
    {
        if (!drawGizmos) return; 
         
        foreach (PlacementCriteria pc in placmentCriteria)
        {
            List<GameObject> planes = PlaneFinder.Instance.GetPlanesForTag(pc.tag);

            foreach (GameObject plane in planes)
            {                
                var boundingRadius = Mathf.Max(pc.Bounds.size.x, pc.Bounds.size.y, pc.Bounds.size.z) * 0.5f;
                var planeSurfacePositions = GetSurfacePositionsForPlane(plane, pc.Bounds, 1.5f);
                var filteredSurfacePositions = FilterOutInvalidSurfacePositions(planeSurfacePositions, -plane.transform.forward * boundingRadius, boundingRadius);
                var sortedAndFilteredSurfacePositions = SortSurfacePosition(pc, filteredSurfacePositions);

                float alphaStep = -1 * (1.0f / (float)sortedAndFilteredSurfacePositions.Count);
                float currentAlpha = 1.0f; 

                foreach (Vector3 position in sortedAndFilteredSurfacePositions)
                {
                    Gizmos.color = new Color(0f, 1f, 0f, currentAlpha);
                    currentAlpha += alphaStep;
                    Gizmos.DrawSphere(position, boundingRadius);
                }

                foreach (Vector3 position in planeSurfacePositions)
                {
                    if (filteredSurfacePositions.Contains(position))
                        continue;

                    Gizmos.color = Color.red;
                    Gizmos.DrawSphere(position, boundingRadius);
                }

                Gizmos.DrawRay(plane.transform.position, -plane.transform.forward * 0.5f);
            }
        }        
    }
}
