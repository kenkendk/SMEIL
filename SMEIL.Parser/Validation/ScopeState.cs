using System;
using System.Collections.Generic;

namespace SMEIL.Parser.Validation
{
    public class ScopeState : IDisposable
    {
        /// <summary>
        /// The actual current scope instance
        /// </summary>
        public readonly IDictionary<string, object> TypedefinitionTable;

        /// <summary>
        /// The symbol table for the current scope
        /// </summary>
        public readonly IDictionary<string, object> SymbolTable;

        /// <summary>
        /// The parent scope
        /// </summary>
        private readonly Stack<ScopeState> m_parent;

        /// <summary>
        /// Flag keeping track of the dispose state
        /// </summary>
        private bool m_isDisposed = false;

        /// <summary>
        /// Creates a new scope
        /// </summary>
        /// <param name="parent">The parent scope chain</param>
        public ScopeState(Stack<ScopeState> parent)
        {
            m_parent = parent ?? throw new ArgumentNullException(nameof(parent));
            SymbolTable = new ChainedDictionary<string, object>(m_parent.Count == 0 ? null : parent.Peek().SymbolTable);
            TypedefinitionTable = new ChainedDictionary<string, object>(m_parent.Count == 0 ? null : parent.Peek().TypedefinitionTable);
            m_parent.Push(this);
        }

        /// <summary>
        /// Disposes the current scope
        /// </summary>
        public void Dispose()
        {
            if (!m_isDisposed)
            {
                m_isDisposed = true;
                if (m_parent.Peek() != this)
                    throw new Exception("Unexpected scope disposal");
                m_parent.Pop();
            }
        }

        /// <summary>
        /// Returns a value indicating if the local symbol table contains the given name
        /// </summary>
        /// <param name="name">The name to find</param>
        /// <returns><c>true</c> if the symbol exists locally; <c>false</c> otherwise</returns>
        public bool SelfContainsSymbol(string name)
        {
            return ((ChainedDictionary<string, object>)SymbolTable).SelfContainsKey(name);
        }

        /// <summary>
        /// Returns a value indicating if the local symbol table contains the given name
        /// </summary>
        /// <param name="name">The name to find</param>
        /// <returns><c>true</c> if the symbol exists locally; <c>false</c> otherwise</returns>
        public bool SelfContainsTypeDef(string name)
        {
            return ((ChainedDictionary<string, object>)SymbolTable).SelfContainsKey(name);
        }

    }
}