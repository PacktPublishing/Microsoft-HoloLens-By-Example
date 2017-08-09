using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using UnityEngine.VR.WSA;
using UnityEngine.VR.WSA.Sharing;
using System;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class BlenderGameObject : BaseBlenderGameObject
{
    public override void Bind(BlenderObject bo)
    {
        // transform matrix 
        transform.localPosition = bo.matrix.GetTranslation();
        transform.localRotation = bo.matrix.GetRotation();
        transform.localScale = bo.matrix.GetScale();

        // mesh 
        if (bo.HasMesh)
        {
            MeshFilter mf = GetComponent<MeshFilter>();
            var mesh = new Mesh();

            mf.mesh = mesh;

            mesh.vertices = bo.verts;
            mesh.normals = bo.normals;
            mesh.uv = bo.uvs;
            mesh.triangles = bo.faces;

            mesh.RecalculateBounds();
        }

        // materials
        if (bo.HasMaterials)
        {
            MeshRenderer mr = GetComponent<MeshRenderer>();
            // temporarily supporting a single material 
            mr.sharedMaterial = CreateMaterial(bo.materials[0]);
        }

        // children 
        if (bo.HasChildren)
        {
            foreach (var i in Enumerable.Range(0, bo.children.Length))
            {
                BlenderObject boChild = bo.children[i];

                // has child? 
                Transform tChild = transform.Find(boChild.name);
                if (tChild == null)
                {
                    GameObject goChild = new GameObject(boChild.name);
                    goChild.AddComponent<BlenderGameObject>();
                    tChild = goChild.transform;
                    tChild.parent = transform;
                }

                BlenderGameObject bgoChild = tChild.GetComponent<BlenderGameObject>();
                bgoChild.Bind(boChild);
            }
        }
    }

    public static Material CreateMaterial(BlenderObject.BlenderObjectMaterial bom)
    {
        Material mat = new Material(Shader.Find("Custom/StandardSurface"));
        //Material mat = BlenderServiceManager.Instance.runtimeMaterials[0];
        mat.EnableKeyword("_Color");

        mat.SetColor("_Color", bom.diffuseColor);
        mat.SetColor("_EmissionColor", bom.emitColor);

        if (bom.HasImage)
        {
            mat.SetTexture("_MainTex", BlenderServiceManager.Instance.GetTexture(bom.imageName));
            mat.mainTextureScale = new Vector2(-1, 1);
        }

        return mat;
    }
}
