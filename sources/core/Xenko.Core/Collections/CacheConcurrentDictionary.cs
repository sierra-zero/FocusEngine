using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Xenko.Core.Collections
{
    public class CacheConcurrentDictionary<TKey, TValue>
    {

        private ConcurrentDictionary<TKey, TValue> dictionary;
        private ConcurrentQueue<TKey> keys;
        private int capacity;

        public CacheConcurrentDictionary(int capacity)
        {
            this.keys = new ConcurrentQueue<TKey>();
            this.capacity = capacity;
            this.dictionary = new ConcurrentDictionary<TKey, TValue>(4, capacity);
        }

        public void Add(TKey key, TValue value)
        {
            if (dictionary.Count == capacity)
            {
                if (keys.TryDequeue(out TKey oldestKey)) dictionary.TryRemove(oldestKey, out _);
            }

            if(dictionary.TryAdd(key, value))
                keys.Enqueue(key);
        }

        public void Clear()
        {
            dictionary.Clear();
            while (keys.TryDequeue(out _)) { }
        }

        public bool TryGet(TKey key, out TValue val)
        {
            return dictionary.TryGetValue(key, out val);
        }

        public TValue this[TKey key]
        {
            get { return dictionary[key]; }
        }
    }
}
