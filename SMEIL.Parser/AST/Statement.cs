using System;
namespace SMEIL.Parser.AST
{
    /// <summary>
    /// Represents a statement
    /// </summary>
    public abstract class Statement : ParsedItem
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="T:SMEIL.Parser.AST.Statement"/> class.
        /// </summary>
        /// <param name="token">The source token.</param>
        protected Statement(ParseToken token)
            : base(token)
        {
        }

        /// <summary>
        /// Clones this statement and returns a copy of it
        /// </summary>
        /// <returns>A copy of the statement</returns>
        public abstract Statement Clone();
    }
}
