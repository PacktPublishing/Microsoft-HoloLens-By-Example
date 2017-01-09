using System; 
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using HoloToolkit.Unity;

/// <summary>
/// Slight extension from CursorManager, including text 
/// </summary>
public class BasicLabelCursor : MonoBehaviour {

    public float textScalePerMeter = 0.00213345925f;

    [Tooltip("Drag the Cursor object to show when it hits a hologram.")]
    public GameObject CursorOnHolograms;

    [Tooltip("Drag the Cursor object to show when it does not hit a hologram.")]
    public GameObject CursorOffHolograms;

    [Tooltip("Distance, in meters, to offset the cursor from the collision point.")]
    public float DistanceFromCollision = 0.01f;   

    [Tooltip("Nested GameObject used to display the gameObjectTextLookup")]
    public TextMesh textMesh;
    
    public string Text
    {
        get { return textMesh.text; }
        set
        {
            textMesh.text = value; 
        }
    }  

    private GameObject _foucsedObject = null;

    public GameObject FocusedObject
    {
        get { return _foucsedObject; }
        set
        {
            if (_foucsedObject == value)
            {
                return;
            }

            OnFocusedObjectChanged(_foucsedObject, value);

            _foucsedObject = value;
        }
    }

    private void Awake()
    {
        // Hide the Cursors to begin with.
        if (CursorOnHolograms != null)
        {
            CursorOnHolograms.SetActive(false);
        }
        if (CursorOffHolograms != null)
        {
            CursorOffHolograms.SetActive(false);
        }

        // Make sure there is a GazeManager in the scene
        if (GazeManager.Instance == null)
        {
            Debug.LogWarning("CursorManager requires a GazeManager in your scene.");
            enabled = false;
        }
    }

    private void LateUpdate()
    {
        FocusedObject = GazeManager.Instance.FocusedObject; 

        if(Text.Length > 0 && FocusedObject != null)
        {
            float scale = (Camera.main.transform.position - FocusedObject.transform.position).magnitude * textScalePerMeter;
            textMesh.transform.localScale = new Vector3(scale, scale, scale);
        }

        // Enable/Disable the cursor based whether gaze hit a hologram
        if (CursorOnHolograms != null)
        {
            CursorOnHolograms.SetActive(GazeManager.Instance.Hit);
        }
        if (CursorOffHolograms != null)
        {
            CursorOffHolograms.SetActive(!GazeManager.Instance.Hit);
        }

        // Place the cursor at the calculated position.
        gameObject.transform.position = GazeManager.Instance.Position + GazeManager.Instance.Normal * DistanceFromCollision;

        // Orient the cursor to match the surface being gazed at.
        gameObject.transform.up = GazeManager.Instance.Normal;
    }

    void OnFocusedObjectChanged(GameObject previousObject, GameObject newObject)
    {
        string text = string.Empty; 

        if (newObject != null)
        {
            text = SceneManager.Instance.GetFriendlyNameForGameObject(newObject.name); 
        }

        Text = text; 
    }    
}
