using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace SMEIL.Parser.AST
{
    /// <summary>
    /// Represents an array index literal
    /// </summary>
    [DebuggerDisplay("[ {Index} ]")]
    public class ArrayIndexLiteral : Constant
    {
        /// <summary>
        /// The index value
        /// </summary>
        public readonly IntegerConstant Index;

        /// <summary>
        /// Constructs a new array index literal
        /// </summary>
        /// <param name="token">The token where the literal was found</param>
        /// <param name="index">The index value</param>
        public ArrayIndexLiteral(ParseToken token, IntegerConstant index)
            : base(token)
        {
            Index = index ?? throw new ArgumentNullException(nameof(index));
        }
    }
}