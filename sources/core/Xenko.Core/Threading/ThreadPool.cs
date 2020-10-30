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

        // have a few more available threads than the engine can consume in a single dispatch
        // this should hopefully reduce thread pool exaustion which can happen with many
        // heavy multithreaded subsystems (e.g. bepu physics)
        private const int EXTRA_THREADS = 3;

        public ThreadPool()
        {
            // fire up worker threads
            ThreadStart ts = new ThreadStart(ProcessWorkItems);
            for (int i = 0; i < Dispatcher.MaxDegreeOfParallelism + EXTRA_THREADS; i++)
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
                try
                {
                    spinLock.Enter(ref lockTaken);

                    if (workItems.Count > 0)
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
                // couldn't find work, wait until some more is available
                else workAvailable.WaitOne();
            }
        }
    }
}
