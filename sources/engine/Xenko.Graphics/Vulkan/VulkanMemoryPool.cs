#if XENKO_GRAPHICS_API_VULKAN

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using Vortice.Vulkan;

namespace Xenko.Graphics
{
    public class VulkanMemoryPool
    {
        internal static VkDeviceMemory BigMemoryPool = VkDeviceMemory.Null, SmallMemoryPool = VkDeviceMemory.Null;
        internal static ConcurrentQueue<ulong> FreeBig = new ConcurrentQueue<ulong>(), FreeSmall = new ConcurrentQueue<ulong>();

        internal static unsafe void AllocatePool(out VkDeviceMemory NativeMemory, ref VkDevice device, ref VkPhysicalDevice pDevice, ref VkMemoryPropertyFlags memoryProperties, ref VkMemoryRequirements memoryRequirements, ulong size)
        {
            var allocateInfo = new VkMemoryAllocateInfo
            {
                sType = VkStructureType.MemoryAllocateInfo,
                allocationSize = size,
            };

            Vortice.Vulkan.Vulkan.vkGetPhysicalDeviceMemoryProperties(pDevice, out var physicalDeviceMemoryProperties);

            var typeBits = memoryRequirements.memoryTypeBits;
            for (uint i = 0; i < physicalDeviceMemoryProperties.memoryTypeCount; i++)
            {
                if ((typeBits & 1) == 1)
                {
                    // Type is available, does it match user properties?
                    var memoryType = *(&physicalDeviceMemoryProperties.memoryTypes_0 + i);
                    if ((memoryType.propertyFlags & memoryProperties) == memoryProperties)
                    {
                        allocateInfo.memoryTypeIndex = i;
                        break;
                    }
                }
                typeBits >>= 1;
            }

            NativeMemory = new VkDeviceMemory();

            fixed (VkDeviceMemory* nativeMemoryPtr = &NativeMemory)
                Vortice.Vulkan.Vulkan.vkAllocateMemory(device, &allocateInfo, null, nativeMemoryPtr);
        }

        public static bool Free(Buffer buf, ulong offset)
        {
            if (BigMemoryPool != VkDeviceMemory.Null && (ulong)buf.SizeInBytes >= Buffer.StagedMeshSmallBufferSize)
            {
                FreeBig.Enqueue(offset);
                return true;
            }
            else if (SmallMemoryPool != VkDeviceMemory.Null)
            {
                FreeSmall.Enqueue(offset);
                return true;
            }
            
            return false;
        }

        public static VkDeviceMemory AllocateMemoryForBuffer(ulong size, VkDevice device, VkPhysicalDevice pDevice, ref VkMemoryRequirements memoryRequirements, ref VkMemoryPropertyFlags memoryProperties, out ulong offset)
        {
            if (size >= Buffer.StagedMeshLargeBufferSize)
            {
                throw new InvalidOperationException("Requested too big memory from pool: " + size);
            }
            else if (size >= Buffer.StagedMeshSmallBufferSize) 
            {
                if (BigMemoryPool == VkDeviceMemory.Null)
                {
                    AllocatePool(out BigMemoryPool, ref device, ref pDevice, ref memoryProperties, ref memoryRequirements, Buffer.StagedMeshLargeBufferCount * Buffer.StagedMeshLargeBufferSize);
                    for (ulong i = 0; i < Buffer.StagedMeshLargeBufferCount; i++)
                        FreeBig.Enqueue(i * Buffer.StagedMeshLargeBufferSize);
                }

                if (FreeBig.TryDequeue(out offset))
                    return BigMemoryPool;

                throw new OutOfMemoryException("Ran out of Big Memory pool space!");
            }
            else
            {
                if (SmallMemoryPool == VkDeviceMemory.Null)
                {
                    AllocatePool(out SmallMemoryPool, ref device, ref pDevice, ref memoryProperties, ref memoryRequirements, Buffer.StagedMeshSmallBufferCount * Buffer.StagedMeshSmallBufferSize);
                    for (ulong i = 0; i < Buffer.StagedMeshSmallBufferCount; i++)
                        FreeSmall.Enqueue(i * Buffer.StagedMeshSmallBufferSize);
                }

                if (FreeSmall.TryDequeue(out offset))
                    return SmallMemoryPool;

                throw new OutOfMemoryException("Ran out of Small Memory pool space!");
            }
        }
    }
}

#endif
