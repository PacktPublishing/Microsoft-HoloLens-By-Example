using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Media.Devices.Core;
using Windows.Perception.Spatial;

namespace FaceTag.Content
{
    public class FrameGrabber
    {
        public struct Frame
        {
            public MediaFrameReference mediaFrameReference;
            public SpatialCoordinateSystem spatialCoordinateSystem;
            public CameraIntrinsics cameraIntrinsics;
            public long timestamp;
        }

        #region properties and variables 

        MediaCapture mediaCapture;
        MediaFrameSource mediaFrameSource;
        MediaFrameReader mediaFrameReader;

        private Frame _lastFrame;

        public Frame LastFrame
        {
            get
            {
                lock (this)
                {
                    return _lastFrame;
                }
            }
            private set
            {
                lock (this)
                {
                    _lastFrame = value;
                }
            }
        }

        private DateTime _lastFrameCapturedTimestamp = DateTime.MaxValue;

        public float ElapsedTimeSinceLastFrameCaptured
        {
            get
            {
                return (float)(DateTime.Now - DateTime.MinValue).TotalMilliseconds;
            }
        }

        public bool IsValid
        {
            get
            {
                return mediaFrameReader != null;
            }
        }

        #endregion 

        private FrameGrabber(MediaCapture mediaCapture = null, MediaFrameSource mediaFrameSource = null, MediaFrameReader mediaFrameReader = null)
        {
            this.mediaCapture = mediaCapture;
            this.mediaFrameSource = mediaFrameSource;
            this.mediaFrameReader = mediaFrameReader;

            if (this.mediaFrameReader != null)
            {
                this.mediaFrameReader.FrameArrived += MediaFrameReader_FrameArrived;
            }
        }

        #region factory methods 

        public static async Task<FrameGrabber> CreateAsync()
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
                return new FrameGrabber(mediaCapture, selectedSource, mediaFrameReader);
            }
            else
            {
                Debug.WriteLine($"MediaFrameReaderStartStatus != Success; {status}");
                return new FrameGrabber();
            }
        }

        #endregion

        void SetFrame(MediaFrameReference frame)
        {
            var spatialCoordinateSystem = frame.CoordinateSystem;
            var cameraIntrinsics = frame.VideoMediaFrame.CameraIntrinsics;

            LastFrame = new Frame
            {
                mediaFrameReference = frame,
                spatialCoordinateSystem = spatialCoordinateSystem,
                cameraIntrinsics = cameraIntrinsics,
                timestamp = Utils.GetCurrentUnixTimestampMillis()
            };

            _lastFrameCapturedTimestamp = DateTime.Now;
        }

        void MediaFrameReader_FrameArrived(MediaFrameReader sender, MediaFrameArrivedEventArgs args)
        {
            MediaFrameReference frame = sender.TryAcquireLatestFrame();

            if (frame != null && frame.CoordinateSystem != null)
            {
                SetFrame(frame);
            }
        }
    }
}
