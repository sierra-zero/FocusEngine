// Copyright (c) Xenko contributors (https://xenko.com) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.
#if XENKO_GRAPHICS_API_VULKAN
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;
using Xenko.Core;

namespace Xenko.Graphics
{
    public static partial class GraphicsAdapterFactory
    {
        private static GraphicsAdapterFactoryInstance defaultInstance;

#if DEBUG
        private static GraphicsAdapterFactoryInstance debugInstance;
#endif

        /// <summary>
        /// Initializes all adapters with the specified factory.
        /// </summary>
        internal static void InitializeInternal(bool debug)
        {
            var result = vkInitialize();
            result.CheckResult();

            // Create the default instance to enumerate physical devices
            defaultInstance = new GraphicsAdapterFactoryInstance(debug);
            var nativePhysicalDevices = vkEnumeratePhysicalDevices(defaultInstance.NativeInstance);

            var adapterList = new List<GraphicsAdapter>();
            for (int i = 0; i < nativePhysicalDevices.Length; i++)
            {
                var adapter = new GraphicsAdapter(nativePhysicalDevices[i], i);
                staticCollector.Add(adapter);
                adapterList.Add(adapter);
            }

            defaultAdapter = adapterList.Count > 0 ? adapterList[0] : null;
            adapters = adapterList.ToArray();

            staticCollector.Add(new AnonymousDisposable(Cleanup));
        }

        private static void Cleanup()
        {
            if (defaultInstance != null)
            {
                defaultInstance.Dispose();
                defaultInstance = null;
            }

#if DEBUG
            if (debugInstance != null)
            {
                debugInstance.Dispose();
                debugInstance = null;
            }
#endif
        }

        /// <summary>
        /// Gets the <see cref="GraphicsAdapterFactoryInstance"/> used by all GraphicsAdapter.
        /// </summary>
        internal static GraphicsAdapterFactoryInstance GetInstance(bool enableValidation)
        {
            lock (StaticLock)
            {
                Initialize();

#if DEBUG
                if (enableValidation)
                {
                    return debugInstance ?? (debugInstance = new GraphicsAdapterFactoryInstance(true));
                }
                else
                {
                    return defaultInstance;
                }
#else
                return defaultInstance;
#endif
            }
        }
    }

    internal class GraphicsAdapterFactoryInstance : IDisposable
    {
#if DEBUG
        private VkDebugReportCallbackEXT debugReportCallback;
        private DebugReportCallbackDelegate debugReport;
        internal BeginDebugMarkerDelegate BeginDebugMarker;
        internal EndDebugMarkerDelegate EndDebugMarker;
#endif

        internal VkInstance NativeInstance;

        public unsafe GraphicsAdapterFactoryInstance(bool enableValidation)
        {
            var applicationInfo = new VkApplicationInfo
            {
                sType = VkStructureType.ApplicationInfo,
                apiVersion = new VkVersion(1, 0, 0),
                pEngineName = (byte*)Marshal.StringToHGlobalAnsi("Focus"),
                //EngineVersion = new SharpVulkan.Version()
            };

            IntPtr[] enabledLayerNames = new IntPtr[0];

#if DEBUG
            if (enableValidation)
            {
                var desiredLayerNames = new[]
                {
                    "VK_LAYER_KHRONOS_validation",
                };

                var layers = vkEnumerateInstanceLayerProperties();
                var availableLayerNames = new HashSet<string>();

                for (int index = 0; index < layers.Length; index++)
                {
                    var properties = layers[index];
                    var namePointer = properties.layerName;
                    var name = Marshal.PtrToStringAnsi((IntPtr)namePointer);

                    availableLayerNames.Add(name);
                }

                enabledLayerNames = desiredLayerNames
                    .Where(x => availableLayerNames.Contains(x))
                    .Select(Marshal.StringToHGlobalAnsi).ToArray();
            }
#endif

            var extensionProperties = vkEnumerateInstanceExtensionProperties();
            var availableExtensionNames = new List<string>();
            var desiredExtensionNames = new List<string>();

            for (int index = 0; index < extensionProperties.Length; index++)
            {
                var extensionProperty = extensionProperties[index];
                var name = Marshal.PtrToStringAnsi((IntPtr)extensionProperty.extensionName);
                availableExtensionNames.Add(name);
            }

            desiredExtensionNames.Add("VK_KHR_get_physical_device_properties2");
            desiredExtensionNames.Add("VK_KHR_external_semaphore_capabilities");
            desiredExtensionNames.Add("VK_KHR_external_semaphore");
            desiredExtensionNames.Add("VK_KHR_surface");
            desiredExtensionNames.Add("VK_KHR_win32_surface"); // windows
            desiredExtensionNames.Add("VK_KHR_android_surface"); // android
            desiredExtensionNames.Add("VK_KHR_xlib_surface"); // linux
            desiredExtensionNames.Add("VK_KHR_xcb_surface"); // linux
            desiredExtensionNames.Add("VK_MVK_macos_surface"); // macos
            desiredExtensionNames.Add("VK_EXT_metal_surface"); // macos
            desiredExtensionNames.Add("VK_MVK_moltenvk"); // macos
            desiredExtensionNames.Add("VK_NV_external_memory_capabilities"); // NVIDIA needs this one for OpenVR
            desiredExtensionNames.Add("VK_KHR_external_memory_capabilities"); // this one might be used in the future for OpenVR

#if DEBUG
            bool enableDebugReport = enableValidation && availableExtensionNames.Contains("VK_EXT_debug_report");
            if (enableDebugReport)
                desiredExtensionNames.Add("VK_EXT_debug_report");
#endif

            // take out any extensions not supported
            for (int i=0; i<desiredExtensionNames.Count; i++)
            {
                if (availableExtensionNames.Contains(desiredExtensionNames[i]) == false)
                {
                    desiredExtensionNames.RemoveAt(i);
                    i--;
                }
            }

            var enabledExtensionNames = desiredExtensionNames.Select(Marshal.StringToHGlobalAnsi).ToArray();

            try
            {
                fixed (void* enabledExtensionNamesPointer = &enabledExtensionNames[0])
                {
                    var instanceCreateInfo = new VkInstanceCreateInfo
                    {
                        sType = VkStructureType.InstanceCreateInfo,
                        pApplicationInfo = &applicationInfo,
                        enabledLayerCount = enabledLayerNames != null ? (uint)enabledLayerNames.Length : 0,
                        ppEnabledLayerNames = enabledLayerNames?.Length > 0 ? (byte**)Core.Interop.Fixed(enabledLayerNames) : null,
                        enabledExtensionCount = (uint)enabledExtensionNames.Length,
                        ppEnabledExtensionNames = (byte**)enabledExtensionNamesPointer,
                    };

                    vkCreateInstance(&instanceCreateInfo, null, out NativeInstance);
                    vkLoadInstance(NativeInstance);
                }

#if DEBUG
                if (enableDebugReport)
                {
                    var createDebugReportCallbackName = Marshal.StringToHGlobalAnsi("vkCreateDebugReportCallbackEXT");
                    var createDebugReportCallback = (CreateDebugReportCallbackDelegate)Marshal.GetDelegateForFunctionPointer(vkGetInstanceProcAddr(NativeInstance, (byte*)createDebugReportCallbackName), typeof(CreateDebugReportCallbackDelegate));

                    debugReport = DebugReport;
                    var createInfo = new VkDebugReportCallbackCreateInfoEXT
                    {
                        sType = VkStructureType.DebugReportCallbackCreateInfoEXT,
                        flags = VkDebugReportFlagsEXT.Error | VkDebugReportFlagsEXT.Warning /* | VkDebugReportFlagsEXT.PerformanceWarningEXT | VkDebugReportFlagsEXT.InformationEXT | VkDebugReportFlagsEXT.DebugEXT*/,
                        pfnCallback = Marshal.GetFunctionPointerForDelegate(debugReport)
                    };
                    createDebugReportCallback(NativeInstance, ref createInfo, null, out debugReportCallback);
                    Marshal.FreeHGlobal(createDebugReportCallbackName);
                }

                if (availableExtensionNames.Contains("VK_EXT_debug_marker"))
                {
                    var beginDebugMarkerName = System.Text.Encoding.ASCII.GetBytes("vkCmdDebugMarkerBeginEXT");

                    var ptr = vkGetInstanceProcAddr(NativeInstance, (byte*)Core.Interop.Fixed(beginDebugMarkerName));
                    if (ptr != IntPtr.Zero)
                        BeginDebugMarker = (BeginDebugMarkerDelegate)Marshal.GetDelegateForFunctionPointer(ptr, typeof(BeginDebugMarkerDelegate));

                    var endDebugMarkerName = System.Text.Encoding.ASCII.GetBytes("vkCmdDebugMarkerEndEXT");
                    ptr = vkGetInstanceProcAddr(NativeInstance, (byte*)Core.Interop.Fixed(endDebugMarkerName));
                    if (ptr != IntPtr.Zero)
                        EndDebugMarker = (EndDebugMarkerDelegate)Marshal.GetDelegateForFunctionPointer(ptr, typeof(EndDebugMarkerDelegate));
                }
#endif
            }
            finally
            {
                foreach (var enabledExtensionName in enabledExtensionNames)
                {
                    Marshal.FreeHGlobal(enabledExtensionName);
                }

                foreach (var enabledLayerName in enabledLayerNames)
                {
                    Marshal.FreeHGlobal(enabledLayerName);
                }

                Marshal.FreeHGlobal((IntPtr)applicationInfo.pEngineName);
            }
        }

#if DEBUG
        private static bool DebugReport(VkDebugReportFlagsEXT flags, VkDebugReportObjectTypeEXT objectType, ulong @object, nuint location, int messageCode, string layerPrefix, string message, IntPtr userData)
        {
            string debugMessage = $"{flags}: {message} ([{messageCode}] {layerPrefix})";
            Debug.WriteLine(debugMessage);
            if (GraphicsAdapterFactory.adapterFlags == DeviceCreationFlags.DebugAndBreak)
                Debugger.Break();
            return false;
        }
#endif

        public unsafe void Dispose()
        {
#if DEBUG
            if (debugReportCallback != VkDebugReportCallbackEXT.Null)
            {
                vkDestroyDebugReportCallbackEXT(NativeInstance, debugReportCallback, null);
            }
#endif

            vkDestroyInstance(NativeInstance, null);
        }

#if DEBUG
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        internal unsafe delegate void BeginDebugMarkerDelegate(VkCommandBuffer commandBuffer, VkDebugMarkerMarkerInfoEXT* markerInfo);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        internal delegate void EndDebugMarkerDelegate(VkCommandBuffer commandBuffer);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        private delegate bool DebugReportCallbackDelegate(VkDebugReportFlagsEXT flags, VkDebugReportObjectTypeEXT objectType, ulong @object, nuint location, int messageCode, string layerPrefix, string message, IntPtr userData);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private unsafe delegate VkResult CreateDebugReportCallbackDelegate(VkInstance instance, ref VkDebugReportCallbackCreateInfoEXT createInfo, VkAllocationCallbacks* allocator, out VkDebugReportCallbackEXT callback);
#endif
    }
}
#endif 
