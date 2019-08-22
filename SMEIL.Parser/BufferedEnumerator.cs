using System;
using System.Collections.Generic;

namespace SMEIL.Parser
{
    /// <summary>
    /// Interface for adding and removing items to an enumerator
    /// </summary>
    /// <typeparam name="T">The items in the enumerator</typeparam>
    public interface IBufferedEnumerator<T> : IEnumerator<T>
    {
        /// <summary>
        /// Pushes an item onto the enumerator
        /// </summary>
        /// <param name="item">The item to add</param>
        void Push(T item);
        /// <summary>
        /// Removes a single item from the enumerator
        /// </summary>
        /// <returns>The item removed</returns>
        T Pop();

        /// <summary>
        /// Peeks for the next element
        /// </summary>
        /// <returns>The next element</returns>
        T Peek();

        /// <summary>
        /// Starts a snapshot of the enumerator
        /// </summary>
        void Snapshot();
        /// <summary>
        /// Rolls back the snapshot
        /// </summary>
        void Rollback();
        /// <summary>
        /// Accepts the current state
        /// </summary>
        void Commit();

        /// <summary>
        /// A flag indicating if the enumerator is empty
        /// </summary>
        bool Empty { get; }

        /// <summary>
        /// Gets the current snapshot length
        /// </summary>
        int SnapshotLength { get; }
    }

    // TODO: This should be rewritten to just have a single
    // buffered copy of the source, and then use an offset+count
    // for each snapshot instead of the elaborate copying
    // used here

    /// <summary>
    /// Represents an enumerator that can have items prepended to the stream
    /// </summary>
    public class BufferedEnumerator<T> : IBufferedEnumerator<T>
    {
        /// <summary>
        /// The enumerator being wrapped
        /// </summary>
        private readonly IEnumerator<T> m_parent;

        /// <summary>
        /// A queue with head items
        /// </summary>
        private readonly Stack<T> m_buffer = new Stack<T>(2);

        /// <summary>
        /// The snapshots
        /// </summary>
        private readonly Stack<Stack<T>> m_snapshots = new Stack<Stack<T>>();

        /// <summary>
        /// Creates a new buffered enumerator
        /// </summary>
        /// <param name="parent">The item to wrap</param>
        /// <param name="beforeFirst"><c>true</c> if the current item is before the first entry</param>
        public BufferedEnumerator(IEnumerator<T> parent, bool beforeFirst = true)
        {
            m_parent = parent ?? throw new ArgumentNullException(nameof(parent));
            if (beforeFirst)
                MoveNext();
        }

        /// <summary>
        /// Gets the current item in the stream
        /// </summary>
        /// <returns>The current item</returns>
        public T Current => m_buffer.Count == 0 ? m_parent.Current : m_buffer.Peek();

        /// <summary>
        /// Gets a value that indicates if the parent is empty
        /// </summary>
        private bool m_parentIsEmpty = false;
        /// <summary>
        /// Gets a value indicating if the sequence is empty
        /// </summary>
        public bool Empty => m_buffer.Count == 0 && m_parentIsEmpty;

        /// <summary>
        /// Gets the number of items in the current snapshot
        /// </summary>
        /// <returns></returns>
        public int SnapshotLength => m_snapshots.Count == 0 ? 0 : m_snapshots.Peek().Count;

        /// <summary>
        /// Gets the current item
        /// </summary>
        object System.Collections.IEnumerator.Current => Current;
        
        /// <summary>
        /// Adds an item to the sequence which will be the current item
        /// </summary>
        /// <param name="item">The item to add</param>
        public void Push(T item)
        {
            m_buffer.Push(item);
        }

        /// <summary>
        /// Gets the current head of the list
        /// </summary>
        /// <returns>The current head</returns>
        public T Pop()
        {
            var n = Current;
            MoveNext();
            return n;
        }

        /// <summary>
        /// Returns the next element in the sequence
        /// </summary>
        /// <returns>The next element in the sequence, if any</returns>
        public T Peek()
        {
            if (m_buffer.Count > 1)
            {
                var tmp = m_buffer.Pop();
                m_buffer.Push(tmp);

                return tmp;
            }
            else
            {
                // Move the current element into the buffer
                if (m_buffer.Count == 0 && !m_parentIsEmpty)
                    Push(Pop());

                return m_parentIsEmpty 
                    ? default(T)
                    : m_parent.Current;
            }
        }

        /// <summary>
        /// Advances the enumerator one element
        /// </summary>
        /// <returns><c>true</c> if the current element is valid, false otherwise</returns>
        public bool MoveNext()
        {
            if (!Empty && m_snapshots.Count > 0)
                m_snapshots.Peek().Push(Current);

            if (m_buffer.Count != 0)
                m_buffer.Pop();
            else if (!m_parentIsEmpty)
                m_parentIsEmpty = !m_parent.MoveNext();

            return !Empty;
        }

        /// <summary>
        /// Resets the current enumerator
        /// </summary>
        public void Reset()
        {
            m_buffer.Clear();
            m_parent.Reset();
        }

        /// <summary>
        /// Starts a new snapshot
        /// </summary>
        public void Snapshot()
        {
            m_snapshots.Push(new Stack<T>());
            //m_snapshots.Peek().Push(Current);
        }

        /// <summary>
        /// Resets the enumerator to the state before the snapshot started
        /// </summary>
        public void Rollback()
        {
            var n = m_snapshots.Pop();
            while(n.Count > 0)
                Push(n.Pop());
        }

        /// <summary>
        /// Accepts the current state
        /// </summary>
        public void Commit()
        {
            var n = m_snapshots.Pop();
            if (m_snapshots.Count > 0 && n.Count > 0)
            {
                // When we copy from stack to stack,
                // we need to reverse insert order
                // to preserve the stored order
                var els = n.ToArray();
                for(var i = els.Length - 1; i >= 0; i--)
                    m_snapshots.Peek().Push(els[i]);
            }
        }

        /// <summary>
        /// Releases all current resources
        /// </summary>
        public void Dispose()
        {
            m_buffer.Clear();
            m_parent.Dispose();
        }
    }
}