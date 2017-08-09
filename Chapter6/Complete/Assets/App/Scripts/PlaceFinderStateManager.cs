using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq; 
using UnityEngine;
using HoloToolkit.Unity;

/// <summary>
/// Responsible for searching the planes for a place given a bounding box, 
/// NB: Depedent on SurfaceMeshesToPlanes and that it has ran (and has surfaces) 
/// </summary>
public class PlaceFinderStateManager : Singleton<PlaceFinderStateManager> {

    public delegate void PlaceSearchComplete(List<Place> foundPlaces);
    public event PlaceSearchComplete OnPlaceSearchComplete = delegate { };

    [Serializable]
    public class Place
    {
        public Vector3 position;
        public Vector3 normal;
        public SurfacePlane surfacePlane;
        public Bounds bounds;
    }

    public LayerMask CollisionLayers = (1 << 31) | (1 << 30);

    public List<Place> foundPlaces = new List<Place>();

    internal bool _searching = false;

    public bool IsSearching
    {
        get
        {
            return _searching;
        }
        private set
        {
            _searching = value;
        }
    }

    void Start()
    {

    }

    #region Searching 

    public void FindPlacesForBounds(Bounds bounds, float minDistanceFromUser, float maxDistanceFromUser, PlaneTypes planeTypes, int numberOfPlacesToFind = 3)
    {
        IsSearching = true;

        StopAllCoroutines();

        foundPlaces.Clear();

        StartCoroutine(FindPlacesForBoundsCoroutine(bounds, minDistanceFromUser, maxDistanceFromUser, planeTypes, numberOfPlacesToFind));
    }

    IEnumerator FindPlacesForBoundsCoroutine(Bounds bounds, float minDistanceFromUser, float maxDistanceFromUser, PlaneTypes planeTypes, int numberOfPlacesToFind)
    {
        List<GameObject> planes = GetPlanesForPlaneTypes(planeTypes);

        foreach (GameObject plane in planes)
        {
            FindPlacesOnPlane(bounds, minDistanceFromUser, maxDistanceFromUser, plane, numberOfPlacesToFind, foundPlaces);
            yield return null;
        }

        IsSearching = false;

        OnPlaceSearchComplete(foundPlaces);
    }

    #region search methods 

    void FindPlacesOnPlane(Bounds bounds, float minDistanceFromUser, float maxDistanceFromUser, GameObject plane, int numberOfPlacesToFind, List<Place> foundPlaces)
    {
        float spaceBetweenPlaces = Mathf.Max(bounds.size.x, bounds.size.z) * 1.2f;

        var planeSurfacePositions = GetSurfacePositionsForPlane(plane, bounds);
        var filteredSurfacePositions = FilterOutInvalidSurfacePositions(planeSurfacePositions, -plane.transform.forward * bounds.max.y * 0.5f, bounds);
        var sortedAndFilteredSurfacePositions = SortSurfacePosition(minDistanceFromUser, maxDistanceFromUser, filteredSurfacePositions);

        int index = 0;
        while (foundPlaces.Count < numberOfPlacesToFind && index < sortedAndFilteredSurfacePositions.Count)
        {
            if (foundPlaces.Count == 0)
            {
                foundPlaces.Add(new Place
                {
                    position = sortedAndFilteredSurfacePositions[index],
                    normal = -plane.transform.forward,
                    surfacePlane = plane.GetComponent<SurfacePlane>(),
                    bounds = bounds
                });
            }
            else
            {
                bool satisfiedDistanceApart = true;

                Vector3 position = sortedAndFilteredSurfacePositions[index];

                foreach (var place in foundPlaces)
                {
                    float distance = (place.position - position).magnitude;
                    if (distance <= spaceBetweenPlaces)
                    {
                        satisfiedDistanceApart = false;
                        break;
                    }
                }

                if (satisfiedDistanceApart)
                {
                    foundPlaces.Add(new Place
                    {
                        position = sortedAndFilteredSurfacePositions[index],
                        normal = -plane.transform.forward,
                        surfacePlane = plane.GetComponent<SurfacePlane>(),
                        bounds = bounds
                    });
                }
            }

            index++;
        }
    }

    List<Vector3> GetSurfacePositionsForPlane(GameObject plane, Bounds boundingBox, float overlap = 1.5f)
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

        if (countX < 0 || countY < 0)
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

    List<Vector3> FilterOutInvalidSurfacePositions(List<Vector3> surfacePositions, Vector3 positionOffset, Bounds bounds)
    {
        return surfacePositions.Where(surfacePosition => !CollidesWithEnvironment(surfacePosition + positionOffset, bounds) && !IsObstructedFromUser(surfacePosition + positionOffset)).ToList();
    }

    bool CollidesWithEnvironment(Vector3 position, Bounds bounds)
    {
        Vector3 boundsPosition = new Vector3(position.x, position.y + bounds.extents.y, position.z);
        bool hasCollisions = Physics.OverlapBox(boundsPosition, bounds.extents, Quaternion.identity).Length > 0;

        //return hasCollisions; 

        if (hasCollisions)
        {
            return true;
        }

        Vector3 bottom = position;
        Vector3 top = new Vector3(position.x, position.y + bounds.size.y, position.z);
        Vector3 dir = (top - bottom);

        return Physics.Raycast(bottom, dir.normalized, bounds.size.y);
    }

    bool IsObstructedFromUser(Vector3 position)
    {
        Vector3 direction = (Camera.main.transform.position - position);
        float maxDistance = direction.magnitude;
        direction.Normalize();

        RaycastHit hitInfo;
        if (Physics.Raycast(position, direction, out hitInfo, maxDistance))
        {
            return hitInfo.transform != Camera.main.transform;
        }

        return false;
    }

    List<Vector3> SortSurfacePosition(float minDistanceFromUser, float maxDistanceFromUser, List<Vector3> surfacePositions)
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
            if (distance < minDistanceFromUser)
            {
                minDistanceCost = minDistanceFromUser / distance;
            }

            float maxDistanceCost = 0f;
            if (distance > maxDistanceFromUser)
            {
                maxDistanceCost = distance / maxDistanceFromUser;
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

    #endregion 

    #region private methods 

    List<GameObject> GetPlanesForPlaneTypes(PlaneTypes planeTypes)
    {
        return SurfaceMeshesToPlanes.Instance.GetActivePlanes(planeTypes);
    }

    #endregion

    #endregion 

    void OnDrawGizmos()
    {
        foreach (var place in foundPlaces)
        {
            Gizmos.color = Color.magenta;
            Vector3 boundsPosition = new Vector3(place.position.x, place.position.y + place.bounds.extents.y, place.position.z);
            Gizmos.DrawWireCube(boundsPosition, place.bounds.size);
        }
    }
}
