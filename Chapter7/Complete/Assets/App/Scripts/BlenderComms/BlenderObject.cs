using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public class BlenderObject
{

    #region inner classes 

    public class BlenderObjectMaterial
    {
        public string name;
        public float specularHardness;
        public Color ambientColor = Color.white;
        public float alpha = 1f;
        public Color diffuseColor = Color.white;
        public Color emitColor = Color.clear;
        public int imageWidth = -1;
        public int imageHeight = -1; 
        public string imageName = string.Empty;        

        public bool HasImage
        {
            get
            {
                return imageName != null && imageName.Length > 0; 
            }
        }

        public static BlenderObjectMaterial CreateFromByteStream(BinaryReader br)
        {
            string name = br.ReadString();
            float specularHardness = br.ReadSingle();
            Color ambientColor = CreateColorFromByteStream(br);
            float alpha = br.ReadSingle();
            Color diffuseColor = CreateColorFromByteStream(br);
            Color emitColor = CreateColorFromByteStream(br);
            byte hasImage = br.ReadByte();
            int imageWidth = -1;
            int imageHeight = -1;
            string imageName = null;             
            if (hasImage > 0)
            {
                imageWidth = br.ReadInt32();
                imageHeight = br.ReadInt32(); 
                imageName = br.ReadString(); 
            }

            BlenderObjectMaterial bom = new BlenderObjectMaterial
            {
                name = name,
                specularHardness = specularHardness,
                ambientColor = ambientColor,
                alpha = alpha,
                diffuseColor = diffuseColor,
                emitColor = emitColor,
                imageWidth = imageWidth, 
                imageHeight = imageHeight,
                imageName = imageName
            };

            return bom; 
        }

        public static Color CreateColorFromByteStream(BinaryReader br)
        {
            return new Color(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());  
        }
    }

    #endregion 

    public string name;
    public string mode;

    public Matrix4x4 matrix; 
    public Vector3[] verts = null;
    public Vector3[] normals = null;
    public Vector2[] uvs = null;
    public int[] faces = null;
    public BlenderObjectMaterial[] materials = null;
    public BlenderObject[] children = null;
    public bool worldAnchorSet = false; 
    public byte[] worldAnchor = null;

    public bool HasMesh
    {
        get
        {
            return verts != null && verts.Length > 0; 
        }
    }

    public bool HasMaterials
    {
        get
        {
            return materials != null && materials.Length > 0; 
        }
    } 

    public bool HasChildren
    {
        get
        {
            return children != null && children.Length > 0; 
        }
    }

    public List<string> Textures
    {
        get
        {
            Queue<BlenderObject> q = new Queue<BlenderObject>();
            q.Enqueue(this);

            List<string> textures = new List<string>(); 

            while(q.Count > 0)
            {
                var bo = q.Dequeue();

                if (bo.HasMaterials)
                {
                    foreach(var mat in bo.materials)
                    {
                        if(mat.imageName != null && mat.imageName.Length > 0)
                        {
                            textures.Add(mat.imageName); 
                        }
                    }
                }

                if (bo.HasChildren)
                {
                    foreach(var child in bo.children)
                    {
                        q.Enqueue(child); 
                    }
                }
            }

            return textures; 
        }
    }

    #region creation methods 

    public static Matrix4x4 CorrectTransformationMatrix(Matrix4x4 matrix)
    {
        return Matrix4x4.Scale(new Vector3(.1f, .1f, .1f)) * matrix; 
    }

    public static BlenderObject CreateFromByteStream(BinaryReader br)
    {
        var bo = new BlenderObject();

        // name 
        bo.name = br.ReadString();
        // mode 
        bo.mode = br.ReadString();
        // transform (
        bo.matrix = Matrix4x4.identity;
        foreach (var i in Enumerable.Range(0, 16))
        {
            bo.matrix[i] = br.ReadSingle();
        }

        bool meshDataAvailable = br.ReadByte() > 0;

        if (meshDataAvailable)
        {
            // verts
            int vertsCount = br.ReadInt32();
            bo.verts = new Vector3[vertsCount / 3];
            int vertsIndex = 0;
            for (var i = 0; i < vertsCount; i += 3)
            {
                bo.verts[vertsIndex] = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                vertsIndex++;
            }
            // normals 
            int normalsCount = br.ReadInt32();
            bo.normals = new Vector3[normalsCount / 3];
            int normalIndex = 0;
            for (var i = 0; i < normalsCount; i += 3)
            {
                bo.normals[normalIndex] = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                normalIndex++;
            }
            // faces
            int facesCount = br.ReadInt32();
            bo.faces = new int[facesCount];
            int faceIndex = 0;
            for (var i = 0; i < facesCount; i += 1)
            {
                bo.faces[faceIndex] = br.ReadInt32();
                faceIndex++;
            }
            // uvs
            int uvsCount = br.ReadInt32();
            bo.uvs = null;
            if (uvsCount > 0)
            {
                bo.uvs = new Vector2[uvsCount / 2];
                int uvIdex = 0;
                for (var i = 0; i < uvsCount; i += 2)
                {
                    bo.uvs[uvIdex] = new Vector2(br.ReadSingle(), br.ReadSingle());
                    uvIdex++;
                }
            }
            // materials
            int materialsCount = br.ReadInt32();
            bo.materials = null;
            if (materialsCount > 0)
            {
                bo.materials = new BlenderObjectMaterial[materialsCount];
                for (int i = 0; i < materialsCount; i++)
                {
                    bo.materials[i] = BlenderObjectMaterial.CreateFromByteStream(br);
                }
            }
        }
        
        // chilren 
        int childrenCount = br.ReadInt32();
        bo.children = null; 
        if(childrenCount > 0)
        {
            bo.children = new BlenderObject[childrenCount];
            for (int i = 0; i < childrenCount; i++)
            {
                bo.children[i] = BlenderObject.CreateFromByteStream(br);
            }
        }
        
        return bo; 
    }

    static Vector3 CreateVector3(double x, double y, double z)
    {
        return new Vector3((float)x, (float)y, (float)z);
    }

    static Vector2 CreateVector2(double x, double y)
    {
        return new Vector2((float)x, (float)y);
    }

    #endregion 
}