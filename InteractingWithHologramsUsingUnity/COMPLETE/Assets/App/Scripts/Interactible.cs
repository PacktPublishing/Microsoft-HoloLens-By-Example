using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Interactible : MonoBehaviour {

    public enum InteractionTypes
    {
        Navigation, 
        Manipulation
    }

    [Tooltip("Determines what/how interaction would be handled")]
    public InteractionTypes interactionType = InteractionTypes.Navigation;

    [Tooltip("Used to determine the roational axis for InteractionTypes.Navigation")]
    public Vector3 interactionAxis = new Vector3(1, 1, 1); 

    [Tooltip("Meaningful name for lookup")]
    public string lookupLabel = "";   

    public Color focusedHighlightColor = new Color(1f, 1f, 0f);

    public Color selectedHighlightColor = new Color(0.28f, 1f, 0f);

    [Tooltip("Outline width when the object has focus")]
    public float highlightOutline = 0.002f; 

    public float TargetHighlightOutline
    {
        get
        {
            return (IsSelected || HasFoucs ? highlightOutline : 0);
        }
    }

    public float CurrentHighlightOutline
    {
        get
        {
            if(defaultMaterials.Length > 0)
            {
                return defaultMaterials[0].GetFloat("_Outline");
            }

            return -1; 
        }
        set
        {
            for (int i = 0; i < defaultMaterials.Length; i++)
            {
                defaultMaterials[i].SetFloat("_Outline", value);
            }
        }
    }

    public Color TargetHighlightColor
    {
        get
        {
            if (IsSelected)
            {
                return selectedHighlightColor;
            }
            if (HasFoucs)
            {
                return focusedHighlightColor;
            }

            return CurrentHighlightColor;
        }
    }

    public Color CurrentHighlightColor
    {
        get
        {
            if (defaultMaterials.Length > 0)
            {
                return defaultMaterials[0].GetColor("_OutlineColor");
            }

            return Color.white;
        }
        set
        {
            for (int i = 0; i < defaultMaterials.Length; i++)
            {
                defaultMaterials[i].SetColor("_OutlineColor", TargetHighlightColor);
            }
        }
    }

    private bool _hasFocus = false; 

    public bool HasFoucs
    {
        get { return _hasFocus; }
        private set
        {
            _hasFocus = value;

            if (!IsSelected)
            {
                StopAllCoroutines();
                StartCoroutine(UpdateMaterial());
            }             
        }
    }

    private bool _selected = false;

    public bool IsSelected
    {
        get { return _selected; }
        set
        {
            Debug.LogFormat("Selected {0}", value); 
            _selected = value;

            StopAllCoroutines();
            StartCoroutine(UpdateMaterial());
        }
    }

    Material[] defaultMaterials;

    void Start () {
        defaultMaterials = GetComponent<Renderer>().materials;
    }
	
	void Update () {
		
	}

    public void GazeEntered()
    {
        HasFoucs = true ;
    }

    public void GazeExited()
    {
        HasFoucs = false; 
    }

    IEnumerator UpdateMaterial()
    {
        float targetHighlightOutline = TargetHighlightOutline;
        Color targetHighlightColor = TargetHighlightColor;

        while (Mathf.Abs(targetHighlightOutline - CurrentHighlightOutline) > float.Epsilon || (targetHighlightColor - CurrentHighlightColor).Length() > float.Epsilon)
        {
            CurrentHighlightOutline = Mathf.Lerp(CurrentHighlightOutline, targetHighlightOutline, 0.5f);
            CurrentHighlightColor = Color.Lerp(CurrentHighlightColor, targetHighlightColor, 0.5f);
            yield return null;
        }
    }
}
