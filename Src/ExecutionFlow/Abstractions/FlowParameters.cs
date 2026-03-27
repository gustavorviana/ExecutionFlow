using System;
using System.Collections;
using System.Collections.Generic;

namespace ExecutionFlow.Abstractions
{
    /// <summary>
    /// A dictionary of execution parameters with support for read-only infrastructure keys.
    /// Infrastructure parameters (set by the framework) cannot be modified or removed.
    /// Custom parameters can be added, modified, and removed freely by handlers.
    /// </summary>
    public class FlowParameters : IReadOnlyDictionary<string, object>
    {
        private readonly Dictionary<string, object> _items = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _readOnlyKeys = new HashSet<string>();

        IEnumerable<string> IReadOnlyDictionary<string, object>.Keys => Keys;

        IEnumerable<object> IReadOnlyDictionary<string, object>.Values => Values;

        /// <summary>Gets all parameter keys.</summary>
        public ICollection<string> Keys => _items.Keys;

        /// <summary>Gets all parameter values.</summary>
        public ICollection<object> Values => _items.Values;

        /// <summary>Gets the total number of parameters.</summary>
        public int Count => _items.Count;

        /// <summary>
        /// Initializes a new empty instance of <see cref="FlowParameters"/>.
        /// </summary>
        public FlowParameters()
        {
        }

        /// <summary>
        /// Gets or sets the value associated with the specified key.
        /// Throws <see cref="InvalidOperationException"/> if the key is a read-only infrastructure parameter.
        /// </summary>
        /// <param name="key">The parameter key.</param>
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

        /// <summary>
        /// Adds a new parameter. Throws if the key is a read-only infrastructure parameter.
        /// </summary>
        /// <param name="key">The parameter key.</param>
        /// <param name="value">The parameter value.</param>
        public void Add(string key, object value)
        {
            ThrowIfReadOnly(key);
            _items.Add(key, value);
        }

        /// <summary>
        /// Removes a parameter. Throws if the key is a read-only infrastructure parameter.
        /// </summary>
        /// <param name="key">The parameter key to remove.</param>
        /// <returns><c>true</c> if the parameter was removed; otherwise, <c>false</c>.</returns>
        public bool Remove(string key)
        {
            ThrowIfReadOnly(key);
            return _items.Remove(key);
        }

        /// <summary>
        /// Determines whether a parameter with the specified key exists.
        /// </summary>
        /// <param name="key">The key to check.</param>
        public bool ContainsKey(string key) => _items.ContainsKey(key);

        /// <summary>
        /// Gets the value associated with the specified key, if it exists.
        /// </summary>
        /// <param name="key">The key to look up.</param>
        /// <param name="value">The value if found.</param>
        /// <returns><c>true</c> if the key was found; otherwise, <c>false</c>.</returns>
        public bool TryGetValue(string key, out object value) => _items.TryGetValue(key, out value);

        /// <inheritdoc />
        public void Add(KeyValuePair<string, object> item)
        {
            ThrowIfReadOnly(item.Key);
            ((ICollection<KeyValuePair<string, object>>)_items).Add(item);
        }

        /// <inheritdoc />
        public bool Remove(KeyValuePair<string, object> item)
        {
            ThrowIfReadOnly(item.Key);
            return ((ICollection<KeyValuePair<string, object>>)_items).Remove(item);
        }

        /// <inheritdoc />
        public IEnumerator<KeyValuePair<string, object>> GetEnumerator() => _items.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private void ThrowIfReadOnly(string key)
        {
            if (_readOnlyKeys.Contains(key))
                throw new InvalidOperationException($"Parameter '{key}' is read-only and cannot be modified.");
        }
    }
}
