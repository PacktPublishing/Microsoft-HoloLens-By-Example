using UnityEngine;
using HoloToolkit.Unity;

/// <summary>
/// Convenient class to easily update the cursor and, when active 
/// (IsChecking), update the color depending on how close it is 
/// to the floor (to indicate valid point) 
/// </summary>
[RequireComponent(typeof(CursorManager))]
public class CursorIndicator : Singleton<CursorIndicator> {

    [Tooltip("Color assigned to the cursor when not ready")]
    public Color notReadyColor = Color.red;

    [Tooltip("Color assigned to the cursor when ready")]
    public Color readyColor = Color.green;

    [Tooltip("GameObject associated to the cursor when on a surface, assuming a renderer is attached")]
    public GameObject onCursorGameObject;

    Color defaultColor = Color.white;

    /// <summary>
    /// The Renderer of whose Material we will be changing 
    /// </summary>
    Renderer cursorRenderer;

    private bool _isChecking = false;    
    
    public bool IsChecking
    {
        get { return _isChecking; }
        set
        {
            if(_isChecking == value)
            {
                return; 
            }

            _isChecking = value; 

            if (!_isChecking)
            {
                cursorRenderer.material.color = defaultColor;
            }
            else
            {
                cursorRenderer.material.color = notReadyColor;
            }
        }
    } 

	void Start () {
        cursorRenderer = onCursorGameObject.GetComponent<Renderer>();

        if (cursorRenderer)
        {
            defaultColor = cursorRenderer.material.color;
        }        
    }

    private void LateUpdate()
    {        
        if(IsChecking)
        {
            if (GazeManager.Instance.Hit)
            {
                if (PlaySpaceManager.Instance.IsCloseToFloor(GazeManager.Instance.HitInfo.point))
                {
                    cursorRenderer.material.color = readyColor;
                }                
                else
                {
                    cursorRenderer.material.color = notReadyColor;
                }
            }
            else
            {
                cursorRenderer.material.color = notReadyColor;
            }
        }        
    }
}
