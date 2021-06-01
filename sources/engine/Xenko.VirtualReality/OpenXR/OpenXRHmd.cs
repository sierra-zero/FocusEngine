using System;
using System.Collections.Generic;
using System.Text;
using Xenko.Core.Mathematics;
using Xenko.Games;
using Xenko.Graphics;
using Silk.NET.OpenXR;
using System.Runtime.InteropServices;
using System.Linq;
using Silk.NET.Core.Native;

namespace Xenko.VirtualReality
{
    public class OpenXRHmd : VRDevice
    {
        private GameBase baseGame;

        private XR XRApi;
        private CompositionLayerProjectionView[] ProjectionViews;
        private Size2 renderSize;

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private unsafe delegate Result pfnGetVulkanGraphicsRequirementsKHR(Instance instance, ulong sys_id, GraphicsRequirementsVulkanKHR* req);

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
                if (XRApi == null) return DeviceState.Invalid;
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

        public override void Commit(CommandList commandList, Texture renderFrame)
        {
            throw new NotImplementedException();
        }

        public override void Draw(GameTime gameTime)
        {
            throw new NotImplementedException();
        }

        public override unsafe void Enable(GraphicsDevice device, GraphicsDeviceManager graphicsDeviceManager, bool requireMirror)
        {            
            // Changing to HANDHELD_DISPLAY or a future form factor may work, but has not been tested.
            FormFactor form_factor = FormFactor.HeadMountedDisplay;

            // Changing the form_factor may require changing the view_type too.
            ViewConfigurationType view_type = ViewConfigurationType.PrimaryStereo;

            // Typically STAGE for room scale/standing, LOCAL for seated
            ReferenceSpaceType play_space_type = ReferenceSpaceType.Local; //XR_REFERENCE_SPACE_TYPE_LOCAL;
            Space play_space;

            // the instance handle can be thought of as the basic connection to the OpenXR runtime
            Instance instance;
            // the system represents an (opaque) set of XR devices in use, managed by the runtime
            ulong system_id = 0;
            // the session deals with the renderloop submitting frames to the runtime
            Session session;

            // each physical Display/Eye is described by a view.
            // view_count usually depends on the form_factor / view_type.
            // dynamically allocating all view related structs instead of assuming 2
            // hopefully allows this app to scale easily to different view_counts.
            uint view_count = 0;
            // the viewconfiguration views contain information like resolution about each view
            ViewConfigurationView[] viewconfig_views;

            // array of view_count containers for submitting swapchains with rendered VR frames
            CompositionLayerProjectionView[] projection_views;
            // array of view_count views, filled by the runtime with current HMD display pose
            View[] views;

            // array of view_count handles for swapchains.
            // it is possible to use imageRect to render all views to different areas of the
            // same texture, but in this example we use one swapchain per view
            Swapchain[] swapchains;
            // array of view_count ints, storing the length of swapchains
            uint[] swapchain_lengths;
            // array of view_count array of swapchain_length containers holding an OpenGL texture
            // that is allocated by the runtime
            SwapchainImageVulkanKHR[][] images;

            // depth swapchain equivalent to the VR color swapchains
            Swapchain[] depth_swapchains;
            uint[] depth_swapchain_lengths;
            SwapchainImageVulkanKHR[][] depth_images;

            //Path hand_paths[HAND_COUNT];

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
            uint ext_count = 0;
            Span<ExtensionProperties> props = new Span<ExtensionProperties>(new ExtensionProperties[128]);
            XRApi = XR.GetApi();
            result = XRApi.EnumerateInstanceExtensionProperties(0, ref ext_count, props);

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


            // --- Create XrInstance
            uint enabled_ext_count = 2;
            List<string> enabled_exts = new List<string>() { "XR_KHR_vulkan_enable", "VK_KHR_image_format_list" };
            //string
            // same can be done for API layers, but API layers can also be enabled by env var

            var enabledExtensionNames = enabled_exts.Select(Marshal.StringToHGlobalAnsi).ToArray();

            fixed (void* enabledExtensionNamesPointer = &enabledExtensionNames[0])
            {
                InstanceCreateInfo instance_create_info = new InstanceCreateInfo()
                {
                    Type = StructureType.TypeInstanceCreateInfo,
                    Next = (void*)0,
                    CreateFlags = 0,
                    EnabledExtensionCount = enabled_ext_count,
                    EnabledExtensionNames = (byte**)enabledExtensionNamesPointer,
                    EnabledApiLayerCount = 0,
                    EnabledApiLayerNames = (byte**)0,
                    ApplicationInfo = new ApplicationInfo(),
                };

                result = XRApi.CreateInstance(&instance_create_info, &instance);
            }

            // --- Get XrSystemId
            SystemGetInfo system_get_info = new SystemGetInfo() {
	            Type = StructureType.TypeSystemGetInfo,
                FormFactor = form_factor
            };

            result = XRApi.GetSystem(instance, &system_get_info, ref system_id);

            {
                SystemProperties system_props = new SystemProperties() {
		            Type = StructureType.TypeSystemProperties,
                };

                result = XRApi.GetSystemProperties(instance, system_id, &system_props);
            }

            ViewConfigurationView vcv = new ViewConfigurationView()
            {
                Type = StructureType.TypeViewConfigurationView,                 
            };

            viewconfig_views = new ViewConfigurationView[128];
            fixed (ViewConfigurationView* viewspnt = &viewconfig_views[0])
                result = XRApi.EnumerateViewConfigurationView(instance, system_id, view_type, 0, ref view_count, viewspnt);
            Array.Resize<ViewConfigurationView>(ref viewconfig_views, (int)view_count);

            // get size
            renderSize.Height = (int)viewconfig_views[0].RecommendedImageRectHeight;
            renderSize.Width = (int)viewconfig_views[0].RecommendedImageRectWidth;

            // OpenXR requires checking graphics requirements before creating a session.
            GraphicsRequirementsOpenGLKHR opengl_reqs = new GraphicsRequirementsOpenGLKHR()
            {
                Type = StructureType.TypeGraphicsRequirementsVulkanKhr
            };

            // this function pointer was loaded with xrGetInstanceProcAddr
            Silk.NET.Core.PfnVoidFunction func = new Silk.NET.Core.PfnVoidFunction();
            GraphicsRequirementsVulkanKHR vulk = new GraphicsRequirementsVulkanKHR();
            result = XRApi.GetInstanceProcAddr(instance, "pfnGetVulkanGraphicsRequirementsKHR", ref func);
            Delegate vulk_req = Marshal.GetDelegateForFunctionPointer((IntPtr)func.Handle, typeof(pfnGetVulkanGraphicsRequirementsKHR));
            vulk_req.DynamicInvoke(instance, system_id, (ulong)&vulk);

            // Checking opengl_reqs.minApiVersionSupported and opengl_reqs.maxApiVersionSupported
            // is not very useful, compatibility will depend on the OpenGL implementation and the
            // OpenXR runtime much more than the OpenGL version.
            // Other APIs have more useful verifiable requirements.

#if XENKO_GRAPHICS_API_VULKAN
            // --- Create session
            var graphics_binding_vulkan = new GraphicsBindingVulkanKHR(){
	            Type = StructureType.TypeGraphicsBindingVulkanKhr,
                Device = new VkHandle((nint)device.NativeDevice.Handle),
                Instance = new VkHandle((nint)device.NativeInstance.Handle),
                PhysicalDevice = new VkHandle((nint)device.NativePhysicalDevice.Handle),
                QueueFamilyIndex = 0,
                QueueIndex = 0,
	        };
#else
            GraphicsBindingVulkanKHR graphics_binding_vulkan = new GraphicsBindingVulkanKHR();
            throw new Exception("OpenXR is only compatible with Vulkan");
#endif

            SessionCreateInfo session_create_info = new SessionCreateInfo() {
	            Type = StructureType.TypeSessionCreateInfo,
                Next = &graphics_binding_vulkan,
                SystemId = system_id
            };

            result = XRApi.CreateSession(instance, &session_create_info, &session);

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

            result = XRApi.CreateReferenceSpace(session, &play_space_create_info, &play_space);

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
                swapchains = new Swapchain[view_count]; //malloc(sizeof(XrSwapchain) * view_count);
                swapchain_lengths = new uint[view_count]; //malloc(sizeof(uint32_t) * view_count);
                images = new SwapchainImageVulkanKHR[view_count][]; //malloc(sizeof(SwapchainImageVulkanKHR*) * view_count);
                for (int i = 0; i < view_count; i++)
                {
                    SwapchainCreateInfo swapchain_create_info = new SwapchainCreateInfo() {
			            Type = StructureType.TypeSwapchainCreateInfo,
			            UsageFlags = SwapchainUsageFlags.SwapchainUsageSampledBit | SwapchainUsageFlags.SwapchainUsageColorAttachmentBit, //XR_SWAPCHAIN_USAGE_SAMPLED_BIT | XR_SWAPCHAIN_USAGE_COLOR_ATTACHMENT_BIT,
			            CreateFlags = 0,
			            Format = (long)PixelFormat.R8G8B8A8_UNorm_SRgb,
			            SampleCount = viewconfig_views[i].RecommendedSwapchainSampleCount,
			            Width = viewconfig_views[i].RecommendedImageRectWidth,
			            Height = viewconfig_views[i].RecommendedImageRectHeight,
			            FaceCount = 1,
			            ArraySize = 1,
			            MipCount = 1,
                    };

                    fixed (Swapchain* scp = &swapchains[i])
                        result = XRApi.CreateSwapchain(session, &swapchain_create_info, scp);

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
                    images[i] = new SwapchainImageVulkanKHR[32];

                    fixed (uint* lenp = &swapchain_lengths[i]) {
                        fixed (void* sibhp = &images[i][0]) {
                        result =
                            XRApi.EnumerateSwapchainImages(swapchains[i], swapchain_lengths[i], lenp, (SwapchainImageBaseHeader*)sibhp);
                        }
                    }
                    Array.Resize(ref images[i], (int)swapchain_lengths[i]);
                }
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

                projection_views[i].SubImage.Swapchain = swapchains[i];
                projection_views[i].SubImage.ImageArrayIndex = 0;
                projection_views[i].SubImage.ImageRect.Offset.X = 0;
                projection_views[i].SubImage.ImageRect.Offset.Y = 0;
                projection_views[i].SubImage.ImageRect.Extent.Width = (int)viewconfig_views[i].RecommendedImageRectWidth;
                projection_views[i].SubImage.ImageRect.Extent.Height = (int)viewconfig_views[i].RecommendedImageRectHeight;

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

            /*xrStringToPath(instance, "/user/hand/left", &hand_paths[HAND_LEFT_INDEX]);
            xrStringToPath(instance, "/user/hand/right", &hand_paths[HAND_RIGHT_INDEX]);

            XrPath select_click_path[HAND_COUNT];
            xrStringToPath(instance, "/user/hand/left/input/select/click",
                           &select_click_path[HAND_LEFT_INDEX]);
            xrStringToPath(instance, "/user/hand/right/input/select/click",
                           &select_click_path[HAND_RIGHT_INDEX]);

            XrPath trigger_value_path[HAND_COUNT];
            xrStringToPath(instance, "/user/hand/left/input/trigger/value",
                           &trigger_value_path[HAND_LEFT_INDEX]);
            xrStringToPath(instance, "/user/hand/right/input/trigger/value",
                           &trigger_value_path[HAND_RIGHT_INDEX]);

            XrPath thumbstick_y_path[HAND_COUNT];
            xrStringToPath(instance, "/user/hand/left/input/thumbstick/y",
                           &thumbstick_y_path[HAND_LEFT_INDEX]);
            xrStringToPath(instance, "/user/hand/right/input/thumbstick/y",
                           &thumbstick_y_path[HAND_RIGHT_INDEX]);

            XrPath grip_pose_path[HAND_COUNT];
            xrStringToPath(instance, "/user/hand/left/input/grip/pose", &grip_pose_path[HAND_LEFT_INDEX]);
            xrStringToPath(instance, "/user/hand/right/input/grip/pose", &grip_pose_path[HAND_RIGHT_INDEX]);

            XrPath haptic_path[HAND_COUNT];
            xrStringToPath(instance, "/user/hand/left/output/haptic", &haptic_path[HAND_LEFT_INDEX]);
            xrStringToPath(instance, "/user/hand/right/output/haptic", &haptic_path[HAND_RIGHT_INDEX]);


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


            // --- Begin session
            XrSessionBeginInfo session_begin_info = {
	                .type = XR_TYPE_SESSION_BEGIN_INFO, .next = NULL, .primaryViewConfigurationType = view_type};
            result = xrBeginSession(session, &session_begin_info);
            if (!xr_check(instance, result, "Failed to begin session!"))
                return 1;
            printf("Session started!\n");

            XrSessionActionSetsAttachInfo actionset_attach_info = {
	                .type = XR_TYPE_SESSION_ACTION_SETS_ATTACH_INFO,
	                .next = NULL,
	                .countActionSets = 1,
	                .actionSets = &gameplay_actionset};
            result = xrAttachSessionActionSets(session, &actionset_attach_info);
            if (!xr_check(instance, result, "failed to attach action set"))
                return 1;

            *
            */
        }

        public override void ReadEyeParameters(Eyes eye, float near, float far, ref Vector3 cameraPosition, ref Matrix cameraRotation, bool ignoreHeadRotation, bool ignoreHeadPosition, out Matrix view, out Matrix projection)
        {
            //TODO
            view = Matrix.Identity;
            projection = Matrix.Identity;
        }

        public override void Update(GameTime gameTime)
        {
            //TODO
        }
    }
}
