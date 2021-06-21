using System;
using System.Collections.Generic;
using System.Text;
using Xenko.Core.Mathematics;
using Xenko.Games;
using Xenko.Graphics;
using Silk.NET.OpenXR;
using System.Runtime.InteropServices;
using System.Linq;
using Silk.NET.Core;
using System.Diagnostics;
using Silk.NET.Core.Native;
using Xenko.Graphics.SDL;
using Vortice.Vulkan;

namespace Xenko.VirtualReality
{
    public class OpenXRHmd : VRDevice
    {
        // API Objects for accessing OpenXR
        public XR Xr;
        public Session globalSession;
        public Swapchain globalSwapchain;
        public Space globalPlaySpace;
        public FrameState globalFrameState;
        public ReferenceSpaceType play_space_type = ReferenceSpaceType.Stage; //XR_REFERENCE_SPACE_TYPE_LOCAL;
        public SwapchainImageVulkan2KHR[] images;
        public SwapchainImageVulkan2KHR[] depth_images;

        // array of view_count containers for submitting swapchains with rendered VR frames
        CompositionLayerProjectionView[] projection_views;

        // array of view_count views, filled by the runtime with current HMD display pose
        View[] views;

        // ExtDebugUtils is a handy OpenXR debugging extension which we'll enable if available unless told otherwise.
        public bool? IsDebugUtilsSupported;

        // OpenXR handles
        public Instance Instance;
        public ulong system_id = 0;

        // input stuff
        private enum HAND_PATHS
        {
            Hand = 0,
            TriggerValue = 1,
            ThumbstickY = 2,
            ThumbstickX = 3,
            TrackpadX = 4,
            TrackpadY = 5,
            GripValue = 6,
            Button1 = 7, // x on left, a on right (or either index)
            Button2 = 8, // y on left, b on right (or either index)
            Menu = 9,
            System = 10, // may be inaccessible
        }
        private ulong[,] hand_paths = new ulong[2, 11];

        // Misc
        private bool _unmanagedResourcesFreed;

        /// <summary>
        /// A simple function which throws an exception if the given OpenXR result indicates an error has been raised.
        /// </summary>
        /// <param name="result">The OpenXR result in question.</param>
        /// <returns>
        /// The same result passed in, just in case it's meaningful and we just want to use this to filter out errors.
        /// </returns>
        /// <exception cref="Exception">An exception for the given result if it indicates an error.</exception>
        [DebuggerHidden]
        [DebuggerStepThrough]
        internal static Result CheckResult(Result result)
        {
            if ((int)result < 0)
            {
                Window.GenerateGenericError(null, $"OpenXR raised an error! Code: {result} ({result:X})\n\nStack Trace: " + (new StackTrace()).ToString());
            }

            return result;
        }

        private List<string> Extensions = new List<string>();

        public unsafe ulong GetSwapchainImage()
        {
            // Get the swapchain image
            var swapchainIndex = 0u;
            var acquireInfo = new SwapchainImageAcquireInfo();
            CheckResult(Xr.AcquireSwapchainImage(globalSwapchain, in acquireInfo, ref swapchainIndex));

            var waitInfo = new SwapchainImageWaitInfo(timeout: long.MaxValue);
            CheckResult(Xr.WaitSwapchainImage(globalSwapchain, in waitInfo));

            return images[swapchainIndex].Image;
        }

        private unsafe void Prepare()
        {
            // Create our API object for OpenXR.
            Xr = XR.GetApi();

            Extensions.Clear();
            Extensions.Add("XR_KHR_vulkan_enable2");
            Extensions.Add("XR_EXT_hp_mixed_reality_controller");

            InstanceCreateInfo instanceCreateInfo;

            var appInfo = new ApplicationInfo()
            {
                ApiVersion = new Version64(1, 0, 9)
            };

            // We've got to marshal our strings and put them into global, immovable memory. To do that, we use
            // SilkMarshal.
            Span<byte> appName = new Span<byte>(appInfo.ApplicationName, 128);
            Span<byte> engName = new Span<byte>(appInfo.EngineName, 128);
            SilkMarshal.StringIntoSpan("FEGame", appName);
            SilkMarshal.StringIntoSpan("FocusEngine", engName);

            var requestedExtensions = SilkMarshal.StringArrayToPtr(Extensions);
            instanceCreateInfo = new InstanceCreateInfo
            (
                applicationInfo: appInfo,
                enabledExtensionCount: (uint)Extensions.Count,
                enabledExtensionNames: (byte**)requestedExtensions
            );

            // Now we're ready to make our instance!
            CheckResult(Xr.CreateInstance(in instanceCreateInfo, ref Instance));

            // For our benefit, let's log some information about the instance we've just created.
            InstanceProperties properties = new();
            CheckResult(Xr.GetInstanceProperties(Instance, ref properties));

            var runtimeName = SilkMarshal.PtrToString((nint)properties.RuntimeName);
            var runtimeVersion = ((Version)(Version64)properties.RuntimeVersion).ToString(3);

            Console.WriteLine($"[INFO] Application: Using OpenXR Runtime \"{runtimeName}\" v{runtimeVersion}");

            // We're creating a head-mounted-display (HMD, i.e. a VR headset) example, so we ask for a runtime which
            // supports that form factor. The response we get is a ulong that is the System ID.
            var getInfo = new SystemGetInfo(formFactor: FormFactor.HeadMountedDisplay);
            CheckResult(Xr.GetSystem(Instance, in getInfo, ref system_id));

            // Get the appropriate enabling extension, or fail if we can't.
            //if (!Xr.TryGetInstanceExtension(null, Instance, out VulkanEnable))
            //{
            //    throw new("Failed to get the graphics binding extension!");
            //}
        }

        private void ReleaseUnmanagedResources()
        {
            if (_unmanagedResourcesFreed)
            {
                return;
            }

            CheckResult(Xr.DestroyInstance(Instance));
            _unmanagedResourcesFreed = true;
        }

        private GameBase baseGame;

        private Size2 renderSize;

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private unsafe delegate Result pfnGetVulkanGraphicsRequirements2KHR(Instance instance, ulong sys_id, GraphicsRequirementsVulkanKHR* req);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private unsafe delegate Result pfnGetVulkanGraphicsDevice2KHR(Instance instance, VulkanGraphicsDeviceGetInfoKHR* getInfo, VkPhysicalDevice* vulkanPhysicalDevice);

        public OpenXRHmd(GameBase game)
        {
            baseGame = game;
            VRApi = VRApi.OpenXR;
        }

        public override Size2 ActualRenderFrameSize { get => renderSize; }
        public override float RenderFrameScaling { get; set; } = 1.4f;

        public override DeviceState State
        {
            get
            {
                if (Xr == null) return DeviceState.Invalid;
                return DeviceState.Valid;
            }
        }

        private Vector3 headPos;
        public override Vector3 HeadPosition => headPos;

        private Quaternion headRot;
        public override Quaternion HeadRotation => headRot;

        private Vector3 headLinVel;
        public override Vector3 HeadLinearVelocity => headLinVel;

        private Vector3 headAngVel;
        public override Vector3 HeadAngularVelocity => headAngVel;

        private TouchController leftHand;
        public override TouchController LeftHand => leftHand;

        private TouchController rightHand;
        public override TouchController RightHand => rightHand;

        private ulong poseCount;
        public override ulong PoseCount => poseCount;

        public override bool CanInitialize => true;

        internal Texture swapTexture;
        internal bool begunFrame;
        internal ulong swapchainPointer;

        public override unsafe void Commit(CommandList commandList, Texture renderFrame)
        {
            // if we didn't wait a frame, don't commit
            if (begunFrame == false)
                return;

            begunFrame = false;

            // submit textures
            // https://github.com/dotnet/Silk.NET/blob/b0b31779ce4db9b68922977fa11772b95f506e09/examples/CSharp/OpenGL%20Demos/OpenGL%20VR%20Demo/OpenXR/Renderer.cs#L507
            var frameEndInfo = new FrameEndInfo()
            {
                Type = StructureType.TypeFrameEndInfo,
                DisplayTime = globalFrameState.PredictedDisplayTime,
                EnvironmentBlendMode = EnvironmentBlendMode.Opaque                
            };

#if XENKO_GRAPHICS_API_VULKAN
            // copy texture to swapchain image
            swapTexture.SetFullHandles(new VkImage(swapchainPointer), VkImageView.Null, 
                                       renderFrame.NativeLayout, renderFrame.NativeAccessMask,
                                       renderFrame.NativeFormat, renderFrame.NativeImageAspect);
#endif

            commandList.Copy(renderFrame, swapTexture);

            // Release the swapchain image
            var releaseInfo = new SwapchainImageReleaseInfo() { Type = StructureType.TypeSwapchainImageReleaseInfo };
            CheckResult(Xr.ReleaseSwapchainImage(globalSwapchain, in releaseInfo));

            fixed (CompositionLayerProjectionView* ptr = &projection_views[0])
            {
                var projectionLayer = new CompositionLayerProjection
                (
                    viewCount: 2,
                    views: ptr,
                    space: globalPlaySpace                 
                );

                var layerPointer = (CompositionLayerBaseHeader*)&projectionLayer;
                for (var eye = 0; eye < 2; eye++)
                {
                    ref var layerView = ref projection_views[eye];
                    layerView.Fov = views[eye].Fov;
                    layerView.Pose = views[eye].Pose;
                }

                frameEndInfo.LayerCount = 1;
                frameEndInfo.Layers = &layerPointer;

                CheckResult(Xr.EndFrame(globalSession, in frameEndInfo));
            }
        }

        internal static Quaternion ConvertToFocus(ref Quaternionf quat)
        {
            return new Quaternion(-quat.X, -quat.Y, -quat.Z, quat.W);
        }

        public override unsafe void UpdatePositions(GameTime gameTime)
        {
            // wait get poses (headPos etc.)
            // --- Wait for our turn to do head-pose dependent computation and render a frame
            FrameWaitInfo frame_wait_info = new FrameWaitInfo()
            {
                Type = StructureType.TypeFrameWaitInfo,
            };

            CheckResult(Xr.WaitFrame(globalSession, in frame_wait_info, ref globalFrameState));

            // --- Create projection matrices and view matrices for each eye
            ViewLocateInfo view_locate_info = new ViewLocateInfo()
            {
                Type = StructureType.TypeViewLocateInfo,
                ViewConfigurationType = ViewConfigurationType.PrimaryStereo,
                DisplayTime = globalFrameState.PredictedDisplayTime,
                Space = globalPlaySpace
            };

            ViewState view_state = new ViewState()
            {
                Type = StructureType.TypeViewState
            };

            uint view_count;
            Xr.LocateView(globalSession, &view_locate_info, &view_state, 2, &view_count, views);
            
            // get head rotation
            headRot = ConvertToFocus(ref views[0].Pose.Orientation);
            
            // since we got eye positions, our head is between our eyes
            headPos.X = (views[0].Pose.Position.X + views[1].Pose.Position.X) *  0.5f;
            headPos.Y = (views[0].Pose.Position.Y + views[1].Pose.Position.Y) * -0.5f;
            headPos.Z = (views[0].Pose.Position.Z + views[1].Pose.Position.Z) *  0.5f;

            if ((Bool32)globalFrameState.ShouldRender)
            {
                FrameBeginInfo frame_begin_info = new FrameBeginInfo()
                {
                    Type = StructureType.TypeFrameBeginInfo,
                };

                CheckResult(Xr.BeginFrame(globalSession, &frame_begin_info));

                swapchainPointer = GetSwapchainImage();
                begunFrame = true;
            }
        }

        public override unsafe void Draw(GameTime gameTime)
        {
            poseCount++;
        }

        public override unsafe void Enable(GraphicsDevice device, GraphicsDeviceManager graphicsDeviceManager, bool requireMirror)
        {            
            // Changing the form_factor may require changing the view_type too.
            ViewConfigurationType view_type = ViewConfigurationType.PrimaryStereo;

            // Typically STAGE for room scale/standing, LOCAL for seated
            Space play_space;

            // the session deals with the renderloop submitting frames to the runtime
            Session session;

            // each physical Display/Eye is described by a view.
            // view_count usually depends on the form_factor / view_type.
            // dynamically allocating all view related structs instead of assuming 2
            // hopefully allows this app to scale easily to different view_counts.
            uint view_count = 0;
            // the viewconfiguration views contain information like resolution about each view
            ViewConfigurationView[] viewconfig_views;

            // array of view_count handles for swapchains.
            // it is possible to use imageRect to render all views to different areas of the
            // same texture, but in this example we use one swapchain per view
            Swapchain swapchain;
            // array of view_count ints, storing the length of swapchains
            uint[] swapchain_lengths;

            // depth swapchain equivalent to the VR color swapchains
            Swapchain depth_swapchains;
            uint[] depth_swapchain_lengths;
 
            /*struct
            {
                // supporting depth layers is *optional* for runtimes
                bool supported;
                XrCompositionLayerDepthInfoKHR* infos;
            }
            depth;*/

            // reuse this variable for all our OpenXR return codes
            Result result = Result.Success;

            // xrEnumerate*() functions are usually called once with CapacityInput = 0.
            // The function will write the required amount into CountOutput. We then have
            // to allocate an array to hold CountOutput elements and call the function
            // with CountOutput as CapacityInput.
            Prepare();

            // TODO: instance null will not be able to convert XrResult to string
            /*if (!xr_check(NULL, result, "Failed to enumerate number of extension properties"))
                return 1;

            XrExtensionProperties* ext_props = malloc(sizeof(XrExtensionProperties) * ext_count);
            for (uint16_t i = 0; i < ext_count; i++)
            {
                // we usually have to fill in the type (for validation) and set
                // next to NULL (or a pointer to an extension specific struct)
                ext_props[i].type = XR_TYPE_EXTENSION_PROPERTIES;
                ext_props[i].next = NULL;
            }

            result = xrEnumerateInstanceExtensionProperties(NULL, ext_count, &ext_count, ext_props);
            if (!xr_check(NULL, result, "Failed to enumerate extension properties"))
                return 1;

            bool opengl_supported = false;

            printf("Runtime supports %d extensions\n", ext_count);
            for (uint32_t i = 0; i < ext_count; i++)
            {
                printf("\t%s v%d\n", ext_props[i].extensionName, ext_props[i].extensionVersion);
                if (strcmp(XR_KHR_OPENGL_ENABLE_EXTENSION_NAME, ext_props[i].extensionName) == 0)
                {
                    opengl_supported = true;
                }

                if (strcmp(XR_KHR_COMPOSITION_LAYER_DEPTH_EXTENSION_NAME, ext_props[i].extensionName) == 0)
                {
                    depth.supported = true;
                }
            }
            free(ext_props);

            // A graphics extension like OpenGL is required to draw anything in VR
            if (!opengl_supported)
            {
                printf("Runtime does not support OpenGL extension!\n");
                return 1;
            }*/

            SystemProperties system_props = new SystemProperties() {
		        Type = StructureType.TypeSystemProperties,
            };

            result = Xr.GetSystemProperties(Instance, system_id, &system_props);

            ViewConfigurationView vcv = new ViewConfigurationView()
            {
                Type = StructureType.TypeViewConfigurationView,                 
            };

            viewconfig_views = new ViewConfigurationView[128];
            fixed (ViewConfigurationView* viewspnt = &viewconfig_views[0])
                result = Xr.EnumerateViewConfigurationView(Instance, system_id, view_type, (uint)viewconfig_views.Length, ref view_count, viewspnt);
            Array.Resize<ViewConfigurationView>(ref viewconfig_views, (int)view_count);

            // get size
            renderSize.Height = (int)Math.Round(viewconfig_views[0].RecommendedImageRectHeight * RenderFrameScaling);
            renderSize.Width = (int)Math.Round(viewconfig_views[0].RecommendedImageRectWidth * RenderFrameScaling) * 2; // 2 views in one frame

#if XENKO_GRAPHICS_API_VULKAN
            // this function pointer was loaded with xrGetInstanceProcAddr
            Silk.NET.Core.PfnVoidFunction func = new Silk.NET.Core.PfnVoidFunction();
            GraphicsRequirementsVulkan2KHR vulk = new GraphicsRequirementsVulkan2KHR();
            result = Xr.GetInstanceProcAddr(Instance, "xrGetVulkanGraphicsRequirements2KHR", ref func);
            Delegate vulk_req = Marshal.GetDelegateForFunctionPointer((IntPtr)func.Handle, typeof(pfnGetVulkanGraphicsRequirements2KHR));
            vulk_req.DynamicInvoke(Instance, system_id, new System.IntPtr(&vulk));

            VulkanGraphicsDeviceGetInfoKHR vgd = new VulkanGraphicsDeviceGetInfoKHR()
            {
                SystemId = system_id,
                Type = StructureType.TypeVulkanGraphicsDeviceGetInfoKhr,
                VulkanInstance = new VkHandle((nint)device.NativeInstance.Handle)
            };

            VkHandle physicalDevice = new VkHandle();

            result = Xr.GetInstanceProcAddr(Instance, "xrGetVulkanGraphicsDevice2KHR", ref func);
            Delegate vulk_dev = Marshal.GetDelegateForFunctionPointer((IntPtr)func.Handle, typeof(pfnGetVulkanGraphicsDevice2KHR));
            vulk_dev.DynamicInvoke(Instance, new System.IntPtr(&vgd), new System.IntPtr(&physicalDevice));

            // --- Create session
            var graphics_binding_vulkan = new GraphicsBindingVulkan2KHR()
            {
                Type = StructureType.TypeGraphicsBindingVulkanKhr,
                Device = new VkHandle((nint)device.NativeDevice.Handle),
                Instance = new VkHandle((nint)device.NativeInstance.Handle),
                PhysicalDevice = physicalDevice,
                QueueFamilyIndex = 0,
                QueueIndex = 0,
            };
#else
            GraphicsBindingVulkan2KHR graphics_binding_vulkan = new GraphicsBindingVulkan2KHR();
            throw new Exception("OpenXR is only compatible with Vulkan");
#endif

            if (graphics_binding_vulkan.PhysicalDevice.Handle == 0)
                Window.GenerateGenericError(null, "OpenXR couldn't find a physical device.\n\nIs an OpenXR runtime running (e.g. SteamVR)?");

            SessionCreateInfo session_create_info = new SessionCreateInfo() {
                Type = StructureType.TypeSessionCreateInfo,
                Next = &graphics_binding_vulkan,
                SystemId = system_id
            };

            result = Xr.CreateSession(Instance, &session_create_info, &session);
            globalSession = session;

            // Many runtimes support at least STAGE and LOCAL but not all do.
            // Sophisticated apps might check with xrEnumerateReferenceSpaces() if the
            // chosen one is supported and try another one if not.
            // Here we will get an error from xrCreateReferenceSpace() and exit.
            ReferenceSpaceCreateInfo play_space_create_info = new ReferenceSpaceCreateInfo()
            {
                Type = StructureType.TypeReferenceSpaceCreateInfo,
                ReferenceSpaceType = play_space_type,
                PoseInReferenceSpace = new Posef(new Quaternionf(0f, 0f, 0f, 1f), new Vector3f(0f, 0f, 0f))                 
            };

            result = Xr.CreateReferenceSpace(session, &play_space_create_info, &play_space);
            globalPlaySpace = play_space;

            // --- Create Swapchains
            /*uint32_t swapchain_format_count;
            result = xrEnumerateSwapchainFormats(session, 0, &swapchain_format_count, NULL);
            if (!xr_check(instance, result, "Failed to get number of supported swapchain formats"))
                return 1;

            printf("Runtime supports %d swapchain formats\n", swapchain_format_count);
            int64_t swapchain_formats[swapchain_format_count];
            result = xrEnumerateSwapchainFormats(session, swapchain_format_count, &swapchain_format_count,
                                                 swapchain_formats);
            if (!xr_check(instance, result, "Failed to enumerate swapchain formats"))
                return 1;

            // SRGB is usually a better choice than linear
            // a more sophisticated approach would iterate supported swapchain formats and choose from them
            int64_t color_format = get_swapchain_format(instance, session, GL_SRGB8_ALPHA8_EXT, true);

            // GL_DEPTH_COMPONENT16 is a good bet
            // SteamVR 1.16.4 supports GL_DEPTH_COMPONENT16, GL_DEPTH_COMPONENT24, GL_DEPTH_COMPONENT32
            // but NOT GL_DEPTH_COMPONENT32F
            int64_t depth_format = get_swapchain_format(instance, session, GL_DEPTH_COMPONENT16, false);
            if (depth_format < 0)
            {
                printf("Preferred depth format GL_DEPTH_COMPONENT16 not supported, disabling depth\n");
                depth.supported = false;
            }*/

            // --- Create swapchain for main VR rendering
            {
                // In the frame loop we render into OpenGL textures we receive from the runtime here.
                swapchain = new Swapchain();
                swapchain_lengths = new uint[1];
                SwapchainCreateInfo swapchain_create_info = new SwapchainCreateInfo() {
			        Type = StructureType.TypeSwapchainCreateInfo,
			        UsageFlags = SwapchainUsageFlags.SwapchainUsageTransferDstBit |
                                 SwapchainUsageFlags.SwapchainUsageSampledBit |
                                 SwapchainUsageFlags.SwapchainUsageColorAttachmentBit,
			        CreateFlags = 0,
			        Format = (long)43, // VK_FORMAT_R8G8B8A8_SRGB = 43
                    SampleCount = 1, //viewconfig_views[0].RecommendedSwapchainSampleCount,
			        Width = (uint)renderSize.Width,
			        Height = (uint)renderSize.Height,
			        FaceCount = 1,
			        ArraySize = 1,
			        MipCount = 1,
                };

                result = Xr.CreateSwapchain(session, &swapchain_create_info, &swapchain);
                globalSwapchain = swapchain;

                swapTexture = new Texture(baseGame.GraphicsDevice, new TextureDescription()
                {
                    ArraySize = 1,
                    Depth = 1,
                    Dimension = TextureDimension.Texture2D,
                    Flags = TextureFlags.RenderTarget | TextureFlags.ShaderResource,
                    Format = PixelFormat.R8G8B8A8_UNorm_SRgb,
                    Height = renderSize.Height,
                    MipLevels = 1,
                    MultisampleCount = MultisampleCount.None,
                    Options = TextureOptions.None,
                    Usage = GraphicsResourceUsage.Default,
                    Width = renderSize.Width,
                });

                // The runtime controls how many textures we have to be able to render to
                // (e.g. "triple buffering")
                /*result = xrEnumerateSwapchainImages(swapchains[i], 0, &swapchain_lengths[i], NULL);
                if (!xr_check(instance, result, "Failed to enumerate swapchains"))
                    return 1;

                images[i] = malloc(sizeof(XrSwapchainImageOpenGLKHR) * swapchain_lengths[i]);
                for (uint32_t j = 0; j < swapchain_lengths[i]; j++)
                {
                    images[i][j].type = XR_TYPE_SWAPCHAIN_IMAGE_OPENGL_KHR;
                    images[i][j].next = NULL;
                }*/

                images = new SwapchainImageVulkan2KHR[32];
                uint img_count = 0;

                fixed (void* sibhp = &images[0]) {
                    CheckResult(Xr.EnumerateSwapchainImages(swapchain, (uint)images.Length, ref img_count, (SwapchainImageBaseHeader*)sibhp));
                }
                Array.Resize(ref images, (int)img_count);
            }

            // --- Create swapchain for depth buffers if supported (//TODO support depth buffering)
            /*{
                if (depth.supported)
                {
                    depth_swapchains = malloc(sizeof(XrSwapchain) * view_count);
                    depth_swapchain_lengths = malloc(sizeof(uint32_t) * view_count);
                    depth_images = malloc(sizeof(XrSwapchainImageOpenGLKHR*) * view_count);
                    for (uint32_t i = 0; i < view_count; i++)
                    {
                        XrSwapchainCreateInfo swapchain_create_info = {
				                .type = XR_TYPE_SWAPCHAIN_CREATE_INFO,
				                .usageFlags = XR_SWAPCHAIN_USAGE_DEPTH_STENCIL_ATTACHMENT_BIT,
				                .createFlags = 0,
				                .format = depth_format,
				                .sampleCount = viewconfig_views[i].recommendedSwapchainSampleCount,
				                .width = viewconfig_views[i].recommendedImageRectWidth,
				                .height = viewconfig_views[i].recommendedImageRectHeight,
				                .faceCount = 1,
				                .arraySize = 1,
				                .mipCount = 1,
				                .next = NULL,
                            };

                        result = xrCreateSwapchain(session, &swapchain_create_info, &depth_swapchains[i]);
                        if (!xr_check(instance, result, "Failed to create swapchain %d!", i))
                            return 1;

                        result =
                            xrEnumerateSwapchainImages(depth_swapchains[i], 0, &depth_swapchain_lengths[i], NULL);
                        if (!xr_check(instance, result, "Failed to enumerate swapchains"))
                            return 1;

                        // these are wrappers for the actual OpenGL texture id
                        depth_images[i] = malloc(sizeof(XrSwapchainImageOpenGLKHR) * depth_swapchain_lengths[i]);
                        for (uint32_t j = 0; j < depth_swapchain_lengths[i]; j++)
                        {
                            depth_images[i][j].type = XR_TYPE_SWAPCHAIN_IMAGE_OPENGL_KHR;
                            depth_images[i][j].next = NULL;
                        }
                        result = xrEnumerateSwapchainImages(depth_swapchains[i], depth_swapchain_lengths[i],
                                                            &depth_swapchain_lengths[i],
                                                            (XrSwapchainImageBaseHeader*)depth_images[i]);
                        if (!xr_check(instance, result, "Failed to enumerate swapchain images"))
                            return 1;
                    }
                }
            }*/


            // Do not allocate these every frame to save some resources
            views = new View[view_count]; //(XrView*)malloc(sizeof(XrView) * view_count);
            for (int i = 0; i < view_count; i++)
                views[i].Type = StructureType.TypeView;

            projection_views = new CompositionLayerProjectionView[view_count]; //(XrCompositionLayerProjectionView*)malloc(sizeof(XrCompositionLayerProjectionView) * view_count);
            for (int i = 0; i < view_count; i++)
            {
                projection_views[i].Type = StructureType.TypeCompositionLayerProjectionView; //XR_TYPE_COMPOSITION_LAYER_PROJECTION_VIEW;
                projection_views[i].SubImage.Swapchain = swapchain;
                projection_views[i].SubImage.ImageArrayIndex = 0;
                projection_views[i].SubImage.ImageRect.Offset.X = (renderSize.Width * i) / 2;
                projection_views[i].SubImage.ImageRect.Offset.Y = 0;
                projection_views[i].SubImage.ImageRect.Extent.Width = renderSize.Width / 2;
                projection_views[i].SubImage.ImageRect.Extent.Height = renderSize.Height;

                // projection_views[i].{pose, fov} have to be filled every frame in frame loop
            };


            /*if (depth.supported) //TODO: depth buffering support
            {
                depth.infos = (XrCompositionLayerDepthInfoKHR*)malloc(sizeof(XrCompositionLayerDepthInfoKHR) *
                                                                      view_count);
                for (uint32_t i = 0; i < view_count; i++)
                {
                    depth.infos[i].type = XR_TYPE_COMPOSITION_LAYER_DEPTH_INFO_KHR;
                    depth.infos[i].next = NULL;
                    depth.infos[i].minDepth = 0.f;
                    depth.infos[i].maxDepth = 1.f;
                    depth.infos[i].nearZ = gl_rendering.near_z;
                    depth.infos[i].farZ = gl_rendering.far_z;

                    depth.infos[i].subImage.swapchain = depth_swapchains[i];
                    depth.infos[i].subImage.imageArrayIndex = 0;
                    depth.infos[i].subImage.imageRect.offset.x = 0;
                    depth.infos[i].subImage.imageRect.offset.y = 0;
                    depth.infos[i].subImage.imageRect.extent.width =
                        viewconfig_views[i].recommendedImageRectWidth;
                    depth.infos[i].subImage.imageRect.extent.height =
                        viewconfig_views[i].recommendedImageRectHeight;

                    // depth is chained to projection, not submitted as separate layer
                    projection_views[i].next = &depth.infos[i];
                };
            }*/


            // --- Set up input (actions)

            Xr.StringToPath(Instance, "/user/hand/left", ref hand_paths[(int)TouchControllerHand.Left, (int)HAND_PATHS.Hand]);
            Xr.StringToPath(Instance, "/user/hand/right", ref hand_paths[(int)TouchControllerHand.Right, (int)HAND_PATHS.Hand]);

            Xr.StringToPath(Instance, "/user/hand/left/input/trigger/value",
                            ref hand_paths[(int)TouchControllerHand.Left, (int)HAND_PATHS.TriggerValue]);
            Xr.StringToPath(Instance, "/user/hand/right/input/trigger/value",
                            ref hand_paths[(int)TouchControllerHand.Right, (int)HAND_PATHS.TriggerValue]);

            /*Xr.StringToPath(Instance, "/user/hand/left/input/thumbstick/y",
                            ref thumbstick_y_path[(int)TouchControllerHand.Left]);
            Xr.StringToPath(Instance, "/user/hand/right/input/thumbstick/y",
                            ref thumbstick_y_path[(int)TouchControllerHand.Right]);

            /XrPath grip_pose_path[HAND_COUNT];
            Xr.StringToPath(Instance, "/user/hand/left/input/grip/pose", &grip_pose_path[(int)TouchControllerHand.Left]);
            Xr.StringToPath(Instance, "/user/hand/right/input/grip/pose", &grip_pose_path[(int)TouchControllerHand.Right]);

            XrPath haptic_path[HAND_COUNT];
            Xr.StringToPath(Instance, "/user/hand/left/output/haptic", &haptic_path[(int)TouchControllerHand.Left]);
            Xr.StringToPath(Instance, "/user/hand/right/output/haptic", &haptic_path[(int)TouchControllerHand.Right]);


            XrActionSetCreateInfo gameplay_actionset_info = {
	                .type = XR_TYPE_ACTION_SET_CREATE_INFO, .next = NULL, .priority = 0};
            strcpy(gameplay_actionset_info.actionSetName, "gameplay_actionset");
            strcpy(gameplay_actionset_info.localizedActionSetName, "Gameplay Actions");

            XrActionSet gameplay_actionset;
            result = xrCreateActionSet(instance, &gameplay_actionset_info, &gameplay_actionset);
            if (!xr_check(instance, result, "failed to create actionset"))
                return 1;

            XrAction hand_pose_action;
            {
                XrActionCreateInfo action_info = {.type = XR_TYPE_ACTION_CREATE_INFO,
		                                              .next = NULL,
		                                              .actionType = XR_ACTION_TYPE_POSE_INPUT,
		                                              .countSubactionPaths = HAND_COUNT,
		                                              .subactionPaths = hand_paths};
                strcpy(action_info.actionName, "handpose");
                strcpy(action_info.localizedActionName, "Hand Pose");

                result = xrCreateAction(gameplay_actionset, &action_info, &hand_pose_action);
                if (!xr_check(instance, result, "failed to create hand pose action"))
                    return 1;
            }
            // poses can't be queried directly, we need to create a space for each
            XrSpace hand_pose_spaces[HAND_COUNT];
            for (int hand = 0; hand < HAND_COUNT; hand++)
            {
                XrActionSpaceCreateInfo action_space_info = {.type = XR_TYPE_ACTION_SPACE_CREATE_INFO,
		                                                         .next = NULL,
		                                                         .action = hand_pose_action,
		                                                         .poseInActionSpace = identity_pose,
		                                                         .subactionPath = hand_paths[hand]};

                result = xrCreateActionSpace(session, &action_space_info, &hand_pose_spaces[hand]);
                if (!xr_check(instance, result, "failed to create hand %d pose space", hand))
                    return 1;
            }

            // Grabbing objects is not actually implemented in this demo, it only gives some  haptic feebdack.
            XrAction grab_action_float;
            {
                XrActionCreateInfo action_info = {.type = XR_TYPE_ACTION_CREATE_INFO,
		                                              .next = NULL,
		                                              .actionType = XR_ACTION_TYPE_FLOAT_INPUT,
		                                              .countSubactionPaths = HAND_COUNT,
		                                              .subactionPaths = hand_paths};
                strcpy(action_info.actionName, "grabobjectfloat");
                strcpy(action_info.localizedActionName, "Grab Object");

                result = xrCreateAction(gameplay_actionset, &action_info, &grab_action_float);
                if (!xr_check(instance, result, "failed to create grab action"))
                    return 1;
            }

            XrAction haptic_action;
            {
                XrActionCreateInfo action_info = {.type = XR_TYPE_ACTION_CREATE_INFO,
		                                              .next = NULL,
		                                              .actionType = XR_ACTION_TYPE_VIBRATION_OUTPUT,
		                                              .countSubactionPaths = HAND_COUNT,
		                                              .subactionPaths = hand_paths};
                strcpy(action_info.actionName, "haptic");
                strcpy(action_info.localizedActionName, "Haptic Vibration");
                result = xrCreateAction(gameplay_actionset, &action_info, &haptic_action);
                if (!xr_check(instance, result, "failed to create haptic action"))
                    return 1;
            }


            // suggest actions for simple controller
            {
                XrPath interaction_profile_path;
                result = xrStringToPath(instance, "/interaction_profiles/khr/simple_controller",
                                        &interaction_profile_path);
                if (!xr_check(instance, result, "failed to get interaction profile"))
                    return 1;

                const XrActionSuggestedBinding bindings[] = {
                        {.action = hand_pose_action, .binding = grip_pose_path[HAND_LEFT_INDEX]},
                        {.action = hand_pose_action, .binding = grip_pose_path[HAND_RIGHT_INDEX]},
		                // boolean input select/click will be converted to float that is either 0 or 1
		                {.action = grab_action_float, .binding = select_click_path[HAND_LEFT_INDEX]},
                        {.action = grab_action_float, .binding = select_click_path[HAND_RIGHT_INDEX]},
                        {.action = haptic_action, .binding = haptic_path[HAND_LEFT_INDEX]},
                        {.action = haptic_action, .binding = haptic_path[HAND_RIGHT_INDEX]},
                    };

                const XrInteractionProfileSuggestedBinding suggested_bindings = {
		                .type = XR_TYPE_INTERACTION_PROFILE_SUGGESTED_BINDING,
		                .next = NULL,
		                .interactionProfile = interaction_profile_path,
		                .countSuggestedBindings = sizeof(bindings) / sizeof(bindings[0]),
		                .suggestedBindings = bindings};

                xrSuggestInteractionProfileBindings(instance, &suggested_bindings);
                if (!xr_check(instance, result, "failed to suggest bindings"))
                    return 1;
            }

            // suggest actions for valve index controller
            {
                XrPath interaction_profile_path;
                result = xrStringToPath(instance, "/interaction_profiles/valve/index_controller",
                                        &interaction_profile_path);
                if (!xr_check(instance, result, "failed to get interaction profile"))
                    return 1;

                const XrActionSuggestedBinding bindings[] = {
                        {.action = hand_pose_action, .binding = grip_pose_path[HAND_LEFT_INDEX]},
                        {.action = hand_pose_action, .binding = grip_pose_path[HAND_RIGHT_INDEX]},
                        {.action = grab_action_float, .binding = trigger_value_path[HAND_LEFT_INDEX]},
                        {.action = grab_action_float, .binding = trigger_value_path[HAND_RIGHT_INDEX]},
                        {.action = haptic_action, .binding = haptic_path[HAND_LEFT_INDEX]},
                        {.action = haptic_action, .binding = haptic_path[HAND_RIGHT_INDEX]},
                    };

                const XrInteractionProfileSuggestedBinding suggested_bindings = {
		                .type = XR_TYPE_INTERACTION_PROFILE_SUGGESTED_BINDING,
		                .next = NULL,
		                .interactionProfile = interaction_profile_path,
		                .countSuggestedBindings = sizeof(bindings) / sizeof(bindings[0]),
		                .suggestedBindings = bindings};

                xrSuggestInteractionProfileBindings(instance, &suggested_bindings);
                if (!xr_check(instance, result, "failed to suggest bindings"))
                    return 1;
            }


            // TODO: should not be necessary, but is for SteamVR 1.16.4 (but not 1.15.x)
            glXMakeCurrent(graphics_binding_gl.xDisplay, graphics_binding_gl.glxDrawable,
                           graphics_binding_gl.glxContext);

            // Set up rendering (compile shaders, ...) before starting the session
            if (init_gl(view_count, swapchain_lengths, &gl_rendering.framebuffers,
                        &gl_rendering.shader_program_id, &gl_rendering.VAO) != 0)
            {
                printf("OpenGl setup failed!\n");
                return 1;
            }


            // --- Begin session */
            SessionBeginInfo session_begin_info = new SessionBeginInfo()
            {
                Type = StructureType.TypeSessionBeginInfo,
                PrimaryViewConfigurationType = view_type
            };

            CheckResult(Xr.BeginSession(session, &session_begin_info));

            /*XrSessionActionSetsAttachInfo actionset_attach_info = {
	                .type = XR_TYPE_SESSION_ACTION_SETS_ATTACH_INFO,
	                .next = NULL,
	                .countActionSets = 1,
	                .actionSets = &gameplay_actionset};
            result = xrAttachSessionActionSets(session, &actionset_attach_info);
            if (!xr_check(instance, result, "failed to attach action set"))
                return 1;
            */
        }

        internal Matrix createViewMatrix(Vector3 translation, Quaternion rotation)
        {
            Matrix rotationMatrix = Matrix.RotationQuaternion(rotation);
            Matrix translationMatrix = Matrix.Translation(translation);
            Matrix viewMatrix = translationMatrix * rotationMatrix;
            viewMatrix.Invert();
            return viewMatrix;
        }

        internal Matrix createProjectionFov(Fovf fov, float nearZ, float farZ)
        {
            Matrix result = Matrix.Identity;

            float tanAngleLeft = (float)Math.Tan(fov.AngleLeft);
            float tanAngleRight = (float)Math.Tan(fov.AngleRight);

            float tanAngleDown = (float)Math.Tan(fov.AngleDown);
            float tanAngleUp = (float)Math.Tan(fov.AngleUp);

            float tanAngleWidth = tanAngleRight - tanAngleLeft;
            float tanAngleHeight = (tanAngleUp - tanAngleDown);

            float offsetZ = 0;

	        if (farZ <= nearZ) {    
		        // place the far plane at infinity
		        result[0] = 2 / tanAngleWidth;
		        result[4] = 0;
		        result[8] = (tanAngleRight + tanAngleLeft) / tanAngleWidth;
		        result[12] = 0;

		        result[1] = 0;
		        result[5] = 2 / tanAngleHeight;
		        result[9] = (tanAngleUp + tanAngleDown) / tanAngleHeight;
		        result[13] = 0;

		        result[2] = 0;
		        result[6] = 0;
		        result[10] = -1;
		        result[14] = -(nearZ + offsetZ);

		        result[3] = 0;
		        result[7] = 0;
		        result[11] = -1;
		        result[15] = 0;
	        } else {
		        // normal projection
		        result[0] = 2 / tanAngleWidth;
		        result[4] = 0;
		        result[8] = (tanAngleRight + tanAngleLeft) / tanAngleWidth;
		        result[12] = 0;

		        result[1] = 0;
		        result[5] = 2 / tanAngleHeight;
		        result[9] = (tanAngleUp + tanAngleDown) / tanAngleHeight;
		        result[13] = 0;

		        result[2] = 0;
		        result[6] = 0;
		        result[10] = -(farZ + offsetZ) / (farZ - nearZ);
		        result[14] = -(farZ* (nearZ + offsetZ)) / (farZ - nearZ);

		        result[3] = 0;
		        result[7] = 0;
		        result[11] = -1;
		        result[15] = 0;
	        }

            return result;
        }

        public override void ReadEyeParameters(Eyes eye, float near, float far, ref Vector3 cameraPosition, ref Matrix cameraRotation, bool ignoreHeadRotation, bool ignoreHeadPosition, out Matrix view, out Matrix projection)
        {
            Matrix eyeMat, rot;
            Vector3 pos, scale;

            View eyeview = views[(int)eye];

            projection = createProjectionFov(eyeview.Fov, near, far);
            var adjustedHeadMatrix = createViewMatrix(new Vector3(-eyeview.Pose.Position.X, -eyeview.Pose.Position.Y, -eyeview.Pose.Position.Z),
                                                      ConvertToFocus(ref eyeview.Pose.Orientation));
            if (ignoreHeadPosition)
            {
                adjustedHeadMatrix.TranslationVector = Vector3.Zero;
            }
            if (ignoreHeadRotation)
            {
                // keep the scale just in case
                adjustedHeadMatrix.Row1 = new Vector4(adjustedHeadMatrix.Row1.Length(), 0, 0, 0);
                adjustedHeadMatrix.Row2 = new Vector4(0, adjustedHeadMatrix.Row2.Length(), 0, 0);
                adjustedHeadMatrix.Row3 = new Vector4(0, 0, adjustedHeadMatrix.Row3.Length(), 0);
            }

            eyeMat = adjustedHeadMatrix * Matrix.Scaling(BodyScaling) * cameraRotation * Matrix.Translation(cameraPosition);
            eyeMat.Decompose(out scale, out rot, out pos);
            var finalUp = Vector3.TransformCoordinate(new Vector3(0, 1, 0), rot);
            var finalForward = Vector3.TransformCoordinate(new Vector3(0, 0, -1), rot);
            view = Matrix.LookAtRH(pos, pos + finalForward, finalUp);
        }

        public override void Update(GameTime gameTime)
        {
            // update controller positions (should this be part of draw...?)
            //! @todo Move this action processing to before xrWaitFrame, probably.
            /*
            const XrActiveActionSet active_actionsets[] = {
            {.actionSet = gameplay_actionset, .subactionPath = XR_NULL_PATH}};

            XrActionsSyncInfo actions_sync_info = {
		    .type = XR_TYPE_ACTIONS_SYNC_INFO,
		    .countActiveActionSets = sizeof(active_actionsets) / sizeof(active_actionsets[0]),
		    .activeActionSets = active_actionsets,
        };
            result = xrSyncActions(session, &actions_sync_info);
            xr_check(instance, result, "failed to sync actions!");

            // query each value / location with a subaction path != XR_NULL_PATH
            // resulting in individual values per hand/.
            XrActionStateFloat grab_value[HAND_COUNT];
            XrSpaceLocation hand_locations[HAND_COUNT];

            for (int i = 0; i < HAND_COUNT; i++)
            {
                XrActionStatePose hand_pose_state = {.type = XR_TYPE_ACTION_STATE_POSE, .next = NULL };
                {
                    XrActionStateGetInfo get_info = {.type = XR_TYPE_ACTION_STATE_GET_INFO,
				                                 .next = NULL,
				                                 .action = hand_pose_action,
				                                 .subactionPath = hand_paths[i]};
                    result = xrGetActionStatePose(session, &get_info, &hand_pose_state);
                    xr_check(instance, result, "failed to get pose value!");
                }
                // printf("Hand pose %d active: %d\n", i, poseState.isActive);

                hand_locations[i].type = XR_TYPE_SPACE_LOCATION;
                hand_locations[i].next = NULL;

                result = xrLocateSpace(hand_pose_spaces[i], play_space, frame_state.predictedDisplayTime,
                                       &hand_locations[i]);
                xr_check(instance, result, "failed to locate space %d!", i);

                /*
                printf("Pose %d valid %d: %f %f %f %f, %f %f %f\n", i,
                spaceLocationValid[i], spaceLocation[0].pose.orientation.x,
                spaceLocation[0].pose.orientation.y, spaceLocation[0].pose.orientation.z,
                spaceLocation[0].pose.orientation.w, spaceLocation[0].pose.position.x,
                spaceLocation[0].pose.position.y, spaceLocation[0].pose.position.z
                );

                grab_value[i].type = XR_TYPE_ACTION_STATE_FLOAT;
                grab_value[i].next = NULL;
                {
                    XrActionStateGetInfo get_info = {.type = XR_TYPE_ACTION_STATE_GET_INFO,
				                                 .next = NULL,
				                                 .action = grab_action_float,
				                                 .subactionPath = hand_paths[i]};

                    result = xrGetActionStateFloat(session, &get_info, &grab_value[i]);
                    xr_check(instance, result, "failed to get grab value!");
                }

                // printf("Grab %d active %d, current %f, changed %d\n", i,
                // grabValue[i].isActive, grabValue[i].currentState,
                // grabValue[i].changedSinceLastSync);

                if (grab_value[i].isActive && grab_value[i].currentState > 0.75)
                {
                    XrHapticVibration vibration = {.type = XR_TYPE_HAPTIC_VIBRATION,
				                               .next = NULL,
				                               .amplitude = 0.5,
				                               .duration = XR_MIN_HAPTIC_DURATION,
				                               .frequency = XR_FREQUENCY_UNSPECIFIED};

                    XrHapticActionInfo haptic_action_info = {.type = XR_TYPE_HAPTIC_ACTION_INFO,
				                                         .next = NULL,
				                                         .action = haptic_action,
				                                         .subactionPath = hand_paths[i]};
                    result = xrApplyHapticFeedback(session, &haptic_action_info,
                                                   (const XrHapticBaseHeader*)&vibration);
            xr_check(instance, result, "failed to apply haptic feedback!");
            // printf("Sent haptic output to hand %d\n", i);
        }
        };

            */

        }
    }
}
