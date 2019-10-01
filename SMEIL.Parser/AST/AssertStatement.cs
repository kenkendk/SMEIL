using System;
using System.Collections.Generic;

namespace SMEIL.Parser.AST
{
    public class AssertStatement : Statement
    {
        /// <summary>
        /// The expression to assert
        /// </summary>
        public readonly Expression Expression;

        /// <summary>
        /// The message to display if the assert fails
        /// </summary>
        public readonly string Message;

        /// <summary>
        /// Constructs a new assert statement
        /// </summary>
        /// <param name="token">The parse token</param>
        /// <param name="expression">The expression to assert</param>
        /// <param name="message">The message to write if the assert fails</param>
        public AssertStatement(ParseToken token, Expression expression, string message)
            : base(token)
        {
            Expression = expression ?? throw new ArgumentNullException(nameof(expression));
            Message = message;
        }

        /// <summary>
        /// Clones this statement and returns a copy of it
        /// </summary>
        /// <returns>A copy of the statement</returns>
        public override Statement Clone()
            => new AssertStatement(
                this.SourceToken,
                Expression.Clone(),
                Message
            );

    }
}