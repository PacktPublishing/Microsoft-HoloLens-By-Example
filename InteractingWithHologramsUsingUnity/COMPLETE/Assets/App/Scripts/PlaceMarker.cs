using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlaceMarker : MonoBehaviour {

    internal bool _selected = false;

    public Color defaultColor = new Color(35f / 255f, 241f / 255f, 216f / 255f);

    public Color selectedColor = new Color(171f / 255f, 248f / 255, 110f / 255);

    public bool Selected
    {
        get { return _selected; }
        set
        {
            _selected = value;

            OnSelectedChanged();
        }
    }

    void OnSelectedChanged()
    {
        UpdateMaterialColor();
    }

    void UpdateMaterialColor()
    {
        GetComponent<MeshRenderer>().material.color = _selected ? selectedColor : defaultColor;
    }
}
