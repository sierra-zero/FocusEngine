using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Xenko.Core.Threading
{
    /// <summary>
    /// Minimally protected collector designed for simplicity and speed. Only Add is really thread-safe.
    /// </summary>
    public class FastConcurrentCollector<T>
    {
        private int len;

        public int Count => len;
        public T[] Collected;

        public int Capacity => Collected.Length;

        public FastConcurrentCollector(int capacity) {
            Collected = new T[capacity];
        }

        public void Resize(int newSize)
        {
            Array.Resize<T>(ref Collected, newSize);
        }

        public void Clear()
        {
            len = 0;
        }

        public void Add(T item)
        {
            int index = Interlocked.Increment(ref len) - 1;
            Collected[index] = item;
        }
    }
}
