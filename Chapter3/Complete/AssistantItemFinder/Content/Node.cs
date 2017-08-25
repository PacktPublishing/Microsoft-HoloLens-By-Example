using System.Collections.Generic;
using System.Numerics;
using Windows.Perception.Spatial;

namespace AssistantItemFinder.Content
{
    /// <summary>
    /// Our 'breadcrumbs' - Nodes are 'dropping' as the user walks around and used 
    /// to construct a path to an item 
    /// </summary>
    internal class Node
    {
        static int NodeID = 0;

        public string Name { get; private set; }

        /// <summary>
        /// Position relative to the Anchor 
        /// </summary>
        public Vector3 Position { get; set; }

        /// <summary>
        /// Gaze forward direction 
        /// </summary>
        public Vector3 Forward { get; set; }

        public long Timestamp { get; set; }

        /// <summary>
        /// Associated SpatialAnchor 
        /// </summary>
        public SpatialAnchor Anchor { get; set; }

        /// <summary>
        /// Sighting around this node 
        /// </summary>
        public List<Sighting> Sightings = new List<Sighting>();

        /// <summary>
        /// Anchor and associated Pose (ie Pose created using the SpatialAnchors Coordinate System)
        /// </summary>
        /// <param name="anchor"></param>
        /// <param name="pose"></param>
        public Node(Spatial​Anchor anchor, Vector3 position, Vector3 forward)
        {
            Name = $"Node_{++Node.NodeID}";
            Anchor = anchor;
            Position = position;
            Forward = forward; 
            Timestamp = Utils.GetCurrentUnixTimestampMillis();
        }

        /// <summary>
        /// Get a transformation matrix that can be used to transform from the coordinate system 
        /// of the referenced anchor to the target coordinate system (taking into account the position) 
        /// </summary>
        /// <param name="targetCoordinateSystem"></param>
        /// <returns></returns>
        public Matrix4x4? GetTransform(SpatialCoordinateSystem targetCoordinateSystem)
        {
            var mat = Anchor.CoordinateSystem.TryGetTransformTo(targetCoordinateSystem);
            if (!mat.HasValue)
            {
                return null;
            }

            Matrix4x4 modelTranslation = Matrix4x4.CreateTranslation(Position);

            return (modelTranslation) * mat.Value;
        }

        /// <summary>
        /// Transform the Nodes position in the target coordinate systems space 
        /// </summary>
        /// <param name="targetCoordinateSystem"></param>
        /// <returns></returns>
        public Vector3? TryGetTransformedPosition(SpatialCoordinateSystem targetCoordinateSystem)
        {
            if (targetCoordinateSystem == Anchor.CoordinateSystem)
            {
                return Position;
            }

            var trans = Anchor.CoordinateSystem.TryGetTransformTo(targetCoordinateSystem);
            if (trans.HasValue)
            {
                return Vector3.Transform(Position, trans.Value);
            }

            return null;
        }

        /// <summary>
        /// Get a distance between 2 points taking into account the different coordiante systems 
        /// </summary>
        /// <param name="targetCoordinateSystem"></param>
        /// <param name="targetPosition"></param>
        /// <returns></returns>
        public float? TryGetDistance(SpatialCoordinateSystem targetCoordinateSystem, Vector3 targetPosition)
        {
            if (targetCoordinateSystem == Anchor.CoordinateSystem)
            {
                return (targetPosition - Position).Length();
            }

            var trans = Anchor.CoordinateSystem.TryGetTransformTo(targetCoordinateSystem);
            if (trans.HasValue)
            {
                return (targetPosition - Vector3.Transform(Position, trans.Value)).Length();
            }

            return null;
        }

        public void AddSighting(Sighting sighting)
        {
            Sightings.Add(sighting);
        }

        public override string ToString()
        {
            return Name;
        }
    }
}