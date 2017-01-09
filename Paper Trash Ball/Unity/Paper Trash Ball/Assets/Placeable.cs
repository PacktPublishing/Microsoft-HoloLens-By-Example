using System.Collections;
using System.Collections.Generic;
using UnityEngine.VR.WSA.Input;
using UnityEngine;

public class Placeable : MonoBehaviour {
    
    public LayerMask RaycastLayers = (1 << 31); // eq. LayerMask.GetMask("SpatialSurface");

    public float AngleThresholdToApplyOffset = 20f;

    public float RaycastMaxDistance = 15f;

    public float SurfaceAngleThreshold = 15f;

    public float CornerAngleDifferenceThreshold = 20f;

    public bool ValidPosition
    {
        get;
        private set;
    }        

    private bool _placing = false;

    public bool Placing
    {
        get { return _placing; }
        private set
        {
            _placing = true; 
        }
    }

    public bool Placed
    {
        get
        {
            const float distanceThreshold = 0.003f;
            return _placing && Vector3.Distance(transform.position, targetPosition) <= distanceThreshold;
        }
    }

    public float HoverDistance = 0.01f;

    public float PlacementSpeed = 1.5f;

    Vector3 targetPosition;

    Vector3 targetNormal;

    MeshFilter meshFilter;

    GestureRecognizer tapGestureRecognizer;

    void Start () {
        meshFilter = GetComponentInChildren<MeshFilter>();

        targetPosition = transform.position;
        targetNormal = transform.up;

        tapGestureRecognizer = new GestureRecognizer();
        tapGestureRecognizer.SetRecognizableGestures(GestureSettings.Tap);
        tapGestureRecognizer.TappedEvent += TapGestureRecognizer_TappedEvent;
        tapGestureRecognizer.StartCapturingGestures();
    }

    void Update () {

        if (!Placing)
        {
            Vector3 position;
            Vector3 normal;
            if (GetValidatedTargetPositionAndNormalFromGaze(out position, out normal))
            {
                ValidPosition = true;
                targetPosition = position;
                targetNormal = normal;
            }
        }        

        transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * PlacementSpeed);
        transform.up = Vector3.Lerp(transform.up, targetNormal, Time.deltaTime * PlacementSpeed);
    }

    bool GetValidatedTargetPositionAndNormalFromGaze(out Vector3 position, out Vector3 normal)
    {
        position = Vector3.zero;
        normal = Vector3.zero;

        Bounds boundingBox = meshFilter.mesh.bounds;
        Vector3 originPosition = GazeController.SharedInstance.GazeHitPosition;

        if (Vector3.Angle(Vector3.up, GazeController.SharedInstance.GazeHitNormal) >= AngleThresholdToApplyOffset)
        {
            originPosition += GazeController.SharedInstance.GazeHitNormal * boundingBox.extents.z * 2.2f; 
        }
        else
        {
            originPosition += Vector3.up * 0.2f;
        }        

        var raycastPositions = GetRaycastOrigins(boundingBox, originPosition);

        RaycastHit hit;
        if (!Physics.Raycast(raycastPositions[0], -Vector3.up, out hit, RaycastMaxDistance, RaycastLayers))
        {
            return false;                         
        }

        position = hit.point + (HoverDistance * hit.normal);
        normal = hit.normal;

        if(Vector3.Angle(Vector3.up, normal) > SurfaceAngleThreshold)
        {
            return false; 
        }

        if (!IsSurfaceApproximatelyEven(raycastPositions)){
            return false; 
        }

        return true;
    }

    bool IsSurfaceApproximatelyEven(Vector3[] raycastPositions)
    { 
        float previousAngle = Vector3.Angle(raycastPositions[0], raycastPositions[1]);

        for (int i=2; i<raycastPositions.Length; i++)
        {
            float angle = Vector3.Angle(raycastPositions[0], raycastPositions[i]);
            if(Mathf.Abs(previousAngle - angle) > CornerAngleDifferenceThreshold)
            {
                return false;
            }
            previousAngle = angle;
        }

        return true; 
    }

    Vector3[] GetRaycastOrigins(Bounds boundingBox, Vector3 originPosition)
    {        
        float minX = originPosition.x + boundingBox.center.x - boundingBox.extents.x;
        float maxX = originPosition.x + boundingBox.center.x + boundingBox.extents.x;

        float minY = originPosition.y + boundingBox.center.y - boundingBox.extents.y;

        float minZ = originPosition.z + boundingBox.center.z - boundingBox.extents.z;
        float maxZ = originPosition.z + boundingBox.center.z + boundingBox.extents.z;

        return new Vector3[]
        {
            new Vector3(originPosition.x + boundingBox.center.x, minY, originPosition.z + boundingBox.center.z), // centre
            new Vector3(minX, minY, minZ), // back left 
            new Vector3(minX, minY, maxZ), // front left 
            new Vector3(maxX, minY, maxZ), // front right 
            new Vector3(maxX, minY, minZ) // back right 
        };
    }

    void TapGestureRecognizer_TappedEvent(InteractionSourceKind source, int tapCount, Ray headRay)
    {
        if (ValidPosition)
        {
            Placing = true;
            tapGestureRecognizer.TappedEvent -= TapGestureRecognizer_TappedEvent;
        }
    }
}
