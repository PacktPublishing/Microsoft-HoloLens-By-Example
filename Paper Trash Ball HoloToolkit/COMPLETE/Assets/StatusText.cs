using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using HoloToolkit.Unity;

public class StatusText : Singleton<StatusText> {

    TextMesh textMesh; 

    public string Text
    {
        get
        {
            return textMesh.text;
        }
        set
        {
            textMesh.text = value; 
        }
    }

    void Awake () {
        textMesh = GetComponentInChildren<TextMesh>(); 
	}
}
