// Copyright (c) Xenko contributors (https://xenko.com) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.
#if XENKO_GRAPHICS_API_VULKAN
using System;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace Xenko.Graphics
{
    /// <summary>
    /// GraphicsResource class
    /// </summary>
    public abstract partial class GraphicsResource
    {
        internal VkDeviceMemory NativeMemory;
        internal long? StagingFenceValue;
        internal CommandList StagingBuilder;
        internal VkPipelineStageFlags NativePipelineStageMask;

        protected bool IsDebugMode
        {
            get
            {
                return GraphicsDevice != null && GraphicsDevice.IsDebugMode;
            }
        }

        protected override unsafe void OnNameChanged()
        {
            base.OnNameChanged();
            //if (GraphicsDevice != null && GraphicsDevice.IsProfilingSupported)
            //{
            //    if (string.IsNullOrEmpty(Name))
            //        return;

            //    var bytes = System.Text.Encoding.ASCII.GetBytes(Name);

            //    fixed (byte* bytesPointer = &bytes[0])
            //    {
            //        var nameInfo = new DebugMarkerObjectNameInfo
            //        {
            //            sType = VkStructureType.DebugMarkerObjectNameInfo,
            //            Object = ,
            //            ObjectName = new IntPtr(bytesPointer),
            //            ObjectType = 
            //        };
            //        GraphicsDevice.NativeDevice.DebugMarkerSetObjectName(ref nameInfo);
            //    }
            //}
        }

        internal static VkPhysicalDeviceMemoryProperties physicalDeviceMemoryProperties;
        internal static bool gotProps = false;
        protected unsafe void AllocateMemory(VkMemoryPropertyFlags memoryProperties, VkMemoryRequirements memoryRequirements)
        {
            if (NativeMemory != VkDeviceMemory.Null)
                return;

            if (memoryRequirements.size == 0)
                return;

            var allocateInfo = new VkMemoryAllocateInfo
            {
                sType = VkStructureType.MemoryAllocateInfo,
                allocationSize = memoryRequirements.size,
            };

            VkPhysicalDeviceMemoryProperties localProps;
            if (!gotProps) 
            {
                gotProps = true;
                vkGetPhysicalDeviceMemoryProperties(GraphicsDevice.NativePhysicalDevice, out physicalDeviceMemoryProperties);
            }
            localProps = physicalDeviceMemoryProperties;

            var typeBits = memoryRequirements.memoryTypeBits;
            for (uint i = 0; i < localProps.memoryTypeCount; i++)
            {
                if ((typeBits & 1) == 1)
                {
                    // Type is available, does it match user properties?
                    var memoryType = *(&localProps.memoryTypes_0 + i);
                    if ((memoryType.propertyFlags & memoryProperties) == memoryProperties)
                    {
                        allocateInfo.memoryTypeIndex = i;
                        break;
                    }
                }
                typeBits >>= 1;
            }

            fixed (VkDeviceMemory* nativeMemoryPtr = &NativeMemory)
            {
               var result = vkAllocateMemory(GraphicsDevice.NativeDevice, &allocateInfo, null, nativeMemoryPtr);

               if (result != VkResult.Success)
               {
                    string err = "Couldn't allocate memory: " + result + ", NativeMemory: " + NativeMemory + ", type: " + allocateInfo.memoryTypeIndex + ", size: " + memoryRequirements.size;
                    Xenko.Core.ErrorFileLogger.WriteLogToFile(err);
                    throw new Exception(err);
               }
            }
        }
    }
}

#endif
