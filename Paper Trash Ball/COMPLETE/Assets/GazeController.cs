using UnityEngine;
using System.Collections;

public class GazeController : MonoBehaviour {

    private static GazeController _sharedInstance;

    public static GazeController SharedInstance
    {
        get
        {
            if (_sharedInstance == null)
            {
                _sharedInstance = GameObject.FindObjectOfType<GazeController>();
            }

            if (_sharedInstance == null)
            {
                GameObject instanceGameObject = new GameObject(typeof(GazeController).Name);
                _sharedInstance = instanceGameObject.AddComponent<GazeController>();
            }

            return _sharedInstance;
        }
    }

    public float MaxDistance = 3f;

    public LayerMask RaycastLayers = (1 << 31) | (1 << 30) | (1 << 5); // eq. LayerMask.GetMask("SpatialSurface", "Hologram", "UI");

    public Vector3 GazeHitPosition { get; private set; }

    public Vector3 GazeHitNormal { get; private set; }

    public Transform GazeHitTransform { get; private set; } 
    
    public Vector3 GazeDirection
    {
        get
        {
            return Camera.main.transform.forward;
        }
    }  

    public Vector3 GazeOrigin
    {
        get
        {
            return Camera.main.transform.position;
        }
    }

	void Update () {

        RaycastHit hit;

        if (Physics.Raycast(GazeOrigin, GazeDirection, out hit, MaxDistance, RaycastLayers))
        {
            GazeHitPosition = hit.point;
            GazeHitNormal = hit.normal;
            GazeHitTransform = hit.transform; 
        }
        else
        {
            GazeHitPosition = GazeOrigin + (GazeDirection * MaxDistance);
            GazeHitNormal = GazeDirection;
            GazeHitTransform = null; 
        }	
	}
}
