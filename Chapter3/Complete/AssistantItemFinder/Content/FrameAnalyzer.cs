using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.ProjectOxford.Vision;
using Microsoft.ProjectOxford.Vision.Contract;

namespace AssistantItemFinder.Content
{
    /// <summary>
    /// Uses Microsofts Cognitive Vision Service to tag captured frames, used to understand what is around 
    /// the world 
    /// </summary>
    internal class FrameAnalyzer
    {
        public delegate void AnalyzedFrame(Frame frame, string description, List<string> tokens);
        public event AnalyzedFrame OnAnalyzedFrame = delegate { }; 

        #region properties and variables     

        public Queue<Frame> frameQueue = new Queue<Frame>();

        VisionServiceClient visionClient; 

        private string serviceKey = string.Empty;

        long lastFrameAnalysisTimestamp = 0;

        public long frameAnalysisFrequencyMS = 6 * 1000;

        ManualResetEvent processingManualResetEvent;

        CancellationTokenSource processingTokenSource;

        Task processingTask;

        #endregion

        public FrameAnalyzer()
        {

        }

        #region factory and initilisation methods 

        public static async Task<FrameAnalyzer> CreateAsync()
        {
            var fa = new FrameAnalyzer();

            await LoadKey(fa);

            if (string.IsNullOrEmpty(fa.serviceKey))
            {
                throw new Exception("service key unavailable or invalid");
            }

            fa.visionClient = new WesteuropeFaceServiceClient(fa.serviceKey);

            return fa;
        }

        async static Task LoadKey(FrameAnalyzer fa)
        {
            var packageFolder = Windows.ApplicationModel.Package.Current.InstalledLocation;
            var sFile = await packageFolder.GetFileAsync("cognitive_service.key");
            var key = await Windows.Storage.FileIO.ReadTextAsync(sFile);
            fa.serviceKey = key;
        }        

        #endregion 

        public void Start()
        {
            Debug.WriteLine("Starting FrameAnazlyer"); 

            if (processingTokenSource == null)
            {
                processingTokenSource = new CancellationTokenSource();
            }

            processingTask = Task.Factory.StartNew(RunAnalyzer, processingTokenSource.Token);
        }

        public void Stop()
        {
            Debug.WriteLine("Stopping FrameAnazlyer");

            if (processingTokenSource != null)
            {
                processingTokenSource.Cancel();
            }
        }

        public void AddFrame(Frame frame)
        {
            frameQueue.Enqueue(frame);

            // Signal the main thread to continue.  
            if(processingManualResetEvent != null)
            {
                processingManualResetEvent.Set();
            }            
        }

        async void RunAnalyzer()
        {
            Debug.WriteLine("Entering RunAnalzyer loop"); 

            processingManualResetEvent = new ManualResetEvent(false);

            while (!processingTokenSource.IsCancellationRequested)
            {
                // Set the event to nonsignaled state.  
                processingManualResetEvent.Reset();

                while (frameQueue.Count > 0)
                {
                    // throttle analyzer 
                    int surplusTime = (int)(frameAnalysisFrequencyMS - (Utils.GetCurrentUnixTimestampMillis() - lastFrameAnalysisTimestamp)); 
                    if(lastFrameAnalysisTimestamp > 0 && surplusTime > 0)
                    {
                        await Task.Delay(surplusTime);
                    }                    

                    var frame = frameQueue.Dequeue();

                    await AnalyzeFrame(frame.frameData, (status, description, tokens) =>
                    {
                        lastFrameAnalysisTimestamp = Utils.GetCurrentUnixTimestampMillis();

                        if(status > 0)
                        {
                            OnAnalyzedFrame(frame, description, tokens);
                        }
                    });
                }

                // Wait until a connection is made before continuing.  
                processingManualResetEvent.WaitOne();
            }

            Debug.WriteLine("Existing RunAnalzyer loop");
        }

        async Task AnalyzeFrame(byte[] data, Action<int, string, List<string>> callback)
        {
            const float minConfidence = 0.7f;

            var description = string.Empty;
            var tags = new HashSet<string>();

            using (var stream = new MemoryStream(data))
            {
                VisualFeature[] visualFeatures = new VisualFeature[] { VisualFeature.Categories, VisualFeature.Description, VisualFeature.Faces, VisualFeature.ImageType, VisualFeature.Tags };
                AnalysisResult analysisResult = await visionClient.AnalyzeImageAsync(stream, visualFeatures);

                if (analysisResult.Description != null && analysisResult.Description.Captions != null && analysisResult.Description.Captions.Length > 0)
                {                    
                    var caption = analysisResult.Description.Captions.OrderByDescending(x => x.Confidence).First();
                    if(caption.Confidence >= minConfidence)
                    {
                        description = caption.Text;
                    }

                    if(analysisResult.Description.Tags != null)
                    {
                        foreach(var tag in analysisResult.Description.Tags)
                        {
                            tags.Add(tag.ToLower()); 
                        }
                    }
                }

                if(analysisResult.Tags != null)
                {
                    foreach(var tag in analysisResult.Tags)
                    {
                        if(tag.Confidence >= minConfidence)
                        {
                            tags.Add(tag.Name.ToLower()); 
                        }
                    }
                }
            }

            callback(string.IsNullOrEmpty(description) ? -1 : 1, description, tags.ToList());      
        }         
    }
}
