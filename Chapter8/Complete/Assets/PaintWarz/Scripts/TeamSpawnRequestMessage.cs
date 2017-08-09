using UnityEngine;
using UnityEngine.Networking;

public class TeamSpawnRequestMessage : MessageBase
{   
    public Vector3 position = Vector3.zero;    

    public TeamSpawnRequestMessage()
    {

    }

    public TeamSpawnRequestMessage(Vector3 position)
    {
        this.position = position; 
    }

    public override string ToString()
    {
        return string.Format("TeamSpawnRequestMessage Starting position. {0}", position);
    }
}
