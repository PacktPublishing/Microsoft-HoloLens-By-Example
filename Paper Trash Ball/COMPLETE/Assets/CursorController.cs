using UnityEngine;
using System.Collections.Generic;

public class CursorController : MonoBehaviour {

    private static CursorController _sharedInstance;

    public static CursorController SharedInstance
    {
        get
        {
            if (_sharedInstance == null)
            {
                _sharedInstance = GameObject.FindObjectOfType<CursorController>();
            }

            if(_sharedInstance == null)
            {
                GameObject instanceGameObject = new GameObject(typeof(CursorController).Name);
                _sharedInstance = instanceGameObject.AddComponent<CursorController>(); 
            }

            return _sharedInstance;
        }
    }

    public LayerMask InteractiveLayers = (1 << 30) | (1 << 5); // eq. LayerMask.GetMask("Hologram", "UI");

    public Color InteractiveColor = new Color(0.67f, 1f, 0.47f);

    public Color DefaultColor = new Color(1, 1, 1);

    public float HoverDistance = 0.01f; 

    public GameObject Cursor;

    Quaternion cursorRotationFix = Quaternion.AngleAxis(90, Vector3.right);

    void LateUpdate()
    {
        Cursor.transform.position = GazeController.SharedInstance.GazeHitPosition +
            GazeController.SharedInstance.GazeHitNormal * HoverDistance;
        Cursor.transform.up = GazeController.SharedInstance.GazeHitNormal;
        Cursor.transform.rotation *= cursorRotationFix;

        Color cursorTargetColor = DefaultColor;
        
        if (GazeController.SharedInstance.GazeHitTransform != null 
            && (InteractiveLayers.value & (1 << GazeController.SharedInstance.GazeHitTransform.gameObject.layer)) > 0)
        {
            cursorTargetColor = InteractiveColor;
        }

        Cursor.GetComponent<MeshRenderer>().material.color = Color.Lerp(
            Cursor.GetComponent<MeshRenderer>().material.color,
            cursorTargetColor,
            2f * Time.deltaTime);        
    }
}
