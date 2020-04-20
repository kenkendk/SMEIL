using System.Diagnostics;
using System;
using System.Collections.Generic;

namespace SMEIL.Parser.AST
{
    /// <summary>
    /// Represents a binary expression
    /// </summary>
    [DebuggerDisplay("{AsString}")]
    public class BinaryExpression : Expression
    {
        /// <summary>
        /// The left-hand-side expression
        /// </summary>
        public Expression Left;
        /// <summary>
        /// The operation
        /// </summary>
        public readonly BinaryOperation Operation;
        /// <summary>
        /// The right-hand-side expression
        /// </summary>
        public Expression Right;

        /// <summary>
        /// Constructs a new binary expression
        /// </summary>
        /// <param name="token">The parse token</param>
        /// <param name="left">The left-hand-side expression</param>
        /// <param name="operation">The operation</param>
        /// <param name="right">The right-hand-side expression</param>
        public BinaryExpression(ParseToken token, Expression left, BinaryOperation operation, Expression right)
            : base(token)
        {
            Left = left ?? throw new ArgumentNullException(nameof(left));
            Operation = operation ?? throw new ArgumentNullException(nameof(operation));
            Right = right ?? throw new ArgumentNullException(nameof(right));
        }

        /// <summary>
        /// Returns a string representation of the expression, suitable for debugging
        /// </summary>
        public override string AsString => $"{Left?.AsString} {Operation.AsString} {Right?.AsString}";

        /// <summary>
        /// Clones this expression and returns a copy of it
        /// </summary>
        /// <returns>A copy of the expression</returns>
        public override Expression Clone()
            => new BinaryExpression(
                SourceToken,
                Left.Clone(),
                Operation,
                Right.Clone()
            );

    }
}
