using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

namespace SMEIL.Parser.Validation
{
    /// <summary>
    /// A chained dictionary allowing reference to a parent dictionary, where children shadow the parent values
    /// </summary>
    /// <typeparam name="TKey">The key type for the dictionary</typeparam>
    /// <typeparam name="TValue">The value type for the dictionary</typeparam>
    public class ChainedDictionary<TKey, TValue> : IDictionary<TKey, TValue>
    {
        /// <summary>
        /// The parent dictionary
        /// </summary>
        private readonly IDictionary<TKey, TValue> m_parent;
        /// <summary>
        /// The shadow dictionary
        /// </summary>
        private readonly Dictionary<TKey, TValue> m_self = new Dictionary<TKey, TValue>();

        /// <summary>
        /// Creates a new chained dictionary
        /// </summary>
        /// <param name="parent">The parent dictionary, can be <c>null</c></param>
        public ChainedDictionary(IDictionary<TKey, TValue> parent)
        {
            m_parent = parent;
        }

        /// <summary>
        /// Gets a value indicating if this scope contains a specific key
        /// (i.e. not traversing the symbols stack)
        /// </summary>
        /// <param name="key">The key to look for</param>
        /// <returns></returns>
        internal bool SelfContainsKey(TKey key)
        {
            return m_self.ContainsKey(key);
        }

        /// <summary>
        /// Builds a list of dictionaries by walking the parents
        /// </summary>
        /// <returns>The list of dictionaries</returns>
        internal List<ChainedDictionary<TKey, TValue>> GetList()
        {
            var res = new List<ChainedDictionary<TKey, TValue>>();
            var p = this;
            while(p != null)
            {
                res.Add(p);
                p = p.m_parent as ChainedDictionary<TKey, TValue>;
            }

            res.Reverse();
            return res;
        }

        /// <inheritdoc />
        public void Add(TKey key, TValue value)
        {
            m_self.Add(key, value);
        }

        /// <inheritdoc />
        public void Add(KeyValuePair<TKey, TValue> item)
        {
            ((IDictionary<TKey, TValue>)m_self).Add(item);
        }

        /// <inheritdoc />
        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            return m_self.Contains(item) || (m_parent != null && m_parent.Contains(item));
        }

        /// <inheritdoc />
        public TValue this[TKey key]
        {
            get 
            {
                if (m_self.ContainsKey(key))
                    return m_self[key];
                if (m_parent != null)
                    return m_parent[key];

                // Throw exception
                return m_self[key];
            }
            set
            {
                m_self[key] = value;
            }
        }

        /// <inheritdoc />
        public bool ContainsKey(TKey key)
        {
            return m_self.ContainsKey(key) || (m_parent != null && m_parent.ContainsKey(key));
        }

        /// <inheritdoc />
        public bool TryGetValue(TKey key, out TValue value)
        {
            if (m_self.TryGetValue(key, out value))
                return true;
            if (m_parent != null && m_parent.TryGetValue(key, out value))
                return true;

            return false;
        }


        /// <inheritdoc />
        public bool Remove(TKey key)
        {
            return m_self.Remove(key);
        }

        /// <inheritdoc />
        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            return ((IDictionary<TKey, TValue>)m_self).Remove(item);
        }

        /// <inheritdoc />
        public void Clear()
        {
            throw new InvalidOperationException("Clear is not supported on chained dictionary");
        }

        /// <inheritdoc />
        public ICollection<TKey> Keys => m_parent == null ? m_self.Keys : (ICollection<TKey>)m_self.Keys.Concat(m_parent.Keys).ToList();
        /// <inheritdoc />
        public ICollection<TValue> Values => m_parent == null ? m_self.Values : (ICollection<TValue>)m_self.Values.Concat(m_parent.Values).ToList();

        /// <inheritdoc />
        public int Count => m_self.Count + (m_parent == null ? 0 : m_parent.Count);
        /// <inheritdoc />
        public bool IsReadOnly => ((IDictionary)m_self).IsReadOnly;

        /// <inheritdoc />
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => m_parent == null ? m_self.GetEnumerator() : m_self.Concat(m_parent).GetEnumerator();

        /// <inheritdoc />
        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            ((IDictionary<TKey, TValue>)m_self).CopyTo(array, arrayIndex);
            if (m_parent != null)
                ((IDictionary<TKey, TValue>)m_parent).CopyTo(array, arrayIndex + m_self.Count);
        }

        /// <inheritdoc />
        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
    }
}