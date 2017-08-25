using System.Numerics;
using System.Runtime.InteropServices;

namespace AssistantItemFinder.Content
{
    /// <summary>
    /// Constant buffer used to send hologram position transform to the shader pipeline.
    /// </summary>
    internal struct ModelConstantBuffer
    {
        public Matrix4x4 model;
    }

    /// <summary>
    /// Used to send per-vertex data to the vertex shader.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct VertexPositionNormalColor
    {
        public VertexPositionNormalColor(Vector3 pos, Vector3 normal, Vector3 color)
        {
            this.pos   = pos;
            this.normal = normal;
            this.color = color;
        }

        public Vector3 pos;
        public Vector3 normal;
        public Vector3 color;
    };
}
