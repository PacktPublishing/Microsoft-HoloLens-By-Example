using System;
using System.Numerics;
using AssistantItemFinder.Common;
using Windows.UI.Input.Spatial;

namespace AssistantItemFinder.Content
{
    internal abstract class Renderer : Disposer
    {
        // Cached reference to device resources.
        private DeviceResources deviceResources;

        // Direct3D resources for cube geometry.
        private SharpDX.Direct3D11.InputLayout inputLayout;
        private SharpDX.Direct3D11.Buffer vertexBuffer;
        private SharpDX.Direct3D11.Buffer indexBuffer;
        private SharpDX.Direct3D11.VertexShader vertexShader;
        private SharpDX.Direct3D11.GeometryShader geometryShader;
        private SharpDX.Direct3D11.PixelShader pixelShader;
        private SharpDX.Direct3D11.Buffer modelConstantBuffer;

        // System resources for cube geometry.
        private ModelConstantBuffer modelConstantBufferData;
        private int indexCount = 0;

        // Variables used with the rendering loop.
        private bool loadingComplete = false;
        private float degreesPerSecond = 45.0f;


        // If the current D3D Device supports VPRT, we can avoid using a geometry
        // shader just to set the render target array index.
        private bool usingVprtShaders = false;

        /// <summary>
        /// Loads vertex and pixel shaders from files and instantiates the cube geometry.
        /// </summary>
        public Renderer(DeviceResources deviceResources)
        {
            this.deviceResources = deviceResources;            
        }

        public abstract GeometricPrimitives.GeometricData GetGeometricData(); 

        /// <summary>
        /// Called once per frame, rotates the cube and calculates the model and view matrices.
        /// </summary>
        public virtual void UpdateModelTransform(Matrix4x4 modelTransform)
        {
            // Loading is asynchronous. Resources must be created before they can be updated.
            if (!loadingComplete)
            {
                return;
            }

            // The view and projection matrices are provided by the system; they are associated
            // with holographic cameras, and updated on a per-camera basis.
            // Here, we provide the model transform for the sample hologram. The model transform
            // matrix is transposed to prepare it for the shader.
            this.modelConstantBufferData.model = Matrix4x4.Transpose(modelTransform);            

            // Use the D3D device context to update Direct3D device-based resources.
            var context = this.deviceResources.D3DDeviceContext;

            // Update the model transform buffer for the hologram.
            context.UpdateSubresource(ref this.modelConstantBufferData, this.modelConstantBuffer);
        }

        /// <summary>
        /// Renders one frame using the vertex and pixel shaders.
        /// On devices that do not support the D3D11_FEATURE_D3D11_OPTIONS3::
        /// VPAndRTArrayIndexFromAnyShaderFeedingRasterizer optional feature,
        /// a pass-through geometry shader is also used to set the render 
        /// target array index.
        /// </summary>
        public virtual  void Render()
        {
            // Loading is asynchronous. Resources must be created before drawing can occur.
            if (!this.loadingComplete)
            {
                return;
            }

            var context = this.deviceResources.D3DDeviceContext;

            // Each vertex is one instance of the VertexPositionColor struct.
            int stride = SharpDX.Utilities.SizeOf<VertexPositionNormalColor>();
            int offset = 0;
            var bufferBinding = new SharpDX.Direct3D11.VertexBufferBinding(this.vertexBuffer, stride, offset);
            context.InputAssembler.SetVertexBuffers(0, bufferBinding);
            context.InputAssembler.SetIndexBuffer(
                this.indexBuffer,
                SharpDX.DXGI.Format.R32_UInt, // Each index is one 16-bit unsigned integer (short).
                0);
            context.InputAssembler.PrimitiveTopology = SharpDX.Direct3D.PrimitiveTopology.TriangleList;
            context.InputAssembler.InputLayout = this.inputLayout;

            // Attach the vertex shader.
            context.VertexShader.SetShader(this.vertexShader, null, 0);
            // Apply the model constant buffer to the vertex shader.
            context.VertexShader.SetConstantBuffers(0, this.modelConstantBuffer);

            if (!this.usingVprtShaders)
            {
                // On devices that do not support the D3D11_FEATURE_D3D11_OPTIONS3::
                // VPAndRTArrayIndexFromAnyShaderFeedingRasterizer optional feature,
                // a pass-through geometry shader is used to set the render target 
                // array index.
                context.GeometryShader.SetShader(this.geometryShader, null, 0);
            }

            // Attach the pixel shader.
            context.PixelShader.SetShader(this.pixelShader, null, 0);

            // Draw the objects.
            context.DrawIndexedInstanced(
                indexCount,     // Index count per instance.
                2,              // Instance count.
                0,              // Start index location.
                0,              // Base vertex location.
                0               // Start instance location.
                );
        }

        /// <summary>
        /// Creates device-based resources to store a constant buffer, cube
        /// geometry, and vertex and pixel shaders. In some cases this will also 
        /// store a geometry shader.
        /// </summary>
        public virtual async void CreateDeviceDependentResourcesAsync()
        {
            ReleaseDeviceDependentResources();

            usingVprtShaders = deviceResources.D3DDeviceSupportsVprt;

            var folder = Windows.ApplicationModel.Package.Current.InstalledLocation;

            // On devices that do support the D3D11_FEATURE_D3D11_OPTIONS3::
            // VPAndRTArrayIndexFromAnyShaderFeedingRasterizer optional feature
            // we can avoid using a pass-through geometry shader to set the render
            // target array index, thus avoiding any overhead that would be 
            // incurred by setting the geometry shader stage.
            var vertexShaderFileName = usingVprtShaders ? "Content\\Shaders\\VPRTVertexShader.cso" : "Content\\Shaders\\VertexShader.cso";

            // Load the compiled vertex shader.
            var vertexShaderByteCode = await DirectXHelper.ReadDataAsync(await folder.GetFileAsync(vertexShaderFileName));

            // After the vertex shader file is loaded, create the shader and input layout.
            vertexShader = this.ToDispose(new SharpDX.Direct3D11.VertexShader(
                deviceResources.D3DDevice,
                vertexShaderByteCode));

            SharpDX.Direct3D11.InputElement[] vertexDesc =
            {
                new SharpDX.Direct3D11.InputElement("POSITION", 0, SharpDX.DXGI.Format.R32G32B32_Float,  0, 0, SharpDX.Direct3D11.InputClassification.PerVertexData, 0),
                new SharpDX.Direct3D11.InputElement("NORMAL",    0, SharpDX.DXGI.Format.R32G32B32_Float, 12, 0, SharpDX.Direct3D11.InputClassification.PerVertexData, 0),
                new SharpDX.Direct3D11.InputElement("COLOR",    0, SharpDX.DXGI.Format.R32G32B32_Float, 24, 0, SharpDX.Direct3D11.InputClassification.PerVertexData, 0)
            };

            inputLayout = this.ToDispose(new SharpDX.Direct3D11.InputLayout(
                deviceResources.D3DDevice,
                vertexShaderByteCode,
                vertexDesc));

            if (!usingVprtShaders)
            {
                // Load the compiled pass-through geometry shader.
                var geometryShaderByteCode = await DirectXHelper.ReadDataAsync(await folder.GetFileAsync("Content\\Shaders\\GeometryShader.cso"));

                // After the pass-through geometry shader file is loaded, create the shader.
                geometryShader = this.ToDispose(new SharpDX.Direct3D11.GeometryShader(
                    deviceResources.D3DDevice,
                    geometryShaderByteCode));
            }

            // Load the compiled pixel shader.
            var pixelShaderByteCode = await DirectXHelper.ReadDataAsync(await folder.GetFileAsync("Content\\Shaders\\PixelShader.cso"));

            // After the pixel shader file is loaded, create the shader.
            pixelShader = this.ToDispose(new SharpDX.Direct3D11.PixelShader(
                deviceResources.D3DDevice,
                pixelShaderByteCode));            
            
            GeometricPrimitives.GeometricData geometricData = GetGeometricData();

            VertexPositionNormalColor[] vertices = new VertexPositionNormalColor[geometricData.VertexCount];
            for(var i=0; i< geometricData.VertexCount; i++)
            {                
                vertices[i] = new VertexPositionNormalColor(geometricData.vertices[i], geometricData.normals[i], geometricData.colors[i]);
            }
            int[] indices = geometricData.indices;

            indexCount = indices.Length;

            vertexBuffer = this.ToDispose(SharpDX.Direct3D11.Buffer.Create(
                deviceResources.D3DDevice,
                SharpDX.Direct3D11.BindFlags.VertexBuffer,
                vertices));

            // Load mesh indices. Each trio of indices represents
            // a triangle to be rendered on the screen.
            // For example: 0,2,1 means that the vertices with indexes
            // 0, 2 and 1 from the vertex buffer compose the 
            // first triangle of this mesh.

            indexBuffer = this.ToDispose(SharpDX.Direct3D11.Buffer.Create(
                deviceResources.D3DDevice,
                SharpDX.Direct3D11.BindFlags.IndexBuffer,
                indices));

            // Create a constant buffer to store the model matrix.
            modelConstantBuffer = this.ToDispose(SharpDX.Direct3D11.Buffer.Create(
                deviceResources.D3DDevice,
                SharpDX.Direct3D11.BindFlags.ConstantBuffer,
                ref modelConstantBufferData));

            // Once the cube is loaded, the object is ready to be rendered.
            loadingComplete = true;
        }

        /// <summary>
        /// Releases device-based resources.
        /// </summary>
        public void ReleaseDeviceDependentResources()
        {
            loadingComplete = false;
            usingVprtShaders = false;

            this.RemoveAndDispose(ref vertexShader);
            this.RemoveAndDispose(ref inputLayout);
            this.RemoveAndDispose(ref pixelShader);
            this.RemoveAndDispose(ref geometryShader);
            this.RemoveAndDispose(ref modelConstantBuffer);
            this.RemoveAndDispose(ref vertexBuffer);
            this.RemoveAndDispose(ref indexBuffer);
        }
    }
}
