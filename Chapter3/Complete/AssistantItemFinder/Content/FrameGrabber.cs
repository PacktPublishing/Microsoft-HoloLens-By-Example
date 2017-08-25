using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using System.Numerics;
using Windows.Media;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using System.IO;

namespace AssistantItemFinder.Content
{
    internal class FrameGrabber
    {
        public interface IFrameGrabberDataSource
        {
            Node CurrentNode { get; }

            Vector3 NodePosition { get; }

            Vector3 GazeForward { get; }

            Vector3 GazeUp { get; }
        }

        #region properties and variables 

        IFrameGrabberDataSource datasource; 

        MediaCapture mediaCapture;
        MediaFrameSource mediaFrameSource;
        MediaFrameReader mediaFrameReader;

        private bool _analyzingFrame = false; 

        public bool IsAnalyzingFrame
        {
            get
            {
                lock (this)
                {
                    return _analyzingFrame; 
                }
            }
            set
            {
                lock (this)
                {
                    _analyzingFrame = value; 
                }
            }
        }

        private bool _isNewFrameAvailable = false; 

        public bool IsNewFrameAvailable
        {
            get
            {
                return _isNewFrameAvailable;
            }
            private set
            {
                lock (this)
                {
                    _isNewFrameAvailable = value; 
                }
            }
        }

        private Frame _currentFrame;

        public Frame CurrentFrame
        {
            get
            {
                lock (this)
                {
                    IsNewFrameAvailable = false; 
                    return _currentFrame;
                }
            }
            private set
            {
                lock (this)
                {
                    IsNewFrameAvailable = true; 
                    _currentFrame = value;
                }
            }
        }

        #endregion 

        private FrameGrabber(IFrameGrabberDataSource datasource = null, MediaCapture mediaCapture = null, MediaFrameSource mediaFrameSource = null, MediaFrameReader mediaFrameReader = null)
        {
            this.datasource = datasource;
            this.mediaCapture = mediaCapture;
            this.mediaFrameSource = mediaFrameSource;
            this.mediaFrameReader = mediaFrameReader;

            if (this.mediaFrameReader != null)
            {
                this.mediaFrameReader.FrameArrived += MediaFrameReader_FrameArrived;
            }
        }

        #region factory methods 

        public static async Task<FrameGrabber> CreateAsync(IFrameGrabberDataSource datasource)
        {
            MediaCapture mediaCapture = null;
            MediaFrameReader mediaFrameReader = null;

            MediaFrameSourceGroup selectedGroup = null;
            MediaFrameSourceInfo selectedSourceInfo = null;

            // Pick first color source             
            var groups = await MediaFrameSourceGroup.FindAllAsync();
            foreach (MediaFrameSourceGroup sourceGroup in groups)
            {
                foreach (MediaFrameSourceInfo sourceInfo in sourceGroup.SourceInfos)
                {
                    if (sourceInfo.SourceKind == MediaFrameSourceKind.Color)
                    {
                        selectedSourceInfo = sourceInfo;
                        break;
                    }
                }

                if (selectedSourceInfo != null)
                {
                    selectedGroup = sourceGroup;
                    break;
                }
            }

            // No valid camera was found. This will happen on the emulator.
            if (selectedGroup == null || selectedSourceInfo == null)
            {
                Debug.WriteLine("Failed to find Group and SourceInfo");
                return new FrameGrabber();
            }

            // Create settings 
            var settings = new MediaCaptureInitializationSettings
            {
                SourceGroup = selectedGroup,

                // This media capture can share streaming with other apps.
                SharingMode = MediaCaptureSharingMode.SharedReadOnly,

                // Only stream video and don't initialize audio capture devices.
                StreamingCaptureMode = StreamingCaptureMode.Video,

                // Set to CPU to ensure frames always contain CPU SoftwareBitmap images
                // instead of preferring GPU D3DSurface images.
                MemoryPreference = MediaCaptureMemoryPreference.Cpu,
            };

            // Create and initilize capture device 
            mediaCapture = new MediaCapture();

            try
            {
                await mediaCapture.InitializeAsync(settings);
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Failed to initilise mediacaptrue {e.ToString()}");
                return new FrameGrabber();
            }

            MediaFrameSource selectedSource = mediaCapture.FrameSources[selectedSourceInfo.Id];

            // create new frame reader 
            mediaFrameReader = await mediaCapture.CreateFrameReaderAsync(selectedSource);

            MediaFrameReaderStartStatus status = await mediaFrameReader.StartAsync();

            if (status == MediaFrameReaderStartStatus.Success)
            {
                Debug.WriteLine("MediaFrameReaderStartStatus == Success");
                return new FrameGrabber(datasource, mediaCapture, selectedSource, mediaFrameReader);
            }
            else
            {
                Debug.WriteLine($"MediaFrameReaderStartStatus != Success; {status}");
                return new FrameGrabber();
            }
        }

        #endregion

        async void SetFrame(MediaFrameReference frame)
        {
            IsAnalyzingFrame = true; 

            var node = datasource.CurrentNode;
            var position = datasource.NodePosition; 
            var forward = datasource.GazeForward;
            var up = datasource.GazeUp;
            var timestamp = Utils.GetCurrentUnixTimestampMillis();

            byte[] frameData = null; 
            try
            {
                frameData = await GetFrameData(frame);
            }
            catch { }            

            if(frameData == null)
            {
                IsAnalyzingFrame = false;
                return; 
            }

            CurrentFrame = new Frame
            {
                frameData = frameData,
                node = node,
                position = position, 
                forward = forward,
                up = up,
                timestamp = timestamp
            };            

            IsAnalyzingFrame = false;
        }

        void MediaFrameReader_FrameArrived(MediaFrameReader sender, MediaFrameArrivedEventArgs args)
        {
            if (!IsAnalyzingFrame && !IsNewFrameAvailable)
            {
                MediaFrameReference frame = sender.TryAcquireLatestFrame();

                if (frame != null)
                {
                    new Task(() => SetFrame(frame)).Start();
                }
            }
        }

        async Task<byte[]> GetFrameData(MediaFrameReference frame)
        {
            byte[] bytes = null;

            if (frame == null)
            {
                return bytes;
            }

            VideoMediaFrame videoMediaFrame = frame.VideoMediaFrame;

            if (videoMediaFrame == null)
            {
                return bytes;
            }

            VideoFrame videoFrame = videoMediaFrame.GetVideoFrame();
            SoftwareBitmap softwareBitmap = videoFrame.SoftwareBitmap;

            if (softwareBitmap == null)
            {
                return bytes;
            }

            SoftwareBitmap bitmapBGRA8 = SoftwareBitmap.Convert(softwareBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore);

            using (InMemoryRandomAccessStream stream = new InMemoryRandomAccessStream())
            {
                BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, stream);
                // Set the software bitmap
                encoder.SetSoftwareBitmap(bitmapBGRA8);
                encoder.IsThumbnailGenerated = false;

                try
                {
                    await encoder.FlushAsync();

                    bytes = new byte[stream.Size];
                    await stream.AsStream().ReadAsync(bytes, 0, bytes.Length);
                }
                catch (Exception e)
                {
                    Debug.WriteLine($"Error while trying to encode frame into a byte array, expceiton {e.Message}");
                }
            }

            return bytes;
        }
    }
}
