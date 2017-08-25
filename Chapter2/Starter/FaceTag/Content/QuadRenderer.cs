using FaceTag.Common;
using SharpDX.Direct3D11;
using SharpDX.Mathematics.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Input.Spatial;

namespace FaceTag.Content
{
    internal class QuadRenderer : Disposer
    {
        #region properties and variables 

        // Cached reference to device resources.
        private DeviceResources deviceResources;

        // Direct3D resources for quad geometry.
        InputLayout inputLayout;
        SharpDX.Direct3D11.Buffer vertexBuffer;
        SharpDX.Direct3D11.Buffer indexBuffer;
        VertexShader vertexShader;
        GeometryShader geometryShader;
        PixelShader pixelShader;
        SharpDX.Direct3D11.Buffer modelConstantBuffer;

        // If the current D3D Device supports VPRT, we can avoid using a geometry
        // shader just to set the render target array index.
        private bool usingVprtShaders = false;

        // Direct3D resources for the default texture.
        SamplerState samplerState;

        // System resources for quad geometry.
        QuadModelConstantBuffer modelConstantBufferData;
        int indexCount = 0;

        public Vector3 Position { get; private set; }
        public Vector3 Velocity { get; private set; }


        public Vector3 Forward { get; set; }
        public Vector3 Up { get; set; }
        public Vector3 Right { get; set; }

        public Vector3 TargetPosition
        {
            get { return targetPosition; }
            set{ targetPosition = value; }
        }

        // Variables used with the rendering loop.
        Vector3 targetPosition = new Vector3(0f, 0f, -2f);
        Vector3 position = new Vector3(0f, 0f, -2f);
        Vector3 velocity = new Vector3(0f, 0f, 0f);

        Vector2 targetTexCoordScale = new Vector2(1.0f, 1.0f);
        Vector2 targetTexCoordOffset = new Vector2(0.0f, 0.0f);
        Vector2 texCoordScale = new Vector2(1.0f, 1.0f);
        Vector2 texCoordOffset = new Vector2(0.0f, 0.0f);

        // This is the rate at which the hologram position is interpolated (LERPed) to the current location.
        const float c_lerpRate = 6.0f;

        private bool _loadingComplete = false;
        
        public bool IsLoaded
        {
            get
            {
                lock (this) { return _loadingComplete; }
            }
            set
            {
                lock (this) { _loadingComplete = value; }
            }
        } 

        #endregion 

        public QuadRenderer(DeviceResources deviceResources)
        {
            this.deviceResources = deviceResources;

            Forward = new Vector3(0, 0, 1f);
            Up = new Vector3(0, 1f, 0);
            Right = new Vector3(1f, 0, 0); 

            this.CreateDeviceDependentResourcesAsync();
        }

        public void SetTexCoordScaleAndOffset(Vector2 texCoordScale, Vector2 texCoordOffset)
        {
            this.targetTexCoordScale = texCoordScale;
            this.targetTexCoordOffset = texCoordOffset; 
        }

        public void ResetTexCoordScaleAndOffset(Vector2 texCoordScale, Vector2 texCoordOffset)
        {
            this.texCoordScale = texCoordScale;
            this.texCoordOffset = texCoordOffset;
        }

        // Renders an RGB image onto the quad, only requires one texture..
        public void RenderRGB(ShaderResourceView rgbTexture)
        {
            if (!IsLoaded)
            {
                return; 
            }

            var context = deviceResources.D3DDeviceContext;

            // Set the RGB shader
            context.PixelShader.SetShader(pixelShader, null, 0);

            // Bind the RGB texture to the shader.
            context.PixelShader.SetShaderResource(0, rgbTexture);

            // Handle the rest of the rendering which is shared by RGB and NV12 rendering.
            RenderInternal();
        }       
        
        void RenderInternal()
        {
            var context = deviceResources.D3DDeviceContext;

            int stride = SharpDX.Utilities.SizeOf<VertexPositionTex>();
            int offset = 0;

            var bufferBinding = new VertexBufferBinding(this.vertexBuffer, stride, offset);

            context.InputAssembler.SetVertexBuffers(0, bufferBinding);
            context.InputAssembler.SetIndexBuffer(
                this.indexBuffer,
                SharpDX.DXGI.Format.R16_UInt, // Each index is one 16-bit unsigned integer (short).
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

            context.PixelShader.SetSamplers(0, 1, samplerState);

            // Draw the objects.
            context.DrawIndexedInstanced(
                indexCount, // Index count per instance.
                2, // Instance count.
                0, // Start index location.
                0, // Base vertex location.
                0  // Start instance location.
            );                                                   
        }

        // Repositions the sample hologram.
        public void Update(SpatialPointerPose pointerPose, StepTimer timer)
        {

            float deltaTime = (float)timer.ElapsedSeconds;
            float lerpDeltaTime = deltaTime * c_lerpRate;

            if (pointerPose != null)
            {
                // Get the gaze direction relative to the given coordinate system.
                var headPosition = pointerPose.Head.Position;
                var headForward = pointerPose.Head.ForwardDirection; 
                var headBack = -headForward;
                var headUp = pointerPose.Head.UpDirection;
                var headRight = Vector3.Cross(headForward, headUp);

                Forward = headForward;
                Up = headUp;
                Right = headRight;                 

                var prevPosition = position;
                position = Vector3.Lerp(position, targetPosition, lerpDeltaTime);

                velocity = (position - prevPosition) / deltaTime;

                texCoordScale = Vector2.Lerp(texCoordScale, targetTexCoordScale, lerpDeltaTime);
                texCoordOffset = Vector2.Lerp(texCoordOffset, targetTexCoordOffset, lerpDeltaTime);

                // Calculate our model to world matrix relative to the user's head.
                Matrix4x4 modelRotationTranslation = Matrix4x4.CreateWorld(position, Forward, Up);

                // Scale our 1m quad down to 20cm wide.
                Matrix4x4 modelScale = Matrix4x4.CreateScale(0.2f);

                Matrix4x4 modelTransform = modelScale * modelRotationTranslation;

                // The view and projection matrices are provided by the system; they are associated
                // with holographic cameras, and updated on a per-camera basis.
                // Here, we provide the model transform for the sample hologram. The model transform
                // matrix is transposed to prepare it for the shad(er.
                modelConstantBufferData.model = Matrix4x4.Transpose(modelTransform);
                modelConstantBufferData.texCoordScale = texCoordScale;
                modelConstantBufferData.texCoordOffset = texCoordOffset;

                // Use the D3D device context to update Direct3D device-based resources.
                var context = deviceResources.D3DDeviceContext;

                // Update the model transform buffer for the hologram.
                context.UpdateSubresource(ref this.modelConstantBufferData, this.modelConstantBuffer);                
            }
        }

        public async void CreateDeviceDependentResourcesAsync()
        {
            IsLoaded = false; 

            var folder = Windows.ApplicationModel.Package.Current.InstalledLocation;

            usingVprtShaders = deviceResources.D3DDeviceSupportsVprt;

            string vertexShaderFileName = usingVprtShaders ? "Content\\Shaders\\QuadVPRTVertexShader.cso" : "Content\\Shaders\\QuadVertexShader.cso";
            
            // Load the compiled vertex shader.
            var vertexShaderByteCode = await DirectXHelper.ReadDataAsync(await folder.GetFileAsync(vertexShaderFileName));

            // After the vertex shader file is loaded, create the shader and input layout.
            vertexShader = this.ToDispose(new VertexShader(deviceResources.D3DDevice,vertexShaderByteCode));

            InputElement[] vertexDesc =
            {
                new InputElement("POSITION", 0, SharpDX.DXGI.Format.R32G32B32_Float,  0, 0, InputClassification.PerVertexData, 0),
                new InputElement("TEXCOORD",    0, SharpDX.DXGI.Format.R32G32_Float, 12, 0, InputClassification.PerVertexData, 0),
            };

            inputLayout = this.ToDispose(new InputLayout(deviceResources.D3DDevice,vertexShaderByteCode, vertexDesc));

            if (!usingVprtShaders)
            {
                // Load the compiled pass-through geometry shader.
                var geometryShaderByteCode = await DirectXHelper.ReadDataAsync(await folder.GetFileAsync("Content\\Shaders\\QuadGeometryShader.cso"));

                // After the pass-through geometry shader file is loaded, create the shader.
                geometryShader = this.ToDispose(new GeometryShader(deviceResources.D3DDevice,geometryShaderByteCode));
            }

            // Load the compiled pixel shader.
            var pixelShaderByteCode = await DirectXHelper.ReadDataAsync(await folder.GetFileAsync("Content\\Shaders\\QuadPixelShaderRGB.cso"));

            // After the pixel shader file is loaded, create the shader.
            pixelShader = this.ToDispose(new PixelShader(deviceResources.D3DDevice,pixelShaderByteCode));

            // Load mesh vertices. Each vertex has a position and a color.
            // Note that the quad size has changed from the default DirectX app
            // template. Windows Holographic is scaled in meters, so to draw the
            // quad at a comfortable size we made the quad width 0.2 m (20 cm).
            VertexPositionTex[] quadVertices = new[]
            {
                new VertexPositionTex(new Vector3(-0.5f,  0.5f, 0f), new Vector2(0f, 0f)),
                new VertexPositionTex(new Vector3(0.5f,  0.5f, 0f), new Vector2(1f, 0f)),
                new VertexPositionTex(new Vector3(0.5f, -0.5f, 0f), new Vector2(1f, 1f)),
                new VertexPositionTex(new Vector3(-0.5f, -0.5f, 0f), new Vector2(0f, 1f))
            };

            vertexBuffer = this.ToDispose(SharpDX.Direct3D11.Buffer.Create(deviceResources.D3DDevice, BindFlags.VertexBuffer, quadVertices));

            // Load mesh indices. Each trio of indices represents
            // a triangle to be rendered on the screen.
            // For example: 2,1,0 means that the vertices with indexes
            // 2, 1, and 0 from the vertex buffer compose the
            // first triangle of this mesh.
            // Note that the winding order is clockwise by default.
            ushort[] quadIndices =
            {
                // -z
                0,2,3,
                0,1,2,
                // +z
                2,0,3,
                1,0,2,
            };

            indexCount = quadIndices.Length;
            indexBuffer = this.ToDispose(SharpDX.Direct3D11.Buffer.Create(deviceResources.D3DDevice,BindFlags.IndexBuffer, quadIndices));

            samplerState = ToDispose(new SamplerState(deviceResources.D3DDevice, new SamplerStateDescription()
            {
                AddressU = TextureAddressMode.Wrap,
                AddressV = TextureAddressMode.Wrap,
                AddressW = TextureAddressMode.Wrap,
                BorderColor = new RawColor4(0, 0, 0, 0),
                ComparisonFunction = Comparison.Never,
                Filter = Filter.MinMagMipLinear,
                MaximumAnisotropy = 16,
                MaximumLod = float.MaxValue,
                MinimumLod = 0,
                MipLodBias = 0.0f
            }));

            // Create a constant buffer to store the model matrix.
            modelConstantBuffer = this.ToDispose(SharpDX.Direct3D11.Buffer.Create(deviceResources.D3DDevice, BindFlags.ConstantBuffer, ref modelConstantBufferData));

            IsLoaded = true; 
        }

        public void ReleaseDeviceDependentResources()
        {
            IsLoaded = false;
            usingVprtShaders = false;

            this.RemoveAndDispose(ref vertexShader);
            this.RemoveAndDispose(ref inputLayout);
            this.RemoveAndDispose(ref pixelShader);
            this.RemoveAndDispose(ref geometryShader);
            this.RemoveAndDispose(ref modelConstantBuffer);
            this.RemoveAndDispose(ref vertexBuffer);
            this.RemoveAndDispose(ref indexBuffer);
            this.RemoveAndDispose(ref samplerState);
        }
    }
}
