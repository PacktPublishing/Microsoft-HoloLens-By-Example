using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace AssistantItemFinder.Content
{
    public partial class GeometricPrimitives
    {
        public struct GeometricData
        {
            public Vector3[] vertices;
            public Vector3[] normals;
            public Vector3[] colors;
            public Vector2[] uvs;

            public int[] indices;

            public int VertexCount
            {
                get
                {
                    return vertices != null ? vertices.Length : 0;
                }                
            }

            public int IndiciesCount
            {
                get
                {
                    return indices != null ? indices.Length : 0;
                }
            }
        }

    }
}
