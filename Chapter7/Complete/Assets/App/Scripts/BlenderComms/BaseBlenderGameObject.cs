using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using UnityEngine.VR.WSA;
using UnityEngine.VR.WSA.Sharing;
using System;

public abstract class BaseBlenderGameObject : MonoBehaviour
{
    #region delegates and events 

    public delegate void Anchored(BaseBlenderGameObject bgo, byte[] data);
    public event Anchored OnAnchored = delegate { };

    #endregion

    #region properties and variables 

    public virtual bool IsAnchored
    {
        get
        {
            return GetComponent<WorldAnchor>() != null;
        }
    }

    #endregion    

    protected virtual void Start()
    {

    }

    protected virtual void Update()
    {

    }

    public virtual void Bind(BlenderObject bo)
    {
        throw new NotImplementedException();
    }

    public virtual bool AnchorAtPosition(Vector3 position)
    {
        throw new NotImplementedException();
    }

    public virtual bool SetAnchor(byte[] data)
    {
        throw new NotImplementedException();
    }

    public void RaiseOnAnchored(byte[] data)
    {
        OnAnchored(this, data);
    }
}
