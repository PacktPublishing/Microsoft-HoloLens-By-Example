using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using HoloToolkit.Unity;
using UnityEngine.VR.WSA;

[RequireComponent(typeof(PlacementManager))]
public class SceneManager : Singleton<SceneManager> {

    #region constants 

    public const float LONG = 180f;
    public const float MEDIUM = 90f;
    public const float SHORT = 40f; 

    #endregion 

    void Start () {
      
    }    

    private void OnDestroy()
    {

    }

    void Update () {
       
    }    
}
