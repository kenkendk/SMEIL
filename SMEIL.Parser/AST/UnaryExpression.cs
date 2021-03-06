using System;

namespace SMEIL.Parser.AST
{
    /// <summary>
    /// Represents a unary expression
    /// </summary>
    public class UnaryExpression : Expression
    {
        /// <summary>
        /// The operation to apply
        /// </summary>
        public readonly UnaryOperation Operation;

        /// <summary>
        /// The expression the operator is applied to
        /// </summary>
        public readonly Expression Expression;

        /// <summary>
        /// Creates a new unary expression
        /// </summary>
        /// <param name="token">The parse token</param>
        /// <param name="operation">The unary operation</param>
        /// <param name="expression">The expression the operator is applied to</param>
        public UnaryExpression(ParseToken token, UnaryOperation operation, Expression expression)
            : base(token)
        {
            Operation = operation ?? throw new ArgumentNullException(nameof(operation));
            Expression = expression ?? throw new ArgumentNullException(nameof(expression));
        }

        /// <summary>
        /// Returns a string representation of the expression, suitable for debugging
        /// </summary>
        public override string AsString => $"{Operation.AsString}{Expression.AsString}";

        /// <summary>
        /// Clones this expression and returns a copy of it
        /// </summary>
        /// <returns>A copy of the expression</returns>
        public override Expression Clone()
            => new UnaryExpression(
                SourceToken,
                Operation,
                Expression.Clone()
            );

    }
}
