using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Interactible : MonoBehaviour {

    [Tooltip("Meaningful name for lookup")]
    public string lookupLabel = "";

    public Color focusedHighlightColor = new Color(1f, 1f, 0f);

    [Tooltip("Outline width when the object has focus")]
    public float focusedHighlightOutline = 0.002f; 

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
                defaultMaterials[i].SetColor("_OutlineColor", focusedHighlightColor);
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
        while(Mathf.Abs((HasFoucs ? focusedHighlightOutline : 0) - CurrentHighlightOutline) > float.Epsilon)
        {
            CurrentHighlightOutline = Mathf.Lerp(CurrentHighlightOutline, (HasFoucs ? focusedHighlightOutline : 0), 0.5f);
            yield return null; 
        }
    }
}
