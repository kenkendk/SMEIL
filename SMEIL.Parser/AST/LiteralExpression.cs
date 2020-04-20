using System;
using System.Diagnostics;

namespace SMEIL.Parser.AST
{
    /// <summary>
    /// Represents a literal as an expression
    /// </summary>
    [DebuggerDisplay("{Value}")]
    public class LiteralExpression : Expression
    {
        /// <summary>
        /// The literal in this expression
        /// </summary>
        public readonly Constant Value; 

        /// <summary>
        /// Creates a new literal expression
        /// </summary>
        /// <param name="token">The token where the literal was found</param>
        /// <param name="value">The value</param>
        public LiteralExpression(ParseToken token, Constant value)
            : base(token)
        {
            Value = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <summary>
        /// Returns a string representation of the expression, suitable for debugging
        /// </summary>
        public override string AsString => Value?.SourceToken.Text;

        /// <summary>
        /// Clones this expression and returns a copy of it
        /// </summary>
        /// <returns>A copy of the expression</returns>
        public override Expression Clone()
            => new LiteralExpression(
                SourceToken,
                Value
            );
    }
}