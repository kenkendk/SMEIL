using System.Collections.Generic;

namespace SMEIL.Parser.AST
{
    /// <summary>
    /// Represents a break statement
    /// </summary>
    public class BreakStatement : Statement
    {
        /// <summary>
        /// Creates a new break statement
        /// </summary>
        /// <param name="token">The parse token</param>
        public BreakStatement(ParseToken token)
            : base(token)
        {
        }
    }
}