// Copyright (c) Xenko contributors (https://xenko.com) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Xenko.Core.Annotations;

namespace Xenko.Core.Threading
{
    /// <summary>
    /// Thread pool for scheduling actions.
    /// </summary>
    /// <remarks>
    /// Base on Stephen Toub's ManagedThreadPool
    /// </remarks>
    internal class ThreadPool
    {
        public static readonly ThreadPool Instance = new ThreadPool();
        private static TimeSpan maxIdleTime = TimeSpan.FromTicks(5 * TimeSpan.TicksPerSecond);

        private readonly int maxThreadCount = Environment.ProcessorCount + 2;
        private readonly Queue<Action> workItems = new Queue<Action>();
        private readonly ManualResetEvent workAvailable = new ManualResetEvent(false);

        private readonly Action<object> cachedTaskLoop;

        private SpinLock spinLock = new SpinLock();
        private int workingCount;
        /// <summary> Usage only within <see cref="spinLock"/> </summary>
        private int aliveCount;

        public ThreadPool()
        {
            // Cache delegate to avoid pointless allocation
            cachedTaskLoop = (o) => ProcessWorkItems();
        }

        public void QueueWorkItem([NotNull] [Pooled] Action workItem)
        {
            PooledDelegateHelper.AddReference(workItem);
            bool lockTaken = false;
            bool startNewTask = false;
            try
            {
                spinLock.Enter(ref lockTaken);

                workItems.Enqueue(workItem);
                workAvailable.Set();

                var curWorkingCount = Interlocked.CompareExchange(ref workingCount, 0, 0);
                if (curWorkingCount + 1 >= aliveCount && aliveCount < maxThreadCount)
                {
                    startNewTask = true;
                    aliveCount++;
                }
            }
            finally
            {
                if (lockTaken)
                    spinLock.Exit(true);
            }
            // No point in wasting spins on the lock while creating the task
            if (startNewTask)
            {
                new Task(cachedTaskLoop, null, TaskCreationOptions.LongRunning).Start();
            }
        }

        private void ProcessWorkItems()
        {
            while (true)
            {
                Action workItem = null;
                var lockTaken = false;
                try
                {
                    spinLock.Enter(ref lockTaken);
                    int workItemCount = workItems.Count;

                    if (workItemCount > 0)
                    {
                        workItem = workItems.Dequeue();
                        if (workItemCount == 1) workAvailable.Reset(); // we must have taken off our last item
                    }
                }
                finally
                {
                    if (lockTaken)
                        spinLock.Exit(true);
                }

                if (workItem != null)
                {
                    Interlocked.Increment(ref workingCount);
                    try
                    {
                        workItem.Invoke();
                    }
                    catch (Exception)
                    {
                        // Ignoring Exception
                    }
                    Interlocked.Decrement(ref workingCount);
                    PooledDelegateHelper.Release(workItem);
                }

                // Wait for another work item to be (potentially) available
                if (workAvailable.WaitOne(maxIdleTime) == false)
                {
                    aliveCount--;
                    return;
                }
            }
        }
    }
}
