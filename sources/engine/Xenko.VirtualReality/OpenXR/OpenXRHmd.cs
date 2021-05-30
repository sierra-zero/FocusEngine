using System;
using System.Collections.Generic;
using System.Text;
using Xenko.Core.Mathematics;
using Xenko.Games;
using Xenko.Graphics;
using Silk.NET.OpenXR;
using System.Runtime.InteropServices;
using System.Linq;

namespace Xenko.VirtualReality
{
    public class OpenXRHmd : VRDevice
    {
        private GameBase baseGame;
        private XR XRApi;

        public OpenXRHmd(GameBase game)
        {
            baseGame = game;
        }

        public override Size2 OptimalRenderFrameSize => throw new NotImplementedException();

        public override Size2 ActualRenderFrameSize { get => throw new NotImplementedException(); protected set => throw new NotImplementedException(); }
        public override Texture MirrorTexture { get => throw new NotImplementedException(); protected set => throw new NotImplementedException(); }
        public override float RenderFrameScaling { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public override DeviceState State => throw new NotImplementedException();

        public override Vector3 HeadPosition => throw new NotImplementedException();

        public override Quaternion HeadRotation => throw new NotImplementedException();

        public override Vector3 HeadLinearVelocity => throw new NotImplementedException();

        public override Vector3 HeadAngularVelocity => throw new NotImplementedException();

        public override TouchController LeftHand => throw new NotImplementedException();

        public override TouchController RightHand => throw new NotImplementedException();

        public override TrackedItem[] TrackedItems => throw new NotImplementedException();

        public override ulong PoseCount => throw new NotImplementedException();

        public override bool CanInitialize => throw new NotImplementedException();

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
            //SystemId system_id;
            // the session deals with the renderloop submitting frames to the runtime
            Session session;

            // each graphics API requires the use of a specialized struct
            GraphicsBindingOpenGLXlibKHR graphics_binding_gl;

            // each physical Display/Eye is described by a view.
            // view_count usually depends on the form_factor / view_type.
            // dynamically allocating all view related structs instead of assuming 2
            // hopefully allows this app to scale easily to different view_counts.
            uint view_count = 0;
            // the viewconfiguration views contain information like resolution about each view
            ViewConfigurationView viewconfig_views;

            // array of view_count containers for submitting swapchains with rendered VR frames
            CompositionLayerProjectionView projection_views;
            // array of view_count views, filled by the runtime with current HMD display pose
            View views;

            // array of view_count handles for swapchains.
            // it is possible to use imageRect to render all views to different areas of the
            // same texture, but in this example we use one swapchain per view
            Swapchain swapchains;
            // array of view_count ints, storing the length of swapchains
            uint swapchain_lengths;
            // array of view_count array of swapchain_length containers holding an OpenGL texture
            // that is allocated by the runtime
            SwapchainImageOpenGLKHR images;

            // depth swapchain equivalent to the VR color swapchains
            Swapchain depth_swapchains;
            uint depth_swapchain_lengths;
            SwapchainImageOpenGLKHR depth_images;

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
            /*XrSystemGetInfo system_get_info = {
	                .type = XR_TYPE_SYSTEM_GET_INFO, .formFactor = form_factor, .next = NULL};

            result = xrGetSystem(instance, &system_get_info, &system_id);
            if (!xr_check(instance, result, "Failed to get system for HMD form factor."))
                return 1;

            printf("Successfully got XrSystem with id %lu for HMD form factor\n", system_id);


            {
                XrSystemProperties system_props = {
		                .type = XR_TYPE_SYSTEM_PROPERTIES,
		                .next = NULL,
                    };

                result = xrGetSystemProperties(instance, system_id, &system_props);
                if (!xr_check(instance, result, "Failed to get System properties"))
                    return 1;

                print_system_properties(&system_props);
            }

            result = xrEnumerateViewConfigurationViews(instance, system_id, view_type, 0, &view_count, NULL);
            if (!xr_check(instance, result, "Failed to get view configuration view count!"))
                return 1;

            viewconfig_views = malloc(sizeof(XrViewConfigurationView) * view_count);
            for (uint32_t i = 0; i < view_count; i++)
            {
                viewconfig_views[i].type = XR_TYPE_VIEW_CONFIGURATION_VIEW;
                viewconfig_views[i].next = NULL;
            }

            result = xrEnumerateViewConfigurationViews(instance, system_id, view_type, view_count,
                                                       &view_count, viewconfig_views);
            if (!xr_check(instance, result, "Failed to enumerate view configuration views!"))
                return 1;
            print_viewconfig_view_info(view_count, viewconfig_views);


            // OpenXR requires checking graphics requirements before creating a session.
            XrGraphicsRequirementsOpenGLKHR opengl_reqs = {.type = XR_TYPE_GRAPHICS_REQUIREMENTS_OPENGL_KHR,
	                                                           .next = NULL};

            // this function pointer was loaded with xrGetInstanceProcAddr
            result = pfnGetOpenGLGraphicsRequirementsKHR(instance, system_id, &opengl_reqs);
            if (!xr_check(instance, result, "Failed to get OpenGL graphics requirements!"))
                return 1;

            // Checking opengl_reqs.minApiVersionSupported and opengl_reqs.maxApiVersionSupported
            // is not very useful, compatibility will depend on the OpenGL implementation and the
            // OpenXR runtime much more than the OpenGL version.
            // Other APIs have more useful verifiable requirements.


            // --- Create session
            graphics_binding_gl = (XrGraphicsBindingOpenGLXlibKHR){
	                .type = XR_TYPE_GRAPHICS_BINDING_OPENGL_XLIB_KHR,
	            };

            // create SDL window the size of the left eye & fill GL graphics binding info
            if (!init_sdl_window(&graphics_binding_gl.xDisplay, &graphics_binding_gl.visualid,
                                 &graphics_binding_gl.glxFBConfig, &graphics_binding_gl.glxDrawable,
                                 &graphics_binding_gl.glxContext,
                                 viewconfig_views[0].recommendedImageRectWidth,
                                 viewconfig_views[0].recommendedImageRectHeight))
            {
                printf("GLX init failed!\n");
                return 1;
            }

            printf("Using OpenGL version: %s\n", glGetString(GL_VERSION));
            printf("Using OpenGL Renderer: %s\n", glGetString(GL_RENDERER));

            XrSessionCreateInfo session_create_info = {
	                .type = XR_TYPE_SESSION_CREATE_INFO, .next = &graphics_binding_gl, .systemId = system_id};

            result = xrCreateSession(instance, &session_create_info, &session);
            if (!xr_check(instance, result, "Failed to create session"))
                return 1;

            printf("Successfully created a session with OpenGL!\n");

            // Many runtimes support at least STAGE and LOCAL but not all do.
            // Sophisticated apps might check with xrEnumerateReferenceSpaces() if the
            // chosen one is supported and try another one if not.
            // Here we will get an error from xrCreateReferenceSpace() and exit.
            XrReferenceSpaceCreateInfo play_space_create_info = {.type = XR_TYPE_REFERENCE_SPACE_CREATE_INFO,
	                                                                 .next = NULL,
	                                                                 .referenceSpaceType = play_space_type,
	                                                                 .poseInReferenceSpace = identity_pose};

            result = xrCreateReferenceSpace(session, &play_space_create_info, &play_space);
            if (!xr_check(instance, result, "Failed to create play space!"))
                return 1;

            // --- Create Swapchains
            uint32_t swapchain_format_count;
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
            }


            // --- Create swapchain for main VR rendering
            {
                // In the frame loop we render into OpenGL textures we receive from the runtime here.
                swapchains = malloc(sizeof(XrSwapchain) * view_count);
                swapchain_lengths = malloc(sizeof(uint32_t) * view_count);
                images = malloc(sizeof(XrSwapchainImageOpenGLKHR*) * view_count);
                for (uint32_t i = 0; i < view_count; i++)
                {
                    XrSwapchainCreateInfo swapchain_create_info = {
			                .type = XR_TYPE_SWAPCHAIN_CREATE_INFO,
			                .usageFlags = XR_SWAPCHAIN_USAGE_SAMPLED_BIT | XR_SWAPCHAIN_USAGE_COLOR_ATTACHMENT_BIT,
			                .createFlags = 0,
			                .format = color_format,
			                .sampleCount = viewconfig_views[i].recommendedSwapchainSampleCount,
			                .width = viewconfig_views[i].recommendedImageRectWidth,
			                .height = viewconfig_views[i].recommendedImageRectHeight,
			                .faceCount = 1,
			                .arraySize = 1,
			                .mipCount = 1,
			                .next = NULL,
                        };

                    result = xrCreateSwapchain(session, &swapchain_create_info, &swapchains[i]);
                    if (!xr_check(instance, result, "Failed to create swapchain %d!", i))
                        return 1;

                    // The runtime controls how many textures we have to be able to render to
                    // (e.g. "triple buffering")
                    result = xrEnumerateSwapchainImages(swapchains[i], 0, &swapchain_lengths[i], NULL);
                    if (!xr_check(instance, result, "Failed to enumerate swapchains"))
                        return 1;

                    images[i] = malloc(sizeof(XrSwapchainImageOpenGLKHR) * swapchain_lengths[i]);
                    for (uint32_t j = 0; j < swapchain_lengths[i]; j++)
                    {
                        images[i][j].type = XR_TYPE_SWAPCHAIN_IMAGE_OPENGL_KHR;
                        images[i][j].next = NULL;
                    }
                    result =
                        xrEnumerateSwapchainImages(swapchains[i], swapchain_lengths[i], &swapchain_lengths[i],
                                                   (XrSwapchainImageBaseHeader*)images[i]);
                    if (!xr_check(instance, result, "Failed to enumerate swapchain images"))
                        return 1;
                }
            }

            // --- Create swapchain for depth buffers if supported
            {
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
            }


            // Do not allocate these every frame to save some resources
            views = (XrView*)malloc(sizeof(XrView) * view_count);
            for (uint32_t i = 0; i < view_count; i++)
            {
                views[i].type = XR_TYPE_VIEW;
                views[i].next = NULL;
            }

            projection_views = (XrCompositionLayerProjectionView*)malloc(
                sizeof(XrCompositionLayerProjectionView) * view_count);
            for (uint32_t i = 0; i < view_count; i++)
            {
                projection_views[i].type = XR_TYPE_COMPOSITION_LAYER_PROJECTION_VIEW;
                projection_views[i].next = NULL;

                projection_views[i].subImage.swapchain = swapchains[i];
                projection_views[i].subImage.imageArrayIndex = 0;
                projection_views[i].subImage.imageRect.offset.x = 0;
                projection_views[i].subImage.imageRect.offset.y = 0;
                projection_views[i].subImage.imageRect.extent.width =
                    viewconfig_views[i].recommendedImageRectWidth;
                projection_views[i].subImage.imageRect.extent.height =
                    viewconfig_views[i].recommendedImageRectHeight;

                // projection_views[i].{pose, fov} have to be filled every frame in frame loop
            };


            if (depth.supported)
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
            }


            // --- Set up input (actions)

            xrStringToPath(instance, "/user/hand/left", &hand_paths[HAND_LEFT_INDEX]);
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
            throw new NotImplementedException();
        }

        public override void Update(GameTime gameTime)
        {
            throw new NotImplementedException();
        }
    }
}
