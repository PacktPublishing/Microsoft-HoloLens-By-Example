using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using HoloToolkit.Unity;

public class PlayStateManager : Singleton<PlayStateManager> {

    void Start () {
		
	}
	
	void Update () {
		if(SceneManager.Instance.State != SceneManager.States.Playing)
        {
            return; 
        }
	}
}
