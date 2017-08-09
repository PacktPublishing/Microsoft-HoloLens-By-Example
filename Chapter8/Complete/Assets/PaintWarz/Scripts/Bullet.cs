using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Bullet GameObject 
/// </summary>
public class Bullet : NetworkBehaviour {

    public float speed = 1.0f;

    public int damage = 20;

    public float flightTime = 5.0f;

    private float createdTimestamp = 0f; 

	void Start () {
        createdTimestamp = Time.timeSinceLevelLoad;

        if (PlaySpaceManager.Instance.Floor != null)
        {
            transform.parent = PlaySpaceManager.Instance.Floor.transform;
        }
    }
	
	void Update () {
        
        transform.position += transform.forward * speed * Time.deltaTime;
        
        if((Time.timeSinceLevelLoad - createdTimestamp) > flightTime)
        {            
            Destroy(gameObject);
        } 
	}

    /// <summary>
    /// Listen our for collisions with other colliders. If of
    /// type Player then notify them via the OnHit method and finally 
    /// destory self. 
    /// </summary>
    /// <param name="other"></param>
    private void OnTriggerEnter(Collider other)
    {         
        if (!this.hasAuthority)
        {
            return; 
        }

        var player = other.GetComponent<Player>();
        if (player)
        {
            player.OnHit(this); 
        }

        Destroy(gameObject);
    }
}
