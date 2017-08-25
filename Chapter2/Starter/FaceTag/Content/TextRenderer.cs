using FaceTag.Common;
using SharpDX.Direct3D11;
using SharpDX.Mathematics.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace FaceTag.Content
{
    internal class TextRenderer : Disposer
    {
        // Cached reference to device resources.
        private DeviceResources deviceResources;
        
        private Texture2D texture2D;
        private ShaderResourceView shaderResourceView; 
        private SamplerState pointSampler; 
        private RenderTargetView renderTargetView; 
        private SharpDX.Direct2D1.RenderTarget d2dRenderTarget; 
        private SharpDX.Direct2D1.SolidColorBrush whiteBrush; 
        private SharpDX.DirectWrite.TextFormat textFormat;

        int textureWidth;
        int textureHeight; 

        public ShaderResourceView Texture
        {
            get
            {
                return shaderResourceView;
            }
        }

        public SharpDX.Direct3D11.SamplerState Sampler
        {
            get
            {
                return pointSampler;
            }
        }

        public TextRenderer(DeviceResources deviceResources, int textureWidth, int textureHeight)
        {
            this.deviceResources = deviceResources;
            this.textureWidth = textureWidth;
            this.textureHeight = textureHeight; 

            this.CreateDeviceDependentResources();
        }

        public void RenderTextOffscreen(string text){
            // Clear the off-screen render target.
            deviceResources.D3DDeviceContext.ClearRenderTargetView(renderTargetView, new RawColor4(0f, 0f, 0f, 0f));

            // Begin drawing with D2D.
            d2dRenderTarget.BeginDraw();

            // Create a text layout to match the screen.
            SharpDX.DirectWrite.TextLayout textLayout = new SharpDX.DirectWrite.TextLayout(deviceResources.DWriteFactory, text, textFormat, textureWidth, textureHeight);

            // Get the text metrics from the text layout.
            SharpDX.DirectWrite.TextMetrics metrics = textLayout.Metrics;

            // In this example, we position the text in the center of the off-screen render target.
            Matrix3x2 screenTranslation = Matrix3x2.CreateTranslation(
                textureWidth * 0.5f,
                textureHeight * 0.5f + metrics.Height * 0.5f
                );
            
            whiteBrush.Transform = screenTranslation.ToRawMatrix3x2();

            // Render the text using DirectWrite.
            d2dRenderTarget.DrawTextLayout(new RawVector2(), textLayout, whiteBrush); 

            // End drawing with D2D.
            d2dRenderTarget.EndDraw(); 
        }

        /// <summary>
        /// Creates device-based resources to store a constant buffer, cube
        /// geometry, and vertex and pixel shaders. In some cases this will also 
        /// store a geometry shader.
        /// </summary>
        public void CreateDeviceDependentResources()
        {
            ReleaseDeviceDependentResources();

            // Create a default sampler state, which will use point sampling.
            pointSampler = ToDispose(new SamplerState(deviceResources.D3DDevice, new SamplerStateDescription()
            {
                AddressU = TextureAddressMode.Clamp,
                AddressV = TextureAddressMode.Clamp,
                AddressW = TextureAddressMode.Clamp,
                BorderColor = new RawColor4(0, 0, 0, 0),
                ComparisonFunction = Comparison.Never,
                Filter = Filter.MinMagMipLinear,
                MaximumAnisotropy = 16,
                MaximumLod = float.MaxValue,
                MinimumLod = 0,
                MipLodBias = 0.0f
            }));

            // Create the texture that will be used as the offscreen render target.
            var textureDesc = new Texture2DDescription
            {
                Format = SharpDX.DXGI.Format.B8G8R8A8_UNorm,
                Width = textureWidth, 
                Height = textureHeight,
                MipLevels = 1,
                ArraySize = 1,
                BindFlags = BindFlags.ShaderResource | BindFlags.RenderTarget, 
                SampleDescription = new SharpDX.DXGI.SampleDescription(1, 0), 
                OptionFlags = ResourceOptionFlags.None, 
                Usage = ResourceUsage.Default, 
                CpuAccessFlags = CpuAccessFlags.None           
            };

            texture2D = new Texture2D(deviceResources.D3DDevice, textureDesc);         

            // Create read and write views for the offscreen render target.
            shaderResourceView = new ShaderResourceView(deviceResources.D3DDevice, texture2D);
            renderTargetView = new RenderTargetView(deviceResources.D3DDevice, texture2D);

            // In this example, we are using D2D and DirectWrite; so, we need to create a D2D render target as well.            
            SharpDX.Direct2D1.RenderTargetProperties props = new SharpDX.Direct2D1.RenderTargetProperties(
                SharpDX.Direct2D1.RenderTargetType.Default, 
                new SharpDX.Direct2D1.PixelFormat(SharpDX.DXGI.Format.Unknown, SharpDX.Direct2D1.AlphaMode.Premultiplied), 
                96, 96, 
                SharpDX.Direct2D1.RenderTargetUsage.None, 
                SharpDX.Direct2D1.FeatureLevel.Level_DEFAULT);

            // The DXGI surface is used to create the render target.
            SharpDX.DXGI.Surface dxgiSurface = texture2D.QueryInterface<SharpDX.DXGI.Surface>();
            d2dRenderTarget = new SharpDX.Direct2D1.RenderTarget(deviceResources.D2DFactory, dxgiSurface, props);

            // Create a solid color brush that will be used to render the text.
            whiteBrush = new SharpDX.Direct2D1.SolidColorBrush(d2dRenderTarget, new RawColor4(1f, 1f, 1f, 1f));

            // This is where we format the text that will be written on the render target.
            textFormat = new SharpDX.DirectWrite.TextFormat(deviceResources.DWriteFactory, "Consolas", SharpDX.DirectWrite.FontWeight.Normal, SharpDX.DirectWrite.FontStyle.Normal, 64f);
            textFormat.TextAlignment = SharpDX.DirectWrite.TextAlignment.Center;
            textFormat.ParagraphAlignment = SharpDX.DirectWrite.ParagraphAlignment.Center;
        }

        /// <summary>
        /// Releases device-based resources.
        /// </summary>
        public void ReleaseDeviceDependentResources()
        {
            RemoveAndDispose(ref texture2D);
            RemoveAndDispose(ref shaderResourceView);
            RemoveAndDispose(ref pointSampler);
            RemoveAndDispose(ref renderTargetView);
            RemoveAndDispose(ref whiteBrush);
            RemoveAndDispose(ref textFormat); 
        }
    }
}
