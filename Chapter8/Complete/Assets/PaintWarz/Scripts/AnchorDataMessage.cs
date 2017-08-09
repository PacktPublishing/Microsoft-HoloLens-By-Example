using UnityEngine;
using UnityEngine.Networking;

public class AnchorDataMessage : MessageBase
{
    public int packetNumber;
    public bool isEnd = false; 
    public byte[] data; 

    public AnchorDataMessage()
    {
       
    }

    public override string ToString()
    {
        return string.Format("AnchorDataMessage No. {0}, Is end {1}, data len {2}", packetNumber, isEnd, (data == null ? 0 : data.Length));
    }
}
