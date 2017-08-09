using System;
using System.Numerics;
using AssistantItemFinder.Common;
using Windows.UI.Input.Spatial;
using System.Threading.Tasks;

namespace AssistantItemFinder.Content
{
    /// <summary>
    /// This sample renderer instantiates a basic rendering pipeline.
    /// </summary>
    internal class NodeRenderer : Renderer
    {
        public enum NodeType
        {
            Sphere,
            Arrow
        }

        public float Diameter { get; private set; }

        public int Tesselation { get; private set; }

        public Vector3 Color { get; private set; }

        public NodeType RenderType {  get; private set; }

        GeometricPrimitives.GeometricData? geometricData; 

        /// <summary>
        /// Loads vertex and pixel shaders from files and instantiates the cube geometry.
        /// </summary>
        public NodeRenderer(DeviceResources deviceResources, Vector3 color, float diameter = 0.05f, int tesselation = 16) : base(deviceResources)
        {
            Diameter = diameter;
            Tesselation = tesselation;
            Color = color;
            RenderType = NodeType.Sphere;
        }

        /// <summary>
        /// Loads vertex and pixel shaders from files and instantiates the cube geometry.
        /// </summary>
        public NodeRenderer(DeviceResources deviceResources) : base(deviceResources)
        {
            RenderType = NodeType.Arrow;
        }

        public override GeometricPrimitives.GeometricData GetGeometricData()
        {
            if(geometricData.HasValue)
            {
                return geometricData.Value; 
            }

            return GeometricPrimitives.Sphere.Create(Color, Diameter, Tesselation);
        }

        public override async void CreateDeviceDependentResourcesAsync()
        {
            if(RenderType == NodeType.Arrow)
            {
                await LoadModelFromDisk("arrow.obj", "arrow.mtl"); 
            }            

            base.CreateDeviceDependentResourcesAsync();
        }

        async Task LoadModelFromDisk(string objFilename, string mtlFilename)
        {
            var packageFolder = Windows.ApplicationModel.Package.Current.InstalledLocation;
            var objFile = await packageFolder.GetFileAsync(objFilename);
            var mtlFile = await packageFolder.GetFileAsync(mtlFilename);

            await ObjParser.LoadAsync(objFile.Path, mtlFile.Path, (verts, normals, uvs, colors, indices) =>
            {
                geometricData = new GeometricPrimitives.GeometricData
                {
                    vertices = verts,
                    normals = normals,
                    uvs = uvs,
                    colors = colors,
                    indices = indices
                };
            });
        }
    }
}
