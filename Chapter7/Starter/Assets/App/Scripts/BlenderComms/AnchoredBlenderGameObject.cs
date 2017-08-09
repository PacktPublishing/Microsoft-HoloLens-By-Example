using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VR.WSA;


public class AnchoredBlenderGameObject : BaseBlenderGameObject
{
    #region constants 

    const int WorldAnchorImportAttempts = 3;

    #endregion

    #region properties and variables 

    private WorldAnchor worldAnchor;

    private bool exporting = false;

    private bool importing = false;

    private List<byte> worldAnchorBuffer = new List<byte>();

    private int importAttempts = WorldAnchorImportAttempts;

    public override bool IsAnchored
    {
        get
        {
            return worldAnchor != null || importing;
        }
    }

    #endregion

    #region Bind method 

    public override void Bind(BlenderObject bo)
    {
        if (transform.childCount == 0)
        {
            GameObject childGO = new GameObject(bo.name);
            BlenderGameObject childBGO = childGO.AddComponent<BlenderGameObject>();
            childBGO.transform.parent = transform;
        }

        transform.GetChild(0).GetComponent<BaseBlenderGameObject>().Bind(bo);

        // world anchor 
        if (bo.worldAnchor != null)
        {
            Debug.Log("Anchor received from service");

            SetAnchor(bo.worldAnchor);
        }
    }

    #endregion 

    #region Anchor methods 

    public override bool AnchorAtPosition(Vector3 position)
    {
        if (exporting || importing)
        {
            return false;
        }

        if (worldAnchor)
        {
            Destroy(worldAnchor);
        }

        gameObject.transform.position = position;

        Debug.Log("AnchorAtPosition");

#if WINDOWS_UWP

        // TODO Implement exporting the WorldAnchor 

#endif 

        return true;
    }

    public override bool SetAnchor(byte[] data)
    {
        if (exporting || importing)
        {
            return false;
        }

        Debug.LogFormat("SetAnchor {0}", data.Length);

#if WINDOWS_UWP

        // TODO Implement importing the WorldAnchor        

#endif

        return true;
    }

    #endregion
}
