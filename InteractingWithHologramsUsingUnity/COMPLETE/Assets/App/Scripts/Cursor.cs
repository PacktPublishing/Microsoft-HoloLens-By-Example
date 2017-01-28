using System; 
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using HoloToolkit.Unity;

/// <summary>
/// Slight extension from CursorManager, including text 
/// </summary>
public class Cursor : MonoBehaviour {

    public float textScalePerMeter = 0.00213345925f;

    [Tooltip("Drag the Cursor object to show when it hits a hologram.")]
    public GameObject CursorOnHolograms;

    [Tooltip("Drag the Cursor object to show when it does not hit a hologram.")]
    public GameObject CursorOffHolograms;

    [Tooltip("Cursor used when the PlayStateManagers selected interactible interaction type is manipulation")]
    public GameObject CursorManipulation;

    [Tooltip("Cursor used when the PlayStateManagers selected interactible interaction type is navigation (constrained on the y axis)")]
    public GameObject CursorHorizontalNavigation;

    [Tooltip("Cursor used when the PlayStateManagers selected interactible interaction type is navigation (constrained on the x axis)")]
    public GameObject CursorVerticalNavigation;

    [Tooltip("Distance, in meters, to offset the cursor from the collision point.")]
    public float DistanceFromCollision = 0.01f;   

    [Tooltip("Nested GameObject used to display the gameObjectTextLookup")]
    public TextMesh textMesh;

    private GameObject _currentCursor; 

    public GameObject CurrentCursor
    {
        get { return _currentCursor; }
        set
        {
            if (_currentCursor == value)
                return;

            if(_currentCursor != null)
            {
                _currentCursor.SetActive(false); 
            }

            _currentCursor = value;

            if (_currentCursor != null)
            {
                _currentCursor.SetActive(true);
            }
        }
    }

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

        if (CursorManipulation != null)
        {
            CursorManipulation.SetActive(false);
        }

        if (CursorHorizontalNavigation != null)
        {
            CursorHorizontalNavigation.SetActive(false);
        }

        if (CursorVerticalNavigation != null)
        {
            CursorVerticalNavigation.SetActive(false);
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

        // Place the cursor at the calculated position.
        transform.position = GazeManager.Instance.Position + GazeManager.Instance.Normal * DistanceFromCollision;

        // Orient the cursor to match the surface being gazed at.
        transform.up = GazeManager.Instance.Normal;

        SetCurrentCursor();
    }

    void SetCurrentCursor()
    {
        // leave the cursor as it is if a interactible is selected 
        if (PlayStateManager.Instance.IsCurrentInteractibleSelected)
        {
            return; 
        }
            
        if (FocusedObject == null)
        {
            CurrentCursor = CursorOffHolograms;
            return;
        }

        Interactible interactible = FocusedObject.GetComponent<Interactible>();

        if(interactible == null)
        {
            CurrentCursor = CursorOnHolograms;
            return;
        }

        if (interactible.interactionType == Interactible.InteractionTypes.Manipulation)
        {
            if (CursorManipulation != null)
            {
                CurrentCursor = CursorManipulation;
                return;
            }
        }
        else
        {
            if (interactible.interactionAxis.x > 0)
            {
                if (CursorVerticalNavigation != null)
                {
                    CurrentCursor = CursorVerticalNavigation;
                    return;
                }
            }
            else if (interactible.interactionAxis.z > 0)
            {
                if (CursorHorizontalNavigation != null)
                {
                    CurrentCursor = CursorHorizontalNavigation;
                    return;
                }
            }
        }

        CurrentCursor = CursorOnHolograms;
    }

    void OnFocusedObjectChanged(GameObject previousObject, GameObject newObject)
    {
        string text = string.Empty; 

        if (newObject != null)
        {
            text = SceneManager.Instance.robotController.GetFriendlyNameForGameObject(newObject.name); 
        }

        Text = text;        
    }    
}
