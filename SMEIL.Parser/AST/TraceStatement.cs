using System;

namespace SMEIL.Parser.AST
{
    /// <summary>
    /// Represents a trace statement
    /// </summary>
    public class TraceStatement : Statement
    {
        /// <summary>
        /// The format string used to format the output
        /// </summary>
        public readonly string Format;
        /// <summary>
        /// The expressions to format
        /// </summary>
        public readonly Expression[] Expressions;

        /// <summary>
        /// Constructs a new trace statement
        /// </summary>
        /// <param name="token">The parse token</param>
        /// <param name="format">The format string</param>
        /// <param name="expressions">The expressions to format</param>
        public TraceStatement(ParseToken token, string format, Expression[] expressions)
            : base(token)
        {
            Format = format ?? throw new ArgumentNullException(nameof(format));
            Expressions = expressions ?? throw new ArgumentNullException(nameof(expressions));
        }
    }
}