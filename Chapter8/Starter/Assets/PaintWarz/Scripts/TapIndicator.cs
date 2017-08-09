using System.Collections;
using UnityEngine;

public class TapIndicator : MonoBehaviour {

    [Tooltip("How far the plane is pushed off the surface")]
    public float hoverDistance = 0.01f; 

	void Start () {
        GetComponent<Renderer>().enabled = false;
    }
	
	public void Show(Vector3 position, Vector3 normal, float displayTime = 2.0f)
    {
        StopAllCoroutines();
        GetComponent<Renderer>().enabled = true;
        transform.position = position + normal * hoverDistance;
        transform.up = normal;
        StartCoroutine(CountDown(displayTime));
    }

    IEnumerator CountDown(float displayTime)
    {
        yield return new WaitForSeconds(displayTime);
        GetComponent<Renderer>().enabled = false; 
    }
}
