using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Xenko.Core.Collections
{
    public class CacheDictionary<TKey, TValue>
    {

        private Dictionary<TKey, TValue> dictionary;
        private Queue<TKey> keys;
        private int capacity;

        public CacheDictionary(int capacity)
        {
            this.keys = new Queue<TKey>();
            this.capacity = capacity;
            this.dictionary = new Dictionary<TKey, TValue>(capacity);
        }

        public Action<TValue> DisposeAction;

        public void Add(TKey key, TValue value)
        {
            if (dictionary.Count == capacity)
            {
                if (DisposeAction == null)
                {
                    while (keys.Count > 0)
                    {
                        TKey oldestKey = keys.Dequeue();
                        if (dictionary.Remove(oldestKey)) break;
                    }
                }
                else
                {
                    while (keys.Count > 0)
                    {
                        TKey oldestKey = keys.Dequeue();
                        if (dictionary.TryGetValue(oldestKey, out TValue val))
                        {
                            DisposeAction(val);
                            dictionary.Remove(oldestKey);
                            break;
                        }
                    }
                }
            }

            dictionary.Add(key, value);
            keys.Enqueue(key);
        }

        public void Clear()
        {
            if (DisposeAction != null)
            {
                foreach (TValue t in dictionary.Values)
                    DisposeAction(t);
            }
            dictionary.Clear();
            keys.Clear();
        }

        public bool TryPull(TKey key, out TValue val)
        {
            if (dictionary.TryGetValue(key, out val))
            {
                dictionary.Remove(key);
                return true;
            }
            return false;
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
