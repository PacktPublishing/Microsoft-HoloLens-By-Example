using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace AssistantItemFinder.Content
{
    internal static class ObjParser
    {
        public delegate void ObjParsingComplete(Vector3[] vertices, Vector3[] normals, Vector2[] uvs, Vector3[] colors, int[] indices);

        private struct Face
        {
            public int vIndex;
            public int uvIndex;
            public int nIndex; 
            
            public static Face Create(string linePart)
            {
                string[] parts = linePart.Split('/');

                return new Face
                {
                    vIndex = int.Parse(parts[0]),
                    uvIndex = parts.Length > 1 && parts[1] != null && parts[1].Length > 0 ? int.Parse(parts[1]) : -1,
                    nIndex = parts.Length > 2 && parts[2] != null && parts[2].Length > 0 ? int.Parse(parts[2]) : -1,
                };
            }           
        }

        public static async Task LoadAsync(string objFilename, string mtlFilename, ObjParsingComplete handler)
        {
            await Task.Factory.StartNew(() =>
             {
                 List<Vector3> verts = new List<Vector3>();
                 List<Vector3> normals = new List<Vector3>();
                 List<Vector2> texCoords = new List<Vector2>();
                 List<Face> faces = new List<Face>();                  

                 foreach(var line in File.ReadAllLines(objFilename))
                 {
                     ParseObjLine(line, verts, normals, texCoords, faces);
                 }

                 Vector3 diffuseColor = new Vector3(1.0f, 0f, 0f);

                 if(mtlFilename != null && mtlFilename.Length > 0)
                 {
                     foreach (var line in File.ReadAllLines(mtlFilename))
                     {
                         ParseMtlLine(line, ref diffuseColor);
                     }
                 }

                 int maxCount = Math.Max(Math.Max(verts.Count, normals.Count), texCoords.Count);

                 Vector3[] expandedVertices = new Vector3[maxCount];
                 Vector3[] expandedNormals = new Vector3[maxCount];
                 Vector2[] expandedUVs = new Vector2[maxCount];
                 Vector3[] expandedColors = new Vector3[maxCount];
                 int[] indices = new int[faces.Count];

                 int count = 0; 

                 foreach (var face in faces)
                 {
                     int index = face.vIndex - 1;

                     var vert = verts[face.vIndex - 1];
                     var normal = face.nIndex > 0 ? normals[face.nIndex-1] : new Vector3(0, 0, 0);
                     var uv = face.uvIndex > 0 ? texCoords[face.uvIndex - 1] : new Vector2(0, 0);
                     var color = diffuseColor;

                     expandedVertices[index] = vert;
                     expandedNormals[index] = normal;
                     expandedUVs[index] = uv;
                     expandedColors[index] = color;
                     indices[count] = index;

                     count++;                       
                 }

                 if(handler != null)
                 {
                     handler(expandedVertices, expandedNormals, expandedUVs, expandedColors, indices);
                 }                 
             });
        }

        static void ParseMtlLine(string line, ref Vector3 diffuseColor)
        {
            string[] parts = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length > 0)
            {
                switch (parts[0])
                {
                    case "Kd":
                        diffuseColor.X = float.Parse(parts[1]);
                        diffuseColor.Y = float.Parse(parts[2]);
                        diffuseColor.Z = float.Parse(parts[3]);
                        break;                    
                }
            }
        }

        static void ParseObjLine(string line, List<Vector3> verts, List<Vector3> normals, List<Vector2> texCoords, List<Face> faces)
        {
            string[] parts = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0)
            {
                switch (parts[0])
                {
                    case "usemtl":
                        // ignore 
                        break;
                    case "mtllib":
                        // ignore 
                        break;
                    case "v":
                        verts.Add(new Vector3(float.Parse(parts[1]), float.Parse(parts[2]), float.Parse(parts[3])));
                        break;
                    case "vn":
                        normals.Add(new Vector3(float.Parse(parts[1]), float.Parse(parts[2]), float.Parse(parts[3])));
                        break;
                    case "vt":
                        texCoords.Add(new Vector2(float.Parse(parts[1]), float.Parse(parts[2])));
                        break;
                    case "f":
                        faces.Add(Face.Create(parts[1]));
                        faces.Add(Face.Create(parts[2]));
                        faces.Add(Face.Create(parts[3]));
                        break;                    
                }
            }
        }
    }
}
