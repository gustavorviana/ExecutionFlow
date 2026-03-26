using System;
using System.Collections;
using System.Collections.Generic;

namespace ExecutionFlow.Abstractions
{
    public class FlowParameters : IDictionary<string, object>
    {
        private readonly Dictionary<string, object> _items = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _readOnlyKeys = new HashSet<string>();

        public FlowParameters()
        {
        }

        internal void AddReadOnly(string key, object value)
        {
            _items[key] = value;
            _readOnlyKeys.Add(key);
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

        public ICollection<string> Keys => _items.Keys;

        public ICollection<object> Values => _items.Values;

        public int Count => _items.Count;

        public bool IsReadOnly => false;

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

        public void Clear()
        {
            foreach (var key in _readOnlyKeys)
                if (!_items.ContainsKey(key))
                    continue;

            var keysToRemove = new List<string>();
            foreach (var key in _items.Keys)
                if (!_readOnlyKeys.Contains(key))
                    keysToRemove.Add(key);

            foreach (var key in keysToRemove)
                _items.Remove(key);
        }

        public bool Contains(KeyValuePair<string, object> item) =>
            ((ICollection<KeyValuePair<string, object>>)_items).Contains(item);

        public void CopyTo(KeyValuePair<string, object>[] array, int arrayIndex) =>
            ((ICollection<KeyValuePair<string, object>>)_items).CopyTo(array, arrayIndex);

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator() => _items.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private void ThrowIfReadOnly(string key)
        {
            if (_readOnlyKeys.Contains(key))
                throw new InvalidOperationException($"Parameter '{key}' is read-only and cannot be modified.");
        }
    }
}