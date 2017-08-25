using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace AssistantItemFinder.Content
{
    /// <summary>
    /// Connects Nodes which is the basis for our path construction 
    /// </summary>
    internal class Edge
    {
        public Node NodeA { get; set; }

        public Node NodeB { get; set; }

        public Vector3 Direction
        {
            get
            {
                if (NodeA.Anchor == NodeB.Anchor)
                {
                    return Vector3.Normalize(NodeB.Position - NodeA.Position);
                }
                else
                {
                    var nodeBToBTrans = NodeB.Anchor.CoordinateSystem.TryGetTransformTo(NodeA.Anchor.CoordinateSystem);
                    if (nodeBToBTrans.HasValue)
                    {
                        // get NodeB's position in NodeA's space 
                        var nodeBPosition = Vector3.Transform(NodeB.Position, nodeBToBTrans.Value);
                        return Vector3.Normalize(nodeBPosition - NodeA.Position);
                    }
                }

                return Vector3.Zero;
            }
        }

        public float Distance
        {
            get
            {
                if (NodeA.Anchor == NodeB.Anchor)
                {
                    return (NodeB.Position - NodeA.Position).Length();
                }
                else
                {
                    var nodeBToBTrans = NodeB.Anchor.CoordinateSystem.TryGetTransformTo(NodeA.Anchor.CoordinateSystem);
                    if (nodeBToBTrans.HasValue)
                    {
                        // get NodeB's position in NodeA's space 
                        var nodeBPosition = Vector3.Transform(NodeB.Position, nodeBToBTrans.Value);
                        return (nodeBPosition - NodeA.Position).Length();
                    }
                }

                return 0f;
            }
        }

        public override string ToString()
        {
            return $"Edge({NodeA.Name} -> {NodeB.Name})";
        }
    }
}