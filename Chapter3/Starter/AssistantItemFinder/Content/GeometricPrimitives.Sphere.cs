/// Original from https://github.com/sharpdx/Toolkit/blob/master/Source/Toolkit/SharpDX.Toolkit.Graphics/GeometricPrimitive.Sphere.cs 
/// Bug fix from Direct3D Rendering Cookbook by Justin Stenning (https://www.packtpub.com/game-development/direct3d-rendering-cookbook) 
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
        /// <summary>
        /// A sphere primitive.
        /// </summary>
        public struct Sphere
        {
            /// <summary>
            /// Creates a sphere primitive.
            /// </summary>
            /// <param name="device">The device.</param>
            /// <param name="diameter">The diameter.</param>
            /// <param name="tessellation">The tessellation.</param>
            /// <param name="toLeftHanded">if set to <c>true</c> vertices and indices will be transformed to left handed. Default is true.</param>
            /// <returns>A sphere primitive.</returns>
            /// <exception cref="System.ArgumentOutOfRangeException">tessellation;Must be >= 3</exception>
            public static GeometricData Create(Vector3 color, float diameter = 0.2f, int tessellation = 16, bool clockWiseWinding = true)
            {
                if (tessellation < 3) throw new ArgumentOutOfRangeException("tessellation", "Must be >= 3");

                var geometricData = new GeometricData();

                int verticalSegments = tessellation;
                int horizontalSegments = tessellation * 2;

                int vertexCount = (verticalSegments + 1) * (horizontalSegments + 1);
                int indiciesCount = (verticalSegments) * (horizontalSegments + 1) * 6;

                geometricData.vertices = new Vector3[vertexCount];
                geometricData.normals = new Vector3[vertexCount];
                geometricData.colors = new Vector3[vertexCount];
                geometricData.uvs = new Vector2[vertexCount];

                geometricData.indices = new int[indiciesCount];

                float radius = diameter / 2;

                int vertexIndex = 0;
                // Create rings of vertices at progressively higher latitudes.
                for (int i = 0; i <= verticalSegments; i++)
                {
                    float v = 1.0f - (float)i / verticalSegments;

                    var latitude = (float)((i * Math.PI / verticalSegments) - Math.PI / 2.0);
                    var dy = (float)Math.Sin(latitude);
                    var dxz = (float)Math.Cos(latitude);

                    // Create a single ring of vertices at this latitude.
                    for (int j = 0; j <= horizontalSegments; j++)
                    {
                        float u = (float)j / horizontalSegments;

                        var longitude = (float)(j * 2.0 * Math.PI / horizontalSegments);
                        var dx = (float)Math.Sin(longitude);
                        var dz = (float)Math.Cos(longitude);

                        dx *= dxz;
                        dz *= dxz;

                        var normal = new Vector3(dx, dy, dz);
                        var position = normal * radius;
                        var textureCoordinate = new Vector2(u, v);                        

                        geometricData.vertices[vertexIndex] = position;
                        geometricData.normals[vertexIndex] = normal;
                        geometricData.colors[vertexIndex] = color; 
                        geometricData.uvs[vertexIndex] = textureCoordinate;

                        vertexIndex++;
                    }                    
                }

                // Fill the index buffer with triangles joining each pair of latitude rings.
                int stride = horizontalSegments + 1;

                int indexCount = 0;
                for (int i = 0; i < verticalSegments; i++)
                {
                    for (int j = 0; j <= horizontalSegments; j++)
                    {
                        int nextI = i + 1;
                        int nextJ = (j + 1) % stride;

                        geometricData.indices[indexCount++] = (i * stride + j);
                        // Implement correct winding of vertices
                        if (clockWiseWinding)
                        {
                            geometricData.indices[indexCount++] = (i * stride + nextJ);
                            geometricData.indices[indexCount++] = (nextI * stride + j);
                        }
                        else
                        {
                            geometricData.indices[indexCount++] = (nextI * stride + j);
                            geometricData.indices[indexCount++] = (i * stride + nextJ);
                        }

                        geometricData.indices[indexCount++] = (i * stride + nextJ);
                        // Implement correct winding of vertices
                        if (clockWiseWinding)
                        {
                            geometricData.indices[indexCount++] = (nextI * stride + nextJ);
                            geometricData.indices[indexCount++] = (nextI * stride + j);
                        }
                        else
                        {
                            geometricData.indices[indexCount++] = (nextI * stride + j);
                            geometricData.indices[indexCount++] = (nextI * stride + nextJ);
                        }
                    }
                }

                return geometricData; 
            }
        }
    }
}
