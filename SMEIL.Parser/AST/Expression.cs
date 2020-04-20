using System.Diagnostics;
using System;
namespace SMEIL.Parser.AST
{
    /// <summary>
    /// Represents an expression
    /// </summary>
    [DebuggerDisplay("{AsString}")]
    public abstract class Expression : ParsedItem
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="T:SMEIL.Parser.AST.Expression"/> class.
        /// </summary>
        /// <param name="token">The source token.</param>
        protected Expression(ParseToken token)
            : base(token)
        {
        }

        /// <summary>
        /// Clones this expression and returns a copy of it
        /// </summary>
        /// <returns>A copy of the expression</returns>
        public abstract Expression Clone();

        /// <summary>
        /// Returns a string representation of the expression, suitable for debugging
        /// </summary>
        public abstract string AsString { get; }

    }
}
