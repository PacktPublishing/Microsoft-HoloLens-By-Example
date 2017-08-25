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

namespace AssistantItemFinder
{
    /// <summary>
    /// Updates, renders, and presents holographic content using Direct3D.
    /// </summary>
    internal class AssistantItemFinderMain : IDisposable
    {
        const float EntitOffsetY = -0.5f;

        private Node currentNode = null;

        private double dwellTimeAtCurrentNode = 0f; 

        private List<Node> nodes = new List<Node>();

        private List<Edge> edges = new List<Edge>(); 

        private List<Entity> entities = new List<Entity>();

        private Node targetNode = null; 

        private Renderer        nodeRenderer;
        
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
        //SpatialStationaryFrameOfReference stationaryReferenceFrame;
        SpatialLocatorAttachedFrameOfReference attachedReferenceFrame;

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

            nodeRenderer = new SphereRenderer(deviceResources, new Vector3(0.3f, 0.3f, 1.0f));
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

            //stationaryReferenceFrame = locator.CreateStationaryFrameOfReferenceAtCurrentLocation();
            attachedReferenceFrame = locator.CreateAttachedFrameOfReferenceAtCurrentHeading();                        

            // Notes on spatial tracking APIs:
            // * Stationary reference frames are designed to provide a best-fit position relative to the
            //   overall space. Individual positions within that reference frame are allowed to drift slightly
            //   as the device learns more about the environment.
            // * When precise placement of individual holograms is required, a SpatialAnchor should be used to
            //   anchor the individual hologram to a position in the real world - for example, a point the user
            //   indicates to be of special interest. Anchor positions do not drift, but can be corrected; the
            //   anchor will use the corrected position starting in the next frame after the correction has
            //   occurred.
        }

        public void Dispose()
        {
            if (nodeRenderer != null)
            {
                nodeRenderer.Dispose();
                nodeRenderer = null;
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
                             
            SpatialCoordinateSystem referenceFrameCoordinateSystem = attachedReferenceFrame.GetStationaryCoordinateSystemAtTimestamp(prediction.Timestamp);            

            var previousNode = currentNode;

            currentNode = UpdateCurrentNode(referenceFrameCoordinateSystem, prediction.Timestamp);

            if (currentNode != previousNode)
            {                
                SpatialPointerPose pose = SpatialPointerPose.TryGetAtTimestamp(referenceFrameCoordinateSystem, prediction.Timestamp);                               
            }

            if (targetNode != null)
            {
                RebuildTrailToTarget(referenceFrameCoordinateSystem, prediction.Timestamp, currentNode, targetNode);
            }

            SpatialInteractionSourceState pointerState = spatialInputHandler.CheckForInput();
            if (null != pointerState)
            {
                Debug.WriteLine($"Setting target {nodes[1].Name}"); 
                targetNode = nodes[1];
            }

            timer.Tick(() => 
            {
                if(currentNode != previousNode)
                {
                    dwellTimeAtCurrentNode = 0;
                }
                else
                {
                    dwellTimeAtCurrentNode += timer.ElapsedSeconds;
                }

                for(var entityIndex = entities.Count-1; entityIndex>=0; entityIndex--)
                {
                    var entity = entities[entityIndex]; 

                    // update rotation of previous one
                    if (entityIndex != entities.Count - 1)
                    {
                        var previousEntity = entities[entityIndex + 1];
                        var previousEntityPosition = previousEntity.Node.TryGetTransformedPosition(referenceFrameCoordinateSystem);
                        var currentEntityPosition = entity.Node.TryGetTransformedPosition(referenceFrameCoordinateSystem); 
                        if (previousEntityPosition.HasValue && currentEntityPosition.HasValue)
                        {
                            var tV = previousEntityPosition.Value;
                            var sV = currentEntityPosition.Value;
                            tV.Y = sV.Y = 0; 
                            var diff = sV - tV;

                            var yAngle = Math.Atan2(diff.X, diff.Z);

                            entity.EulerAngles = new Vector3(0, (float)(yAngle * (180 / Math.PI)), 0);
                        }                        
                    }

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

                // SetFocusPoint informs the system about a specific point in your scene to
                // prioritize for image stabilization. The focus point is set independently
                // for each holographic camera.
                // You should set the focus point near the content that the user is looking at.
                // In this example, we put the focus point at the center of the sample hologram,
                // since that is the only hologram available for the user to focus on.
                // You can also set the relative velocity and facing of that content; the sample
                // hologram is at a fixed point so we only need to indicate its position.
                //if (spinningCubeSpatialAnchor != null)
                //{
                //    //renderingParameters.SetFocusPoint(
                //    //spinningCubeSpatialAnchor.CoordinateSystem,
                //    //spinningCubeRenderer.Position
                //    //);
                //}
                //else
                //{
                //    //renderingParameters.SetFocusPoint(
                //    //currentCoordinateSystem,
                //    //spinningCubeRenderer.Position
                //    //);
                //}                
            }

            // The holographic frame will be used to get up-to-date view and projection matrices and
            // to present the swap chain.
            return holographicFrame;
        }

        int RebuildTrailToTarget(SpatialCoordinateSystem referenceFrameCoordinateSystem, PerceptionTimestamp perceptionTimestamp, Node startNode, Node endNode, int lookAhead = 3)
        {
            Debug.WriteLine($"RebuildTrailToTarget {startNode.Name} -> {endNode.Name}");
             
            entities.Clear();

            var trail = new List<Node>(); 

            if(startNode == endNode)
            {
                trail.Add(startNode); 
            }
            else
            {
                BuildPath(endNode, startNode, trail, new List<Node>());
            }            

            if(trail.Count == 0)
            {
                Debug.WriteLine($"Unable to find Path for startNode {startNode.Name} and {endNode.Name}");
                return -1;  
            }

            Debug.WriteLine($"Creating trials {trail.ToArray()}");

            var baseEntity = GetReferenceEntitySpatialCoordinateSystem(referenceFrameCoordinateSystem); 
            
            for (var i = 0; i < Math.Min(trail.Count, lookAhead); i++)
            {
                var node = trail[i];   

                var entity = new Entity($"node_{i}");
                entity.Node = node;
                entity.Renderer = nodeRenderer;

                entity.UpdateTransform(referenceFrameCoordinateSystem);
                // offset from baseEntity (to keep the y positions uniform and consistent) 
                var targetPosition = baseEntity.Transform.Translation;
                entity.Position = new Vector3(0, (targetPosition - entity.Transform.Translation).Y, 0f);

                entities.Add(entity);
            }

            return 1; 
        }

        /// <summary>
        /// use node 0 as the relative reference point for y 
        /// </summary>
        /// <param name="referenceFrameCoordinateSystem"></param>
        /// <returns></returns>
        Entity GetReferenceEntitySpatialCoordinateSystem(SpatialCoordinateSystem referenceFrameCoordinateSystem)
        {
            if(nodes.Count == 0)
            {
                return null; 
            }

            var entity = new Entity($"base_node");
            entity.Node = nodes[0];
            entity.Renderer = nodeRenderer;

            entity.UpdateTransform(referenceFrameCoordinateSystem);

            return entity; 
        }

        bool BuildPath(Node currentNode, Node endNode, List<Node> trail, List<Node> visitedNodes)
        {            
            List<Node> connectedNodes = edges.Where(
                    edge => (edge.NodeA == currentNode || edge.NodeB == currentNode) 
                ).Select(
                    edge => (edge.NodeA != currentNode ? edge.NodeA : edge.NodeB)
                ).Where(
                    node => !visitedNodes.Contains(node) 
                ).ToList();

            visitedNodes.Add(currentNode);

            if (connectedNodes.Contains(endNode)){
                trail.Add(currentNode);  
                return true; 
            }
            else
            {
                List<Node> shortestTrail = null;

                foreach(var node in connectedNodes)
                {
                    var currentTrail = new List<Node>(); 

                    if (BuildPath(node, endNode, currentTrail, visitedNodes))
                    {                        
                        currentTrail.Add(currentNode);
                        
                        if(shortestTrail == null || currentTrail.Count < shortestTrail.Count)
                        {
                            shortestTrail = currentTrail;
                        }
                    }
                }

                if(shortestTrail != null)
                {
                    trail.AddRange(shortestTrail);
                    return true; 
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
                nodes.Add(new Node(nodeAnchor, Vector3.Zero));

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
                            nodes.Add(new Node(nodeAnchor, Vector3.Zero));

                            // create a new edge connecting the current node and this node 
                            edges.Add(new Edge
                            {
                                NodeA = currentNode,
                                NodeB = nodes[nodes.Count - 1]
                            });

                            Debug.WriteLine($"Creating new node ({nodes[nodes.Count - 1].Name}) Head position {pose.Head.Position} and direction {pose.Head.ForwardDirection}, direction from current node {direction}.. Edge created {edges.Last().ToString()}");
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

                    //SpatialCoordinateSystem referenceFrameCoordinateSystem = stationaryReferenceFrame.CoordinateSystem;
                    SpatialCoordinateSystem referenceFrameCoordinateSystem = attachedReferenceFrame.GetStationaryCoordinateSystemAtTimestamp(prediction.Timestamp);

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
        }

        /// <summary>
        /// Notifies renderers that device resources may now be recreated.
        /// </summary>
        public void OnDeviceRestored(Object sender, EventArgs e)
        {
            nodeRenderer.CreateDeviceDependentResourcesAsync();
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
