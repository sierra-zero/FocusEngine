// Copyright (c) Xenko contributors (https://xenko.com) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Collections;
using System.Collections.Generic;
using Xenko.Core;

namespace Xenko.Graphics
{
    /// <summary>
    /// A graphics command context. You should usually stick to one per rendering thread.
    /// </summary>
    public class GraphicsContext
    {
        /// <summary>
        /// Gets the current command list.
        /// </summary>
        public CommandList CommandList { get; set; }

        /// <summary>
        /// Gets the current resource group allocator.
        /// </summary>
        public ResourceGroupAllocator ResourceGroupAllocator { get; private set; }

        public GraphicsResourceAllocator Allocator { get; private set; }

        public static int PrepareAllocatorCount = 32;
        private static Queue<ResourceGroupAllocator> AvailableAllocators;

        public GraphicsContext(GraphicsDevice graphicsDevice, GraphicsResourceAllocator allocator = null, CommandList commandList = null)
        {
            CommandList = commandList ?? graphicsDevice.InternalMainCommandList ?? CommandList.New(graphicsDevice).DisposeBy(graphicsDevice);
            Allocator = allocator ?? new GraphicsResourceAllocator(graphicsDevice).DisposeBy(graphicsDevice);

            // prepare some resources now, so we don't need to do it during runtime (which can cause lag spikes or worse)
            if (AvailableAllocators == null && PrepareAllocatorCount > 0)
            {
                AvailableAllocators = new Queue<ResourceGroupAllocator>();
                while (AvailableAllocators.Count < PrepareAllocatorCount)
                    AvailableAllocators.Enqueue(new ResourceGroupAllocator(Allocator, CommandList, 2).DisposeBy(graphicsDevice));
            }

            if (AvailableAllocators?.Count > 0)
                ResourceGroupAllocator = AvailableAllocators.Dequeue();
            else
                ResourceGroupAllocator = new ResourceGroupAllocator(Allocator, CommandList, 2).DisposeBy(graphicsDevice);
        }
    }
}
