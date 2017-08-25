using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ProjectOxford.Face;
using Windows.Media.Capture.Frames;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using System.Diagnostics;
using System.IO;
using Windows.Media;

namespace FaceTag.Content
{
    /// <summary>
    /// Class used to perform facial recognitino using Microsofts Cognitive Service, see the following links for 
    /// details. 
    /// https://docs.microsoft.com/en-us/azure/cognitive-services/face/overview
    /// https://docs.microsoft.com/en-us/azure/cognitive-services/face/tutorials/faceapiincsharptutorial
    /// https://westus.dev.cognitive.microsoft.com/docs/services/563879b61984550e40cbbe8d/operations/563879b61984550f30395236
    /// https://github.com/Microsoft/Cognitive-Samples-VideoFrameAnalysis/
    /// https://github.com/Microsoft/Cognitive-Samples-VideoFrameAnalysis/blob/master/Windows/VideoFrameAnalyzer/FrameGrabber.cs
    /// https://docs.microsoft.com/en-gb/azure/cognitive-services/computer-vision/quickstarts/csharp
    /// </summary>
    public class FrameAnalyzer
    {
        public const int SUCCESS = 1;
        public const int FAILED_UNKNOWN = -1;

        #region types 

        /// <summary>
        /// Bounds 
        /// </summary>
        public struct Bounds
        {
            public int top;
            public int left;
            public int width;
            public int height;

            public override string ToString()
            {
                return $"Bounds({top}, {left}, {width}, {height})";
            }
        }

        /// <summary>
        /// Data object encapsulating the details of a identified person 
        /// </summary>
        public class DetectedPerson
        {
            public string name = "Unknown";
            public Bounds bounds;
            public Guid personId;
            public List<Guid> faceIds = new List<Guid>();
            public long lastDetectedTimestamp;

            // attributes 
            public string gender = "unknown";
            public float smile = 0f;
            public int age = 0; 

            public override string ToString()
            {
                var personIdString = personId != null ? personId.ToString() : string.Empty; 
                return $"DetectedPerson({name}, {bounds.ToString()}, {personIdString})";
            }
        }

        /// <summary>
        /// Data object loaded from the JSON file created by the create_groups.py script; used to search for faces 
        /// </summary>
        class Person
        {
            public string name;
            public string personId;
            public List<string> faceIds = new List<string>();

            public override string ToString()
            {
                return $"Person({name}, {personId})";
            }
        }        

        #endregion 

        #region properties and variables 

        FaceServiceClient faceClient;

        /// <summary>
        /// Service key 
        /// </summary>
        string cognitiveServiceKey; 

        string groupId;

        /// <summary>
        /// Loaded persons from the exported JSON file from the create_group.py script 
        /// </summary>
        List<Person> groupPersons = new List<Person>();
        
        private bool _isAnalyzingFrame = false;

        /// <summary>
        /// Flag to indicate if a frame is currently in progress or not 
        /// </summary>
        public bool IsAnalyzingFrame
        {
            get
            {
                lock (this)
                {
                    return _isAnalyzingFrame;
                }
            }
            private set
            {
                lock (this)
                {
                    _isAnalyzingFrame = value;
                }
            }
        }

        /// <summary>
        /// Is ready for another frame if:
        /// 1. Is currently not processing a frame 
        /// 2. Elapsed time since last processing the frame is greater or equal to frameAnalysisFrequencyMS (this throttles the API calls as there can be limit to how 
        /// quickly you call the service) 
        /// </summary>
        public bool IsReady
        {
            get
            {
                return !IsAnalyzingFrame && (Utils.GetCurrentUnixTimestampMillis() - lastFrameAnalysisTimestamp) >= frameAnalysisFrequencyMS;
            }
        }

        /// <summary>
        /// Timestamp of last processing a frame
        /// </summary>
        long lastFrameAnalysisTimestamp = 0;

        /// <summary>
        /// Frequency of processing frames 
        /// </summary>
        public long frameAnalysisFrequencyMS = 6000; 

        #endregion 

        private FrameAnalyzer()
        {

        }

        #region factory methods

        public static async Task<FrameAnalyzer> CreateAsync()
        {
            var fa = new FrameAnalyzer();

            await LoadKey(fa);
            await LoadGroupPersons(fa);

            fa.faceClient = new FaceServiceClient(fa.cognitiveServiceKey);
         
            // validate fa 
            if (string.IsNullOrEmpty(fa.cognitiveServiceKey))
            {
                throw new Exception("cognitive service key unavailable or invalid"); 
            }

            if (string.IsNullOrEmpty(fa.groupId))
            {
                throw new Exception("face group id unavailable or invalid");
            }

            if (fa.groupPersons == null || fa.groupPersons.Count == 0)
            {
                throw new Exception("no persons loaded");
            }

            return fa;        
        }
                      
        async static Task LoadKey(FrameAnalyzer fa)
        {
            var packageFolder = Windows.ApplicationModel.Package.Current.InstalledLocation;
            var sampleFile = await packageFolder.GetFileAsync("cognitive_service.key");
            var key = await Windows.Storage.FileIO.ReadTextAsync(sampleFile);
            fa.cognitiveServiceKey = key; 
        }

        async static Task LoadGroupPersons(FrameAnalyzer fa)
        {
            var packageFolder = Windows.ApplicationModel.Package.Current.InstalledLocation;
            var sampleFile = await packageFolder.GetFileAsync("group.json");
            var json = await Windows.Storage.FileIO.ReadTextAsync(sampleFile);

            JObject jsonObj = JObject.Parse(json);

            fa.groupId = (string)jsonObj["group_id"];

            JArray personsObj = (JArray)jsonObj["persons"];
            foreach(var personObj in personsObj)
            {                
                var person = new Person();
                person.name = (string)personObj["name"];
                person.personId = (string)personObj["person_id"];

                JArray faceIdsObj = (JArray)personObj["face_ids"];
                string[] faceIds = faceIdsObj.Select(c => (string)c).ToArray();
                person.faceIds.AddRange(faceIds);

                fa.groupPersons.Add(person); 
            }
        }

        #endregion 

        /// <summary>
        /// Analyze a frame and notufy the delegate of the results 
        /// </summary>
        /// <param name="frame"></param>
        /// <param name="callback"></param>
        public async void AnalyzeFrame(MediaFrameReference frame, Action<int, List<DetectedPerson>> callback)
        {
            IsAnalyzingFrame = true;

            var detectedPersonsInFrame = new List<DetectedPerson>();

            // get the raw data of the frame 
            var data = await GetFrameData(frame);

            if (data == null || data.Length == 0)
            {
                Debug.WriteLine("ERROR :: AnalyzeFrame failed - data is null or empty");
                IsAnalyzingFrame = false;
                callback(FAILED_UNKNOWN, detectedPersonsInFrame);
                return;
            }

            // call the remote API to dectect faces 
            await DetectFaces(data, detectedPersonsInFrame);

            // if faces were detected, then try to identify them 
            if (detectedPersonsInFrame.Count > 0)
            {
                await IdentifyFaces(detectedPersonsInFrame);
            }

            // try to match each identified person with our loaded repository  
            foreach(var p in detectedPersonsInFrame)
            {
                if(p.personId != null)
                {
                    var match = groupPersons.Where(m => m.personId.Equals(p.personId.ToString(), StringComparison.OrdinalIgnoreCase)).FirstOrDefault(); 
                    if(match != null)
                    {
                        p.name = match.name; 
                    }
                }
            }

            lastFrameAnalysisTimestamp = Utils.GetCurrentUnixTimestampMillis();
            IsAnalyzingFrame = false;            

            callback(SUCCESS, detectedPersonsInFrame);                       
        }

        /// <summary>
        /// Detect any faces within the image 
        /// </summary>
        /// <param name="data"></param>
        /// <param name="detectedPersons"></param>
        /// <returns></returns>
        async Task DetectFaces(byte[] data, List<DetectedPerson> detectedPersons)
        {
            Microsoft.ProjectOxford.Face.Contract.Face[] detectResults = null;

            using (var ms = new MemoryStream(data))
            {
                detectResults = await faceClient.DetectAsync(ms, true, false, new[] { FaceAttributeType.Age, FaceAttributeType.Gender, FaceAttributeType.Smile });
            }

            if (detectResults != null && detectResults.Length > 0)
            {
                foreach (var detectResult in detectResults)
                {                    
                    var detectedPerson = new DetectedPerson
                    {
                        faceIds = new List<Guid>(new[] { detectResult.FaceId }),
                        bounds = new Bounds
                        {
                            left = detectResult.FaceRectangle.Left,
                            top = detectResult.FaceRectangle.Top,
                            width = detectResult.FaceRectangle.Width,
                            height = detectResult.FaceRectangle.Height
                        }, 
                        lastDetectedTimestamp = Utils.GetCurrentUnixTimestampMillis()
                    };

                    var faceAttributes = detectResult.FaceAttributes;
                    if(faceAttributes != null)
                    {
                        detectedPerson.age = (int)faceAttributes.Age;
                        detectedPerson.smile = (float)faceAttributes.Smile;
                        detectedPerson.gender = faceAttributes.Gender != null ? faceAttributes.Gender : "unknown";
                    }

                    detectedPersons.Add(detectedPerson);
                }
            }
        }

        /// <summary>
        /// Try to identify all recognised faces 
        /// </summary>
        /// <param name="detectedPersons"></param>
        /// <returns></returns>
        async Task IdentifyFaces(List<DetectedPerson> detectedPersons)
        {
            Microsoft.ProjectOxford.Face.Contract.IdentifyResult[] identifyResults = null;

            identifyResults = await faceClient.IdentifyAsync(groupId, detectedPersons
                .Where(f => f.faceIds.Count > 0)
                .SelectMany(f => f.faceIds).ToArray(), 1);

            if(identifyResults != null && identifyResults.Length > 0)
            {
                foreach(var identity in identifyResults)
                {
                    if(identity.Candidates == null || identity.Candidates.Length == 0)
                    {
                        continue; 
                    }

                    var matchingPerson = detectedPersons.Where(p => p.faceIds.Contains(identity.FaceId)).FirstOrDefault(); 
                    if(matchingPerson != null)
                    {
                        matchingPerson.personId = identity.Candidates[0].PersonId;
                    }
                }
            }
        }

        /// <summary>
        /// Get the raw data from the frame 
        /// </summary>
        /// <param name="frame"></param>
        /// <returns></returns>
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
