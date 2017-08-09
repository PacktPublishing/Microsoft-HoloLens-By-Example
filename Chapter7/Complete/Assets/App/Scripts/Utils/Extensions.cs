using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class Extensions {

    #region Vector3 

    public static Vector3 GetTranslation(this Matrix4x4 matrix)
    {
        Vector3 translate;
        translate.x = matrix.m03;
        translate.y = matrix.m13;
        translate.z = matrix.m23;
        return translate;
    }

    public static Vector3 GetScale(this Matrix4x4 matrix)
    {
        Vector3 scale;
        scale.x = new Vector4(matrix.m00, matrix.m10, matrix.m20, matrix.m30).magnitude;
        scale.y = new Vector4(matrix.m01, matrix.m11, matrix.m21, matrix.m31).magnitude;
        scale.z = new Vector4(matrix.m02, matrix.m12, matrix.m22, matrix.m32).magnitude;
        return scale;
    }

    #endregion

    #region Quaternion 

    public static Quaternion GetRotation(this Matrix4x4 matrix)
    {
        Vector3 forward;
        forward.x = matrix.m02;
        forward.y = matrix.m12;
        forward.z = matrix.m22;

        Vector3 upwards;
        upwards.x = matrix.m01;
        upwards.y = matrix.m11;
        upwards.z = matrix.m21;

        return Quaternion.LookRotation(forward, upwards);
    }

    #endregion

    #region Transform 

    public static Bounds GetRenderingBounds(this Transform t)
    {
        return t.GetRenderingBounds(Vector3.zero);
    }

    public static Bounds GetRenderingBounds(this Transform t, Vector3 padding)
    {
        Bounds bounds = new Bounds(t.position, Vector3.zero);

        {
            Renderer renderer = t.gameObject.GetComponent<Renderer>();
            if (renderer != null)
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        Renderer[] renderers = t.gameObject.GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in renderers)
        {
            bounds.Encapsulate(renderer.bounds);
        }

        bounds.size = new Vector3(bounds.size.x + padding.x, bounds.size.y + padding.y, bounds.size.z + padding.z);

        return bounds;
    }

    #endregion
}
