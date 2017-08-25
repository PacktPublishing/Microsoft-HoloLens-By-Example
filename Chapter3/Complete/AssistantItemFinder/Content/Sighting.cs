using System.Collections.Generic;
using System.Numerics;

namespace AssistantItemFinder.Content
{
    /// <summary>
    /// Data Object to store 'sightings' at each node 
    /// </summary>
    internal class Sighting
    {
        /// <summary>
        /// Service provided description of the sighting 
        /// </summary>
        public string Description { get; private set; }

        /// <summary>
        /// Position relative to a SpatialAnchor 
        /// </summary>
        public Vector3 Position { get; private set; }
        
        /// <summary>
        /// Users Gaze direction 
        /// </summary>
        public Vector3 Forward { get; private set; }
        
        /// <summary>
        /// Up direction 
        /// </summary>
        public Vector3 Up { get; private set; }  
        
        /// <summary>
        /// When the Sighting occured
        /// </summary>
        public long Timestamp { get; private set; }             

        /// <summary>
        /// Description tokens i.e. recognised objects identified in the frame 
        /// </summary>
        public HashSet<string> Tokens = new HashSet<string>();  

        public Sighting(string description, Vector3 position, Vector3 forward, Vector3 up, params string[] tokens)
        {
            Description = description;

            Position = position;
            Forward = forward;
            Up = up;

            if (tokens != null)
            {
                foreach(var token in tokens)
                {
                    Tokens.Add(token); 
                }
            }

            Timestamp = Utils.GetCurrentUnixTimestampMillis();
        }     
        
        public Sighting AddToken(string token)
        {
            Tokens.Add(token);

            return this; 
        }   
    }
}
