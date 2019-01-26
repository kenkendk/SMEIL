using System;

namespace SMEIL.Parser.AST
{
    /// <summary>
    /// Represents a parenthesized expression
    /// </summary>
    public class ParenthesizedExpression : Expression
    {
        /// <summary>
        /// The expression inside the parenthesis
        /// </summary>
        public readonly Expression Expression;

        /// <summary>
        /// Constructs a new parenthesized expression
        /// </summary>
        /// <param name="token">The parse token</param>
        /// <param name="expression">The expression inside the parenthesis</param>
        public ParenthesizedExpression(ParseToken token, Expression expression)
            : base(token)
        {
            Expression = expression ?? throw new ArgumentNullException(nameof(expression));
        }
    }
}
