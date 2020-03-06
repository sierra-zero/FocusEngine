// Copyright (c) Xenko contributors (https://xenko.com) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Xenko.Core.Annotations;

namespace Xenko.Core.Threading
{
    /// <summary>
    /// Thread pool for scheduling actions.
    /// </summary>
    public class ThreadPool
    {
        public static readonly ThreadPool Instance = new ThreadPool();

        private readonly Queue<Action> workItems = new Queue<Action>();
        private readonly AutoResetEvent workAvailable = new AutoResetEvent(false);

        private SpinLock spinLock = new SpinLock();

        public ThreadPool()
        {
            // fire up worker threads
            ThreadStart ts = new ThreadStart(ProcessWorkItems);
            for (int i = 0; i < Dispatcher.MaxDegreeOfParallelism; i++)
            {
                Thread t = new Thread(ts);
                t.Name = "ThreadPool #" + i;
                t.IsBackground = true;
                t.Start();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void QueueWorkItem([NotNull] [Pooled] Action workItem)
        {
            PooledDelegateHelper.AddReference(workItem);
            bool lockTaken = false;
            try
            {
                spinLock.Enter(ref lockTaken);
                workItems.Enqueue(workItem);
            }
            finally
            {
                if (lockTaken)
                    spinLock.Exit(true);
            }

            workAvailable.Set();
        }

        private void ProcessWorkItems()
        {
            while (true)
            {
                Action workItem = null;
                var lockTaken = false;
                int workItemCount;
                try
                {
                    spinLock.Enter(ref lockTaken);
                    workItemCount = workItems.Count;

                    if (workItemCount > 0)
                        workItem = workItems.Dequeue();
                }
                finally
                {
                    if (lockTaken)
                        spinLock.Exit(true);
                }

                // do we have a job to do?
                if (workItem != null)
                {
                    try
                    {
                        workItem.Invoke();
                    }
                    catch (Exception)
                    {
                        // Ignoring Exception
                    }
                    PooledDelegateHelper.Release(workItem);
                }

                // only reset and wait if we took our last job
                if (workItemCount <= 1) workAvailable.WaitOne();
            }
        }
    }
}
