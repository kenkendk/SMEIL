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

    }
}
