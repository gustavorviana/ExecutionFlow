using System;
using System.Collections;
using System.Collections.Generic;

namespace ExecutionFlow.Abstractions
{
    public class FlowParameters : IReadOnlyDictionary<string, object>
    {
        private readonly Dictionary<string, object> _items = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _readOnlyKeys = new HashSet<string>();

        IEnumerable<string> IReadOnlyDictionary<string, object>.Keys => Keys;

        IEnumerable<object> IReadOnlyDictionary<string, object>.Values => Values;

        public ICollection<string> Keys => _items.Keys;

        public ICollection<object> Values => _items.Values;

        public int Count => _items.Count;

        public FlowParameters()
        {
        }

        public object this[string key]
        {
            get => _items[key];
            set
            {
                ThrowIfReadOnly(key);
                _items[key] = value;
            }
        }

        internal void AddReadOnly(string key, object value)
        {
            _items[key] = value;
            _readOnlyKeys.Add(key);
        }

        public void Add(string key, object value)
        {
            ThrowIfReadOnly(key);
            _items.Add(key, value);
        }

        public bool Remove(string key)
        {
            ThrowIfReadOnly(key);
            return _items.Remove(key);
        }

        public bool ContainsKey(string key) => _items.ContainsKey(key);

        public bool TryGetValue(string key, out object value) => _items.TryGetValue(key, out value);

        public void Add(KeyValuePair<string, object> item)
        {
            ThrowIfReadOnly(item.Key);
            ((ICollection<KeyValuePair<string, object>>)_items).Add(item);
        }

        public bool Remove(KeyValuePair<string, object> item)
        {
            ThrowIfReadOnly(item.Key);
            return ((ICollection<KeyValuePair<string, object>>)_items).Remove(item);
        }

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator() => _items.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private void ThrowIfReadOnly(string key)
        {
            if (_readOnlyKeys.Contains(key))
                throw new InvalidOperationException($"Parameter '{key}' is read-only and cannot be modified.");
        }
    }
}