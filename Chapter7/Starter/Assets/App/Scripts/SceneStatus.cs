using System.Collections;
using UnityEngine;
using HoloToolkit.Unity; 

public class SceneStatus : Singleton<SceneStatus> {

    #region properties and variables 

    public TextMesh label;

    #endregion 

    void Start () {
		if(label == null)
        {
            label = GetComponentInChildren<TextMesh>(); 
        }
	}

    /// <summary>
    /// Update the text of the TextMesh
    /// </summary>
    /// <param name="text"></param>
    /// <param name="clearTime">< 0 will be ignored, otherwise will clear clear field after specified time</param>
    public void SetText(string text, float clearTime = -1)
    {
        StopAllCoroutines();

        label.text = text; 

        if(clearTime > 0)
        {
            StartCoroutine(ClearAfterElapsedTime(clearTime));
        }
    }
	
	IEnumerator ClearAfterElapsedTime(float timeInSeconds)
    {
        yield return new WaitForSeconds(timeInSeconds);

        SetText(""); 
    }
}
