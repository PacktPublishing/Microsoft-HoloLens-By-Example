using System.Numerics;

namespace AssistantItemFinder.Content
{
    /// <summary>
    /// Captured Frame from the FrameGrabber; includes information about the users 
    /// position and pose (position, forward, and up) and closest Node
    /// </summary>
    internal class Frame
    {
        public byte[] frameData;
        public Node node;
        public long timestamp;

        public Vector3 position;
        public Vector3 forward;
        public Vector3 up;

        /// <summary>
        /// A Frame is considered similar if they are assigned to the same Node and 
        /// facing direction is close 
        /// </summary>
        /// <param name="frame"></param>
        /// <returns></returns>
        public bool IsSimilar(Frame frame)
        {
            if(frame.node != node)
            {
                return false; 
            }

            var dot = Vector3.Dot(forward, frame.forward);
            return dot >= 0.5f; 
        }
    }
}
