using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace SMEIL.Parser.AST
{
    /// <summary>
    /// Represents an array index
    /// </summary>
    [DebuggerDisplay("[ {Index} ]")]
    public class ArrayIndex : ParsedItem
    {
        /// <summary>
        /// The index into the array, or null if the wildcard is used
        /// </summary>
        public readonly Expression Index;

        /// <summary>
        /// Constructs a new array index for a wildcard item
        /// </summary>
        /// <param name="token">The token for the index</param>
        public ArrayIndex(ParseToken token)
            : base(token)
        {
            this.Index = null;
        }

        /// <summary>
        /// Constructs a new array index
        /// </summary>
        /// <param name="token">The token for the index</param>
        /// <param name="index">The index value</param>
        public ArrayIndex(ParseToken token, Expression index)
            : base(token)
        {
            this.Index = index ?? throw new ArgumentNullException(nameof(index));
        }
    }
}