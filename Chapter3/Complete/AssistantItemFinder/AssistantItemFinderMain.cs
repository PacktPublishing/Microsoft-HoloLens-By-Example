using System;
using System.Diagnostics;
using System.Linq;
using Windows.Graphics.Holographic;
using Windows.Perception.Spatial;
using Windows.UI.Input.Spatial;

using AssistantItemFinder.Common;
using System.Threading.Tasks;
using Windows.Foundation;
using System.Collections.Generic;
using System.Numerics;
using Windows.Perception;

using AssistantItemFinder.Content;
using static AssistantItemFinder.Content.FrameGrabber;

namespace AssistantItemFinder
{
    /// <summary>
    /// Updates, renders, and presents holographic content using Direct3D.
    /// </summary>
    internal class AssistantItemFinderMain : IDisposable, IFrameGrabberDataSource
    {
        const float EntityOffsetY = -1.0f;

        /// <summary>
        /// How much space each node occupies, decreasing this will increase the resolution
        /// for path finding 
        /// </summary>
        const float NodeRadius = 0.8f; 

        private Node currentNode = null;

        private Vector3 currentNodePosition = Vector3.Zero; 

        private Vector3 currentGazeForward = Vector3.Zero;

        private Vector3 currentGazeRight = Vector3.Zero;

        private double dwellTimeAtCurrentNode = 0f; 

        private List<Node> nodes = new List<Node>();

        private List<Edge> edges = new List<Edge>(); 

        private List<Entity> entities = new List<Entity>();

        private string requestedSightingTerm = string.Empty; 

        private Node targetNode = null;
        private Sighting targetSighting = null; 

        private Renderer targetNodeRenderer;
        private Renderer nodeRenderer;

        private SpatialInputHandler         spatialInputHandler;

        // Cached reference to device resources.
        private DeviceResources             deviceResources;

        // Render loop timer.
        private StepTimer                   timer = new StepTimer();

        // Represents the holographic space around the user.
        HolographicSpace                    holographicSpace;

        // SpatialLocator that is attached to the primary camera.
        SpatialLocator                      locator;

        // A reference frame attached to the holographic camera.        
        SpatialLocatorAttachedFrameOfReference referenceFrame;

        #region IFrameGrabberDatasource

        public Node CurrentNode
        {
            get
            {
                lock (this)
                {
                    return currentNode;
                }
            }
            private set
            {
                lock (this)
                {
                    if(currentNode != value)
                    {
                        // reset dwell time if the node has changed 
                        dwellTimeAtCurrentNode = 0; 

                        currentNode = value;
                    }                    
                }
            }
        }

        public Vector3 NodePosition
        {
            get
            {
                lock (this)
                {
                    return currentNodePosition;
                }
            }
            private set
            {
                lock (this)
                {
                    currentNodePosition = value;
                }
            }
        }

        public Vector3 GazeForward
        {
            get
            {
                lock (this)
                {
                    return currentGazeForward;
                }
            }
            private set
            {
                lock (this)
                {
                    currentGazeForward = value; 
                }
            }
        }

        public Vector3 GazeUp
        {
            get
            {
                lock (this)
                {
                    return currentGazeRight;
                }
            }
            private set
            {
                lock (this)
                {
                    currentGazeRight = value; 
                }
            }
        }

        #endregion 

        FrameGrabber frameGrabber;

        FrameAnalyzer frameAnalyzer;

        SpeechManager speechManager;

        /// <summary>
        /// Loads and initializes application assets when the application is loaded.
        /// </summary>
        /// <param name="deviceResources"></param>
        public AssistantItemFinderMain(DeviceResources deviceResources)
        {
            this.deviceResources = deviceResources;

            // Register to be notified if the Direct3D device is lost.
            this.deviceResources.DeviceLost     += this.OnDeviceLost;
            this.deviceResources.DeviceRestored += this.OnDeviceRestored;
        }

        public void SetHolographicSpace(HolographicSpace holographicSpace)
        {
            this.holographicSpace = holographicSpace;

            targetNodeRenderer = new NodeRenderer(deviceResources, new Vector3(0.3f, 1.0f, 0.3f), 0.09f);
            targetNodeRenderer.CreateDeviceDependentResourcesAsync();

            nodeRenderer = new NodeRenderer(deviceResources, new Vector3(0.3f, 0.3f, 1.0f), 0.05f);
            nodeRenderer.CreateDeviceDependentResourcesAsync(); 

            spatialInputHandler = new SpatialInputHandler();

            // Use the default SpatialLocator to track the motion of the device.
            locator = SpatialLocator.GetDefault();

            // Be able to respond to changes in the positional tracking state.
            locator.LocatabilityChanged += this.OnLocatabilityChanged;

            // Respond to camera added events by creating any resources that are specific
            // to that camera, such as the back buffer render target view.
            // When we add an event handler for CameraAdded, the API layer will avoid putting
            // the new camera in new HolographicFrames until we complete the deferral we created
            // for that handler, or return from the handler without creating a deferral. This
            // allows the app to take more than one frame to finish creating resources and
            // loading assets for the new holographic camera.
            // This function should be registered before the app creates any HolographicFrames.
            holographicSpace.CameraAdded += this.OnCameraAdded;

            // Respond to camera removed events by releasing resources that were created for that
            // camera.
            // When the app receives a CameraRemoved event, it releases all references to the back
            // buffer right away. This includes render target views, Direct2D target bitmaps, and so on.
            // The app must also ensure that the back buffer is not attached as a render target, as
            // shown in DeviceResources.ReleaseResourcesForBackBuffer.
            holographicSpace.CameraRemoved += this.OnCameraRemoved;

            // The simplest way to render world-locked holograms is to create a stationary reference frame
            // when the app is launched. This is roughly analogous to creating a "world" coordinate system
            // with the origin placed at the device's position as the app is launched.

            referenceFrame = locator.CreateAttachedFrameOfReferenceAtCurrentHeading();

            // Notes on spatial tracking APIs:
            // * Stationary reference frames are designed to provide a best-fit position relative to the
            //   overall space. Individual positions within that reference frame are allowed to drift slightly
            //   as the device learns more about the environment.
            // * When precise placement of individual holograms is required, a SpatialAnchor should be used to
            //   anchor the individual hologram to a position in the real world - for example, a point the user
            //   indicates to be of special interest. Anchor positions do not drift, but can be corrected; the
            //   anchor will use the corrected position starting in the next frame after the correction has
            //   occurred.

            InitServices();
        }

        async void InitServices()
        {
            frameGrabber = await FrameGrabber.CreateAsync(this);

            frameAnalyzer = await FrameAnalyzer.CreateAsync();
            frameAnalyzer.Start(); 
            frameAnalyzer.OnAnalyzedFrame += FrameAnalyzer_OnAnalyzedFrame;

            speechManager = await SpeechManager.CreateAndStartAsync();
            speechManager.OnPhraseRecognized += SpeechManager_OnPhraseRecognized;
        }        

        public void Dispose()
        {
            if(frameAnalyzer != null)
            {
                frameAnalyzer.Stop();
                frameAnalyzer = null; 
            }

            if(speechManager != null)
            {
                speechManager.Stop();
                speechManager = null; 
            }            

            if (nodeRenderer != null)
            {
                nodeRenderer.Dispose();
                nodeRenderer = null;
            }

            if (targetNodeRenderer != null)
            {
                targetNodeRenderer.Dispose();
                targetNodeRenderer = null;
            }
        }

        /// <summary>
        /// Updates the application state once per frame.
        /// </summary>
        public HolographicFrame Update()
        {
            // Before doing the timer update, there is some work to do per-frame
            // to maintain holographic rendering. First, we will get information
            // about the current frame.

            // The HolographicFrame has information that the app needs in order
            // to update and render the current frame. The app begins each new
            // frame by calling CreateNextFrame.
            HolographicFrame holographicFrame = holographicSpace.CreateNextFrame();

            // Get a prediction of where holographic cameras will be when this frame
            // is presented.
            HolographicFramePrediction prediction = holographicFrame.CurrentPrediction;

            // Back buffers can change from frame to frame. Validate each buffer, and recreate
            // resource views and depth buffers as needed.
            deviceResources.EnsureCameraResources(holographicFrame, prediction);

            // Next, we get a coordinate system from the attached frame of reference that is
            // associated with the current frame. Later, this coordinate system is used for
            // for creating the stereo view matrices when rendering the sample content.      
                             
            SpatialCoordinateSystem referenceFrameCoordinateSystem = referenceFrame.GetStationaryCoordinateSystemAtTimestamp(prediction.Timestamp);            

            // remember where we were (changed if the CurrentNode != previousNode) 
            var previousNode = CurrentNode;

            // update current node the user resides in 
            CurrentNode = UpdateCurrentNode(referenceFrameCoordinateSystem, prediction.Timestamp,  NodeRadius);

            // .. and current gaze 
            SpatialPointerPose pose = SpatialPointerPose.TryGetAtTimestamp(referenceFrameCoordinateSystem, prediction.Timestamp);

            NodePosition = pose.Head.Position;
            GazeForward = pose.Head.ForwardDirection;
            GazeUp = pose.Head.UpDirection; 

            var mat = referenceFrameCoordinateSystem.TryGetTransformTo(CurrentNode.Anchor.CoordinateSystem);

            if (mat.HasValue)
            {
                NodePosition = Vector3.Transform(NodePosition, mat.Value);
                GazeForward = Vector3.TransformNormal(GazeForward, mat.Value);
                GazeUp = Vector3.TransformNormal(GazeUp, mat.Value);
            }

            if (!string.IsNullOrEmpty(requestedSightingTerm))
            {                
                var candidates = FindClosestNodesWithSightedItem(referenceFrameCoordinateSystem, pose, requestedSightingTerm);

                if(candidates != null && candidates.Count > 0)
                {
                    targetNode = candidates[0];
                    targetSighting = candidates[0].Sightings.Where(sighting => sighting.Tokens.Any(token => token.Equals(requestedSightingTerm, StringComparison.OrdinalIgnoreCase))).First();
                }

                requestedSightingTerm = string.Empty;
            }

            // currently at position 
            if (CurrentNode == targetNode)
            {
                if (dwellTimeAtCurrentNode >= 5)
                {
                    targetNode = null;
                    targetSighting = null; 
                    entities.Clear();
                    Debug.WriteLine("Well done! Assisted the user find their item");
                }
            }

            if (targetNode != null)
            {
                RebuildTrailToTarget(referenceFrameCoordinateSystem, prediction.Timestamp, CurrentNode, targetNode);                
            }


            ProcessNextFrame();

            timer.Tick(() => 
            {
                dwellTimeAtCurrentNode += timer.ElapsedSeconds;

                for (var entityIndex=0; entityIndex<entities.Count; entityIndex++)
                {
                    var entity = entities[entityIndex]; 
                    entity.Update(timer, referenceFrameCoordinateSystem);
                }
            });

            // We complete the frame update by using information about our content positioning
            // to set the focus point.
            foreach (var cameraPose in prediction.CameraPoses)
            {
                // The HolographicCameraRenderingParameters class provides access to set
                // the image stabilization parameters.
                HolographicCameraRenderingParameters renderingParameters = holographicFrame.GetRenderingParameters(cameraPose);             
            }

            // The holographic frame will be used to get up-to-date view and projection matrices and
            // to present the swap chain.
            return holographicFrame;
        }

        bool IsReadyToProcessFrame()
        {
            return
                frameGrabber != null && // frameCapture has been initilised 
                frameGrabber.IsNewFrameAvailable && // frameCapture has a frame 
                frameAnalyzer != null;             
        }

        void FrameAnalyzer_OnAnalyzedFrame(Frame frame, string description, List<string> tokens)
        {
            if (string.IsNullOrEmpty(description))
            {
                return; 
            }

            Debug.WriteLine($"FrameAnalyzer_OnAnalyzedFrame {frame.node.Name} ... {description}"); 

            frame.node.AddSighting(new Sighting(description, frame.position, frame.forward, frame.up, tokens.ToArray()));
        }

        /// <summary>
        /// Handle recognised phrases from the user 
        /// </summary>
        /// <param name="status"></param>
        /// <param name="text"></param>
        /// <param name="rulePath"></param>
        /// <param name="semanticInterpretation"></param>
        void SpeechManager_OnPhraseRecognized(int status, string text, string tag)
        {
            if(status < 0)
            {
                return; 
            }

            var tagSplit = tag.Split('_');
            var intent = tagSplit[0]; 
            var item = tagSplit[1];

            if (intent.Equals("RememberLocation", StringComparison.OrdinalIgnoreCase))
            {
                var node = CurrentNode;
                var position = NodePosition;
                var forward = GazeForward;
                var up = GazeUp;

                node.AddSighting(new Sighting(text, position, forward, up, item));
            } 
            else if (intent.Equals("findLocation", StringComparison.OrdinalIgnoreCase))
            {
                // assign the object to a class variable to be searched for within the update method 
                requestedSightingTerm = item;
            }
        }

        /// <summary>
        /// Process the next frame available from the FrameGrabber 
        /// </summary>
        /// <returns></returns>
        bool ProcessNextFrame()
        {
            if (!IsReadyToProcessFrame())
            {
                return false;
            }

            // obtain the details of the last frame captured 
            Frame frame = frameGrabber.CurrentFrame;

            if (frame.frameData == null)
            {
                return false;
            }

            // filter out 
            if (IsFrameUnique(frame))
            {
                frameAnalyzer.AddFrame(frame);
            }                       

            return true; 
        }

        /// <summary>
        /// Ensure we haven't already processed a similar frame 
        /// </summary>
        /// <param name="frame"></param>
        /// <returns></returns>
        bool IsFrameUnique(Frame frame)
        {
            // similar frame in queue to be processed ?
            foreach(var queuedFrame in frameAnalyzer.frameQueue)
            {
                if (queuedFrame.IsSimilar(frame))
                {
                    return false; 
                }
            }

            // similar frame already processed ? 
            foreach(var node in nodes)
            {
                if(node != frame.node)
                {
                    continue; 
                }

                foreach(var sighting in node.Sightings)
                {
                    var dot = Vector3.Dot(sighting.Forward, frame.forward); 
                    if(dot >= 0.5)
                    {
                        return false; 
                    }     
                }
            }

            return true; 
        }

        int RebuildTrailToTarget(SpatialCoordinateSystem referenceFrameCoordinateSystem, PerceptionTimestamp perceptionTimestamp, Node startNode, Node endNode, 
            int lookAhead = 100)
        {
            Debug.WriteLine($"RebuildTrailToTarget {startNode.Name} -> {endNode.Name}");
             
            entities.Clear();

            Stack<Node> trail = new Stack<Node>(); 
            BuildPath(endNode, startNode, trail);

            if(trail.Count == 0)
            {
                Debug.WriteLine($"Unable to find Path for startNode {startNode.Name} and {endNode.Name}");
                return -1;  
            }

            var rootEntity = GetRootEntity(referenceFrameCoordinateSystem);

            int i = 0; 
            while(i < lookAhead && trail.Count > 0)
            {
                var node = trail.Pop();

                var entity = new Entity($"node_{i}");
                entity.Node = node;
                entity.Renderer = entity.Node == targetNode ? targetNodeRenderer : nodeRenderer;
                entity.UpdateTransform(referenceFrameCoordinateSystem);
                var targetPosition = rootEntity.Transform.Translation;
                entity.Position = new Vector3(0, (targetPosition - entity.Transform.Translation).Y, 0f);
                entities.Add(entity);

                i += 1; 
            }

            return 1; 
        }

        void CreateEntitiesForAllNodes(SpatialCoordinateSystem referenceFrameCoordinateSystem)
        {
            entities.Clear();

            var rootEntity = GetRootEntity(referenceFrameCoordinateSystem);

            var i = 0; 
            foreach (var node in this.nodes)
            {
                var entity = new Entity($"node_{i}");
                entity.Node = node;
                entity.Renderer = nodeRenderer;
                entity.UpdateTransform(referenceFrameCoordinateSystem);
                var targetPosition = rootEntity.Transform.Translation;
                entity.Position = new Vector3(0, (targetPosition - entity.Transform.Translation).Y, 0f);
                entities.Add(entity);

                i += 1; 
            }
        }

        /// <summary>
        /// We position the entities relative to the first node to keep the entities 
        /// positioned consistent 
        /// </summary>
        /// <param name="referenceFrameCoordinateSystem"></param>
        /// <returns></returns>
        Entity GetRootEntity(SpatialCoordinateSystem referenceFrameCoordinateSystem)
        {
            if(nodes.Count == 0)
            {
                return null; 
            }

            var entity = new Entity($"node_root");
            entity.Node = nodes[0];
            entity.Renderer = nodeRenderer;
            entity.Position = new Vector3(0, EntityOffsetY, 0);
            entity.UpdateTransform(referenceFrameCoordinateSystem);

            return entity; 
        }

        bool BuildPath(Node currentNode, Node endNode, Stack<Node> trail)
        {
            Debug.WriteLine($"BuildPath {currentNode.Name} -> {endNode.Name}");
             
            List<Node> connectedNodes = edges.Where(edge => edge.NodeA == currentNode || edge.NodeB == currentNode).Select(edge => (edge.NodeA != currentNode ? edge.NodeA : edge.NodeB)).ToList();
            if (connectedNodes.Contains(endNode)){
                Debug.WriteLine($"(found endpoint) Adding node {currentNode.Name}"); 
                trail.Push(currentNode);  
                return true; 
            }
            else
            {
                foreach(var node in connectedNodes)
                {
                    if(BuildPath(node, endNode, trail))
                    {
                        Debug.WriteLine($"Adding node {currentNode.Name}");
                        trail.Push(currentNode);
                        return true; 
                    }
                }
            }

            return false; 
        }

        Node UpdateCurrentNode(SpatialCoordinateSystem referenceFrameCoordinateSystem, PerceptionTimestamp perceptionTimestamp, float nodeRadius = 1.0f)
        {
            SpatialPointerPose pose = SpatialPointerPose.TryGetAtTimestamp(referenceFrameCoordinateSystem, perceptionTimestamp);

            if(pose == null)
            {
                return currentNode; 
            }

            if (currentNode == null)
            { 
                // create current node 
                var nodeAnchor = Spatial​Anchor.TryCreateRelativeTo(referenceFrameCoordinateSystem, pose.Head.ForwardDirection * 0.1f);
                
                if (nodeAnchor == null)
                {
                    Debug.WriteLine($"WARN: Failed to create Anchor"); 
                    return null; 
                }

                Debug.WriteLine($"Creating new node Head position {pose.Head.Position} and direction {pose.Head.ForwardDirection}");

                AddNode(nodeAnchor, perceptionTimestamp);

                return nodes[nodes.Count-1];                
            }
            else
            {
                // outside the current nodes threshold? 
                var distance = currentNode.TryGetDistance(referenceFrameCoordinateSystem, pose.Head.Position);
                if(distance.HasValue && distance.Value > nodeRadius)
                {
                    // search for node 
                    var closestNodes = GetClosestNodes(referenceFrameCoordinateSystem, pose, nodeRadius);
                    if(closestNodes != null && closestNodes.Count > 0)
                    {
                        foreach(var node in closestNodes)
                        {
                            if(node == currentNode)
                            {
                                continue; 
                            }

                            return node; 
                        }
                    }                    

                    // no node exist... try to create one
                    // position of current node in respect to the reference frame 
                    var currentNodesPosition = currentNode.TryGetTransformedPosition(referenceFrameCoordinateSystem);
                    if (currentNodesPosition.HasValue)
                    {
                        var direction = Vector3.Normalize(
                            new Vector3(pose.Head.Position.X, 0f, pose.Head.Position.Z) - 
                            new Vector3(currentNodesPosition.Value.X, 0f, currentNodesPosition.Value.Z)
                            );

                        var targetPosition = currentNodesPosition.Value + direction * nodeRadius;
                        var distanceFromPose = (targetPosition - new Vector3(pose.Head.Position.X, 0f, pose.Head.Position.Z)).Length();

                        var nodeAnchor = Spatial​Anchor.TryCreateRelativeTo(referenceFrameCoordinateSystem, (direction * distanceFromPose));

                        if (nodeAnchor != null)
                        {
                            var newNode = AddNode(nodeAnchor, perceptionTimestamp); 

                            // create a new edge connecting the current node and this node 
                            edges.Add(new Edge
                            {
                                NodeA = currentNode,
                                NodeB = newNode
                            });

                            Debug.WriteLine($"Creating new node ({newNode.Name}) Head position {pose.Head.Position} and direction {pose.Head.ForwardDirection}, direction from current node {direction}.. Edge created {currentNode.Name}");
                            return nodes[nodes.Count - 1];
                        }
                        else
                        {
                            Debug.WriteLine($"WARN: Failed to create Anchor");
                        }                        
                    }                    
                }                
            }

            return currentNode; 
        }

        Node AddNode(SpatialAnchor anchor, PerceptionTimestamp perceptionTimestamp)
        {
            var position = Vector3.Zero;
            var forward = Vector3.Zero;

            var anchorPose = SpatialPointerPose.TryGetAtTimestamp(anchor.CoordinateSystem, perceptionTimestamp);

            if(anchorPose != null)
            {
                position = anchorPose.Head.Position;
                forward = anchorPose.Head.ForwardDirection;
            }

            var node = new Node(anchor, position, forward);
            nodes.Add(node);

            return node; 
        }

        public IList<Node> FindClosestNodesWithSightedItem(SpatialCoordinateSystem referenceFrameCoordinateSystem, SpatialPointerPose pose, string sightingItem)
        {
            var filteredNodes = nodes.Where(node =>
            {
                return node.Sightings.Any(sighting =>
                {
                    return sighting.Tokens.Any(token => token.Equals(sightingItem, StringComparison.OrdinalIgnoreCase));
                });

            }); 

            if(filteredNodes != null)
            {
                return filteredNodes.OrderBy(node =>
                {
                    return node.TryGetDistance(referenceFrameCoordinateSystem, pose.Head.Position);
                }).ToList(); 
            }

            return null; 
        }

        public IList<Node> GetClosestNodes(SpatialCoordinateSystem referenceFrameCoordinateSystem, SpatialPointerPose pose, float nodeRadius = 0.5f)
        {
            return nodes.OrderBy(node =>
            {
                return node.TryGetDistance(referenceFrameCoordinateSystem, pose.Head.Position);
            }).Where(node =>
            {
                return node.TryGetDistance(referenceFrameCoordinateSystem, pose.Head.Position) <= nodeRadius;
            }).ToList();
        }

        /// <summary>
        /// Renders the current frame to each holographic display, according to the 
        /// current application and spatial positioning state. Returns true if the 
        /// frame was rendered to at least one display.
        /// </summary>
        public bool Render(ref HolographicFrame holographicFrame)
        {
            // Don't try to render anything before the first Update.
            if (timer.FrameCount == 0)
            {
                return false;
            }

            //
            // TODO: Add code for pre-pass rendering here.
            //
            // Take care of any tasks that are not specific to an individual holographic
            // camera. This includes anything that doesn't need the final view or projection
            // matrix, such as lighting maps.
            //

            // Up-to-date frame predictions enhance the effectiveness of image stablization and
            // allow more accurate positioning of holograms.
            holographicFrame.UpdateCurrentPrediction();
            HolographicFramePrediction prediction = holographicFrame.CurrentPrediction;

            // Lock the set of holographic camera resources, then draw to each camera
            // in this frame.
            return deviceResources.UseHolographicCameraResources(
                (Dictionary<uint, CameraResources> cameraResourceDictionary) =>
            {
                bool atLeastOneCameraRendered = false;

                foreach (var cameraPose in prediction.CameraPoses)
                {
                    // This represents the device-based resources for a HolographicCamera.
                    CameraResources cameraResources = cameraResourceDictionary[cameraPose.HolographicCamera.Id];

                    // Get the device context.
                    var context = deviceResources.D3DDeviceContext;
                    var renderTargetView = cameraResources.BackBufferRenderTargetView;
                    var depthStencilView = cameraResources.DepthStencilView;

                    // Set render targets to the current holographic camera.
                    context.OutputMerger.SetRenderTargets(depthStencilView, renderTargetView);

                    // Clear the back buffer and depth stencil view.
                    SharpDX.Mathematics.Interop.RawColor4 transparent = new SharpDX.Mathematics.Interop.RawColor4(0.0f, 0.0f, 0.0f, 0.0f);
                    context.ClearRenderTargetView(renderTargetView, transparent);
                    context.ClearDepthStencilView(
                        depthStencilView,
                        SharpDX.Direct3D11.DepthStencilClearFlags.Depth | SharpDX.Direct3D11.DepthStencilClearFlags.Stencil,
                        1.0f,
                        0);

                    // The view and projection matrices for each holographic camera will change
                    // every frame. This function refreshes the data in the constant buffer for
                    // the holographic camera indicated by cameraPose.

                    SpatialCoordinateSystem referenceFrameCoordinateSystem = referenceFrame.GetStationaryCoordinateSystemAtTimestamp(prediction.Timestamp);

                    if(referenceFrameCoordinateSystem == null)
                    {
                        continue; 
                    }

                    cameraResources.UpdateViewProjectionBuffer(deviceResources, cameraPose, referenceFrameCoordinateSystem);

                    // Attach the view/projection constant buffer for this camera to the graphics pipeline.
                    bool cameraActive = cameraResources.AttachViewProjectionBuffer(deviceResources);

                    // Only render world-locked content when positional tracking is active.
                    if (cameraActive)
                    {
                        foreach(var entity in entities)
                        {
                            entity.Render();
                        }
                    }

                    atLeastOneCameraRendered = true;
                }

                return atLeastOneCameraRendered;
            });
        }

        public void SaveAppState()
        {
            //
            // TODO: Insert code here to save your app state.
            //       This method is called when the app is about to suspend.
            //
            //       For example, store information in the SpatialAnchorStore.
            //
        }

        public void LoadAppState()
        {
            //
            // TODO: Insert code here to load your app state.
            //       This method is called when the app resumes.
            //
            //       For example, load information from the SpatialAnchorStore.
            //
        }


        /// <summary>
        /// Notifies renderers that device resources need to be released.
        /// </summary>
        public void OnDeviceLost(Object sender, EventArgs e)
        {            
            nodeRenderer.ReleaseDeviceDependentResources();
            targetNodeRenderer.ReleaseDeviceDependentResources();
        }

        /// <summary>
        /// Notifies renderers that device resources may now be recreated.
        /// </summary>
        public void OnDeviceRestored(Object sender, EventArgs e)
        {            
            nodeRenderer.CreateDeviceDependentResourcesAsync();
            targetNodeRenderer.CreateDeviceDependentResourcesAsync();
        }

        void OnLocatabilityChanged(SpatialLocator sender, Object args)
        {
            switch (sender.Locatability)
            {
                case SpatialLocatability.Unavailable:
                    // Holograms cannot be rendered.
                    {
                        String message = "Warning! Positional tracking is " + sender.Locatability + ".";
                        Debug.WriteLine(message);
                    }
                    break;

                // In the following three cases, it is still possible to place holograms using a
                // SpatialLocatorAttachedFrameOfReference.
                case SpatialLocatability.PositionalTrackingActivating:
                // The system is preparing to use positional tracking.

                case SpatialLocatability.OrientationOnly:
                // Positional tracking has not been activated.

                case SpatialLocatability.PositionalTrackingInhibited:
                    // Positional tracking is temporarily inhibited. User action may be required
                    // in order to restore positional tracking.
                    break;

                case SpatialLocatability.PositionalTrackingActive:
                    // Positional tracking is active. World-locked content can be rendered.
                    break;
            }
        }

        public void OnCameraAdded(
            HolographicSpace sender,
            HolographicSpaceCameraAddedEventArgs args
            )
        {
            Deferral deferral = args.GetDeferral();
            HolographicCamera holographicCamera = args.Camera;

            Task task1 = new Task(() =>
            {
                //
                // TODO: Allocate resources for the new camera and load any content specific to
                //       that camera. Note that the render target size (in pixels) is a property
                //       of the HolographicCamera object, and can be used to create off-screen
                //       render targets that match the resolution of the HolographicCamera.
                //

                // Create device-based resources for the holographic camera and add it to the list of
                // cameras used for updates and rendering. Notes:
                //   * Since this function may be called at any time, the AddHolographicCamera function
                //     waits until it can get a lock on the set of holographic camera resources before
                //     adding the new camera. At 60 frames per second this wait should not take long.
                //   * A subsequent Update will take the back buffer from the RenderingParameters of this
                //     camera's CameraPose and use it to create the ID3D11RenderTargetView for this camera.
                //     Content can then be rendered for the HolographicCamera.
                deviceResources.AddHolographicCamera(holographicCamera);

                // Holographic frame predictions will not include any information about this camera until
                // the deferral is completed.
                deferral.Complete();
            });
            task1.Start();
        }

        public void OnCameraRemoved(
            HolographicSpace sender,
            HolographicSpaceCameraRemovedEventArgs args
            )
        {
            Task task2 = new Task(() =>
            {
                //
                // TODO: Asynchronously unload or deactivate content resources (not back buffer 
                //       resources) that are specific only to the camera that was removed.
                //
            });
            task2.Start();

            // Before letting this callback return, ensure that all references to the back buffer 
            // are released.
            // Since this function may be called at any time, the RemoveHolographicCamera function
            // waits until it can get a lock on the set of holographic camera resources before
            // deallocating resources for this camera. At 60 frames per second this wait should
            // not take long.
            deviceResources.RemoveHolographicCamera(args.Camera);
        }
    }
}
