using AssistantItemFinder.Common;
using System;
using System.Numerics;
using Windows.Perception.Spatial;

namespace AssistantItemFinder.Content
{
    /// <summary>
    /// Visual representation of a Node (aka Breadcrumb) 
    /// </summary>
    internal class Entity
    {
        public string Tag { get; private set; }

        public Node Node { get; set; }

        public Renderer Renderer { get; set; }

        public Vector3 Position { get; set; }

        public Vector3 EulerAngles { get; set; }

        public Vector3 Scale { get; set; }

        public bool Visible { get; set; }

        public bool Enabled { get; set; }

        private Matrix4x4 transform = Matrix4x4.Identity; 

        public Matrix4x4 Transform
        {
            get
            {                
                return transform; 
            }
        }

        public Entity(string tag, bool visible = true, bool enabled = true)
        {
            Tag = tag;

            Visible = visible;
            Enabled = enabled;

            Position = Vector3.Zero;
            EulerAngles = Vector3.Zero;
            Scale = Vector3.One;
        }

        public virtual void Update(StepTimer timer, SpatialCoordinateSystem referenceFrameCoordinateSystem)
        {
            if(!Enabled)
            {
                return; 
            }

            UpdateTransform(referenceFrameCoordinateSystem);      
        }

        public void UpdateTransform(SpatialCoordinateSystem referenceFrameCoordinateSystem)
        {
            if (!Enabled)
            {
                return;
            }

            var trans = Node.GetTransform(referenceFrameCoordinateSystem);
            if (trans.HasValue)
            {
                Matrix4x4 modelTranslation = Matrix4x4.CreateTranslation(Position);
                Matrix4x4 modelRotation = Matrix4x4.CreateFromYawPitchRoll(
                    DegreeToRadian(EulerAngles.Y),
                    DegreeToRadian(EulerAngles.X),
                    DegreeToRadian(EulerAngles.Z));
                Matrix4x4 modelScale = Matrix4x4.CreateScale(Scale);

                transform = (modelScale * modelRotation * modelTranslation) * trans.Value;
            }
        }

        public virtual void Render()
        {
            if (!Visible || Renderer == null)
            {
                return;
            }

            Renderer.UpdateModelTransform(Transform);                        
            Renderer.Render();
        }

        private float DegreeToRadian(double angle)
        {
            return (float)(Math.PI * angle / 180.0);
        }

        private float RadianToDegree(double angle)
        {
            return (float)(angle * (180.0 / Math.PI));
        }
    }
}
