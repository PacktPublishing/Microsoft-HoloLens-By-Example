using System.Numerics;

namespace FaceTag.Content
{
    /// <summary>
    /// Constant buffer used to send hologram position transform to the shader pipeline.
    /// </summary>
    internal struct QuadModelConstantBuffer
    {
        public Matrix4x4 model; 
        public Vector2 texCoordScale;
        public Vector2 texCoordOffset;
    };

    /// <summary>
    /// Used to send per-vertex data to the vertex shader.
    /// </summary>
    struct VertexPositionTex
    {
        public Vector3 pos;
        public Vector2 tex;

        public VertexPositionTex(Vector3 pos, Vector2 tex)
        {
            this.pos = pos;
            this.tex = tex;
        }
    };
}
