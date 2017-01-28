using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TaggingTheWorld.Common;
using Windows.Foundation.Collections;
// add namespaces 
using Windows.Media.Capture;
using Windows.Media.Effects;
using Windows.Media.MediaProperties;
using Windows.Storage.Streams;

namespace TaggingTheWorld
{
    public class CameraProcessor
    {
        CancellationTokenSource cancellationTokenSource;
        MediaCapture mediaCapture;

        public CameraProcessor()
        {
            InitMediaCapture();             
        }        

        public void StartCapturing()
        {
            if (cancellationTokenSource == null)
            {
                cancellationTokenSource = new CancellationTokenSource();
                Task.Factory.StartNew(() => CaptureLoop(cancellationTokenSource.Token), cancellationTokenSource.Token);
            }
        }

        public void StopCapturing()
        {
            if (cancellationTokenSource != null)
            {
                cancellationTokenSource.Cancel();
                cancellationTokenSource = null;
            }
        }

        async void CaptureLoop(CancellationToken token)
        {
            await InitMediaCapture(); 

            while (true)
            {
                token.ThrowIfCancellationRequested();
                Thread.Sleep(1000);
                Debug.WriteLine("working...");
            }

            var imgFormat = ImageEncodingProperties.CreateJpeg();

            // Capture a frame and put it to MemoryStream
            var memoryStream = new MemoryStream();
            using (var ras = new InMemoryRandomAccessStream())
            {
                await mediaCapture.CapturePhotoToStreamAsync(imgFormat, ras);
                ras.Seek(0);
                using (var stream = ras.AsStreamForRead())
                {
                    stream.CopyTo(memoryStream);

                }
            }

            var imageBytes = memoryStream.ToArray();
            memoryStream.Position = 0;

            try
            {
                
            }           
            catch (Exception exc)
            {
                return "Failed";
            }

            return "";
        }

        async Task<int> InitMediaCapture()
        {
            mediaCapture = new MediaCapture();
            await mediaCapture.InitializeAsync();
            await mediaCapture.AddVideoEffectAsync(new VideoEffectDefinition(), MediaStreamType.Photo);

            //await mediaCapture.InitializeAsync();
            return 1; 
        }
    }

    public class VideoEffectDefinition : IVideoEffectDefinition
    {
        public string ActivatableClassId => "Windows.Media.MixedRealityCapture.MixedRealityCaptureVideoEffect";

        public IPropertySet Properties { get; }

        IPropertySet IVideoEffectDefinition.Properties
        {
            get
            {
                return new PropertySet
                {
                    {"HologramCompositionEnabled", false},
                    {"RecordingIndicatorEnabled", false},
                    {"VideoStabilizationEnabled", false},
                    {"VideoStabilizationBufferLength", 0},
                    {"GlobalOpacityCoefficient", 0.9f},
                    {"StreamType", (int)MediaStreamType.Photo}
                };
            }
        }        
    }
}
