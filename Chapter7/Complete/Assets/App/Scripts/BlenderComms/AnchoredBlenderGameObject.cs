using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using UnityEngine.VR.WSA;
using UnityEngine.VR.WSA.Sharing;
using System;

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

        worldAnchor = gameObject.AddComponent<WorldAnchor>();

        if (worldAnchor.isLocated)
        {
            StartExportingWorldAnchor();
        }
        else
        {
            worldAnchor.OnTrackingChanged += WorldAnchor_OnTrackingChanged;
        }

        exporting = true;

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

        importing = true;

        worldAnchorBuffer.Clear();
        worldAnchorBuffer.AddRange(data);

        importAttempts = WorldAnchorImportAttempts;
        WorldAnchorTransferBatch.ImportAsync(data, OnImportComplete);

#endif

        return true;
    }

    private void WorldAnchor_OnTrackingChanged(WorldAnchor self, bool located)
    {
        Debug.Log("WorldAnchor_OnTrackingChanged");

        if (located)
        {
            worldAnchor.OnTrackingChanged -= WorldAnchor_OnTrackingChanged;
            StartExportingWorldAnchor();
        }
    }

    private void StartExportingWorldAnchor()
    {
        Debug.Log("StartExportingWorldAnchor");

        if (worldAnchor == null)
        {
            worldAnchor = gameObject.GetComponent<WorldAnchor>();
        }

        if (worldAnchor == null)
        {
            return;
        }

        worldAnchorBuffer.Clear();

        WorldAnchorTransferBatch transferBatch = new WorldAnchorTransferBatch();
        transferBatch.AddWorldAnchor(gameObject.name, worldAnchor);
        WorldAnchorTransferBatch.ExportAsync(transferBatch, OnExportDataAvailable, OnExportComplete);
    }

    private void OnExportDataAvailable(byte[] data)
    {
        worldAnchorBuffer.AddRange(data);
    }

    private void OnExportComplete(SerializationCompletionReason completionReason)
    {
        Debug.LogFormat("OnExportComplete {0}", completionReason);

        exporting = false;

        if (completionReason == SerializationCompletionReason.Succeeded)
        {
            RaiseOnAnchored(worldAnchorBuffer.ToArray());
        }
        else
        {
            // TODO: handle expectational case 
        }
    }

    private void OnImportComplete(SerializationCompletionReason completionReason, WorldAnchorTransferBatch deserializedTransferBatch)
    {
        Debug.LogFormat("OnImportComplete {0}", completionReason);

        importing = false;

        if (completionReason != SerializationCompletionReason.Succeeded)
        {
            if (importAttempts > 0)
            {
                importing = true;
                importAttempts--;
                WorldAnchorTransferBatch.ImportAsync(worldAnchorBuffer.ToArray(), OnImportComplete);
            }
            return;
        }

        worldAnchor = deserializedTransferBatch.LockObject(gameObject.name, gameObject);
    }

    #endregion
}
